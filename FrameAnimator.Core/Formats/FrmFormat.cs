namespace FrameAnimator.Core.Formats
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using Geometry;
    using Model;

    /// <summary>Result of the write preparation step (size of each direction, header offsets…).</summary>
    public sealed class FrmLayout
    {
        public Int32[] Counts { get; } = new Int32[Project.DirectionCount];
        public Int32[] DirectionSize { get; } = new Int32[Project.DirectionCount];
        public (Int32 X, Int32 Y)[] Shift { get; } = new (Int32, Int32)[Project.DirectionCount];
        public Int32 FrameAreaSize { get; set; }
    }

    /// <summary>
    /// Writing and reading of Fallout FRM files (big-endian). See specification §4.2.
    /// The functions of this type show no messages; <see cref="ProjectService"/> handles the dialogs.
    /// </summary>
    public static class FrmFormat
    {
        public const Int32 Version = 4;
        public const Int32 HeaderSize = 62;

        /// <summary>Maximum number of pixels accepted for a frame being read (16 Mpx, far beyond any game image).</summary>
        public const Int32 MaxFramePixels = 16 * 1024 * 1024;
        public const Int32 FrameHeaderSize = 12;

        // ------------------------------------------------------------------ validation

        /// <summary>
        /// Valid frame set for an <c>.FRM</c> file: <c>n0&gt;0</c> and (<c>n0==n1</c> or <c>n1==0</c>) and <c>n2..n5 == n1</c>.
        /// </summary>
        public static Boolean IsValidForFrm(Project p)
        {
            Int32 n0 = p.Directions[0].Count, n1 = p.Directions[1].Count;
            if (n0 == 0)
                return false;
            if (n0 != n1 && n1 != 0)
                return false;
            for (Int32 d = 2; d < Project.DirectionCount; d++)
                if (p.Directions[d].Count != n1)
                    return false;
            return true;
        }

        /// <summary>Valid frame set for <c>.FR0…FR5</c>: six directions of the same non-zero length.</summary>
        public static Boolean IsValidForFrN(Project p)
        {
            Int32 n0 = p.Directions[0].Count;
            if (n0 == 0)
                return false;
            for (Int32 d = 1; d < Project.DirectionCount; d++)
                if (p.Directions[d].Count != n0)
                    return false;
            return true;
        }

        // ------------------------------------------------------------------ writing

        /// <summary>Auto-cuts all frames, computes per-direction sizes and header offsets (start of 0x407b18).</summary>
        public static FrmLayout Prepare(Project p)
        {
            var layout = new FrmLayout();
            for (Int32 d = 0; d < Project.DirectionCount; d++)
            {
                var dir = p.Directions[d];
                layout.Counts[d] = dir.Count;
                Int32 size = 0;
                foreach (var f in dir.Frames)
                {
                    FrameMath.AutoCut(p, f);
                    size += f.Width * f.Height + FrameHeaderSize;
                }
                layout.DirectionSize[d] = size;
                layout.FrameAreaSize += size;
                if (dir.Count > 0)
                    layout.Shift[d] = FrameMath.GetShift(dir);
            }
            return layout;
        }

        /// <summary>Writes a complete <c>.FRM</c> file (frame set assumed valid).</summary>
        public static void WriteFrm(Project p, FrmLayout layout, Stream s)
        {
            WriteHeaderStart(p, layout.Counts[0], s);
            for (Int32 d = 0; d < Project.DirectionCount; d++)
                WriteS16(s, layout.Shift[d].X);
            for (Int32 d = 0; d < Project.DirectionCount; d++)
                WriteS16(s, layout.Shift[d].Y);
            Int32 running = 0;
            for (Int32 d = 0; d < Project.DirectionCount; d++)
            {
                if (layout.DirectionSize[d] > 0)
                {
                    WriteS32(s, running);
                    running += layout.DirectionSize[d];
                }
                else
                    WriteS32(s, 0);
            }
            WriteS32(s, layout.FrameAreaSize);
            for (Int32 d = 0; d < Project.DirectionCount; d++)
                for (Int32 f = 0; f < p.Directions[d].Count; f++)
                    WriteFrame(p, p.Directions[d], f, s);
        }

        /// <summary>
        /// Writes the <c>.FRd</c> file of one direction. Reproduces a quirk of the original: the "FrameAreaSize" field
        /// contains the size of ALL directions.
        /// </summary>
        public static void WriteFrDirection(Project p, FrmLayout layout, Int32 d, Stream s)
        {
            WriteHeaderStart(p, layout.Counts[0], s);
            WriteS16(s, layout.Shift[d].X);
            for (Int32 i = 0; i < 5; i++)
                WriteS16(s, 0);
            WriteS16(s, layout.Shift[d].Y);
            for (Int32 i = 0; i < 5; i++)
                WriteS16(s, 0);
            for (Int32 i = 0; i < Project.DirectionCount; i++)
                WriteS32(s, 0);
            WriteS32(s, layout.FrameAreaSize);
            for (Int32 f = 0; f < p.Directions[d].Count; f++)
                WriteFrame(p, p.Directions[d], f, s);
        }

        private static void WriteHeaderStart(Project p, Int32 framesPerDirection, Stream s)
        {
            WriteS32(s, Version);
            WriteS16(s, p.Settings.Fps);
            WriteS16(s, p.Settings.ActionFrame);
            WriteS16(s, framesPerDirection);
        }

        /// <summary>Header and pixels of a frame (0x403b90). Auto-cut must have been applied.</summary>
        public static void WriteFrame(Project p, Direction dir, Int32 index, Stream s)
        {
            var f = dir.Frames[index];
            Int32 transparent = FrameMath.TransparentColor(p, f);
            var indexer = new ColorIndexer(p.Palette, p.Settings.DitherMethod, transparent, p.Settings.LegacyColorMatching);

            (Int32 ox, Int32 oy) = index > 0
                ? FrameMath.Relative(dir.Frames[index - 1], f)
                : (dir.FirstFrameOffsetX, dir.FirstFrameOffsetY);

            WriteS16(s, f.Width);
            WriteS16(s, f.Height);
            WriteS32(s, f.Width * f.Height);
            WriteS16(s, ox);
            WriteS16(s, oy);

            var bmp = f.Bitmap;
            var row = new Byte[Math.Max(0, (Int32)f.Width)];
            for (Int32 y = f.Top; y < f.Top + f.Height; y++)
            {
                for (Int32 x = f.Left; x < f.Left + f.Width; x++)
                    row[x - f.Left] = indexer.Index(bmp.Pixels[y * bmp.Width + x]);
                s.Write(row, 0, row.Length);
            }
        }

        private static void WriteS16(Stream s, Int32 v)
        {
            Span<Byte> b = stackalloc Byte[2];
            BinaryPrimitives.WriteInt16BigEndian(b, unchecked((Int16)v));
            s.Write(b);
        }

        private static void WriteS32(Stream s, Int32 v)
        {
            Span<Byte> b = stackalloc Byte[4];
            BinaryPrimitives.WriteInt32BigEndian(b, v);
            s.Write(b);
        }

        // ------------------------------------------------------------------ reading

        /// <summary>Decoded FRM header (the original uses only part of it).</summary>
        public sealed class Header
        {
            public Int32 Version { get; init; }
            public UInt16 Fps { get; init; }
            public UInt16 ActionFrame { get; init; }
            /// <summary>Read as a signed <c>int16</c> (<c>movsx</c> in the original).</summary>
            public Int32 FramesPerDirection { get; init; }
            public Int16[] ShiftX { get; init; } = new Int16[6];
            public Int16[] ShiftY { get; init; } = new Int16[6];
            public Int32[] DataOffset { get; init; } = new Int32[6];
            public Int32 FrameAreaSize { get; init; }
        }

        public static Header ReadHeader(Stream s)
        {
            Int32 version = ReadS32(s);
            return new Header
            {
                Version = version,
                Fps = (UInt16)ReadS16(s),
                ActionFrame = (UInt16)ReadS16(s),
                FramesPerDirection = ReadS16(s),
                ShiftX = ReadShorts(s, 6),
                ShiftY = ReadShorts(s, 6),
                DataOffset = ReadInts(s, 6),
                FrameAreaSize = ReadS32(s),
            };
        }

        /// <summary>Reads a frame (0x403da4) and adds it to <paramref name="dir"/>. <paramref name="relX"/>/<paramref name="relY"/> are the offsets read.</summary>
        public static Frame ReadFrame(Project p, Stream s, Direction dir, out Int32 relX, out Int32 relY)
        {
            Int32 w = ReadS16(s), h = ReadS16(s);
            ReadS32(s); // size, ignored
            relX = ReadS16(s);
            relY = ReadS16(s);
            // an inconsistent size (corrupt file) must not trigger an allocation of several GB
            if (w <= 0 || h <= 0 || (Int64)w * h > MaxFramePixels)
                throw new InvalidDataException("Invalid frame size in FRM.");
            var bmp = new Bitmap32(w, h);
            var buf = new Byte[w * h];
            ReadExactOrRepeat(s, buf);
            var pal = p.Palette.Colors;
            for (Int32 i = 0; i < buf.Length; i++)
                bmp.Pixels[i] = pal[buf[i]];
            var frame = new Frame(bmp);
            dir.Frames.Add(frame);
            FrameMath.AutoCut(p, frame);
            return frame;
        }

        /// <summary>
        /// Reads the requested bytes; if the file is truncated, the original (<c>FileRead</c> without checking) leaves the buffer
        /// unchanged: the last byte read is repeated.
        /// </summary>
        private static void ReadExactOrRepeat(Stream s, Byte[] buf)
        {
            Int32 read = 0;
            while (read < buf.Length)
            {
                Int32 n = s.Read(buf, read, buf.Length - read);
                if (n <= 0)
                    break;
                read += n;
            }
            if (read < buf.Length)
            {
                Byte last = read > 0 ? buf[read - 1] : (Byte)0;
                Array.Fill(buf, last, read, buf.Length - read);
            }
        }

        public static Int16 ReadS16(Stream s)
        {
            Span<Byte> b = stackalloc Byte[2];
            b.Clear();
            s.ReadAtLeast(b, 2, throwOnEndOfStream: false);
            return BinaryPrimitives.ReadInt16BigEndian(b);
        }

        public static Int32 ReadS32(Stream s)
        {
            Span<Byte> b = stackalloc Byte[4];
            b.Clear();
            s.ReadAtLeast(b, 4, throwOnEndOfStream: false);
            return BinaryPrimitives.ReadInt32BigEndian(b);
        }

        private static Int16[] ReadShorts(Stream s, Int32 n)
        {
            var a = new Int16[n];
            for (Int32 i = 0; i < n; i++)
                a[i] = ReadS16(s);
            return a;
        }

        private static Int32[] ReadInts(Stream s, Int32 n)
        {
            var a = new Int32[n];
            for (Int32 i = 0; i < n; i++)
                a[i] = ReadS32(s);
            return a;
        }

        /// <summary>Internal name of a frame coming from an FRM: <c>base + "%02d%04d"</c> (formula from the original, 0x405c90).</summary>
        public static String FrameName(String frmFileName, Int32 direction, Int32 frame)
        {
            String name = Path.GetFileName(frmFileName);
            if (name.Length >= 4)
                name = name[..^4];
            String suffix = (1_000_000 + direction * 10_000 + frame).ToString()[1..];
            return name + suffix;
        }
    }
}