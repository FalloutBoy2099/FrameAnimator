namespace FrameAnimator.Core.Tests
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>Crafted (valid or hostile) BMP / PNG / GIF / FRM byte streams for the limit and fuzz tests.</summary>
    internal static class CraftedFiles
    {
        public static Byte[] Bmp(Int32 width, Int32 height, Int32 bpp = 24, Int32 compression = 0, Int32 headerSize = 40, Int32 colorsUsed = 0, Int32 pixelOffset = 54, Int32 payload = 64)
        {
            var d = new Byte[Math.Max(54, 14 + Math.Min(headerSize, 200)) + payload];
            d[0] = (Byte)'B';
            d[1] = (Byte)'M';
            BitConverter.GetBytes(pixelOffset).CopyTo(d, 10);
            BitConverter.GetBytes(headerSize).CopyTo(d, 14);
            BitConverter.GetBytes(width).CopyTo(d, 18);
            BitConverter.GetBytes(height).CopyTo(d, 22);
            BitConverter.GetBytes((Int16)1).CopyTo(d, 26);
            BitConverter.GetBytes((Int16)bpp).CopyTo(d, 28);
            BitConverter.GetBytes(compression).CopyTo(d, 30);
            BitConverter.GetBytes(colorsUsed).CopyTo(d, 46);
            return d;
        }

        private static UInt32[]? _crcTable;

        internal static UInt32 Crc(ReadOnlySpan<Byte> data)
        {
            if (_crcTable is null)
            {
                var t = new UInt32[256];
                for (UInt32 n = 0; n < 256; n++)
                {
                    UInt32 c = n;
                    for (Int32 k = 0; k < 8; k++)
                        c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    t[n] = c;
                }
                _crcTable = t;
            }
            UInt32 crc = 0xFFFFFFFFu;
            foreach (Byte b in data)
                crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }

        private static void Chunk(Stream s, String type, ReadOnlySpan<Byte> body)
        {
            Span<Byte> four = stackalloc Byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(four, (UInt32)body.Length);
            s.Write(four);
            var tb = Encoding.ASCII.GetBytes(type);
            var crcInput = new Byte[4 + body.Length];
            tb.CopyTo(crcInput, 0);
            body.CopyTo(crcInput.AsSpan(4));
            s.Write(tb);
            s.Write(body);
            BinaryPrimitives.WriteUInt32BigEndian(four, Crc(crcInput));
            s.Write(four);
        }

        /// <summary>Zlib stream containing <paramref name="uncompressedLength"/> zero bytes (compresses to almost nothing).</summary>
        public static Byte[] ZlibZeros(Int64 uncompressedLength)
        {
            using var ms = new MemoryStream();
            using (var z = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                var zeros = new Byte[65536];
                for (Int64 left = uncompressedLength; left > 0; left -= zeros.Length)
                    z.Write(zeros, 0, (Int32)Math.Min(zeros.Length, left));
            }
            return ms.ToArray();
        }

        /// <summary>PNG with the given raw IHDR values (not validated) and the given zlib stream as the single IDAT.</summary>
        public static Byte[] Png(UInt32 width, UInt32 height, Byte bitDepth, Byte colorType, Byte[] zlibData, Byte interlace = 0, Boolean plte = false)
        {
            using var ms = new MemoryStream();
            ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
            var ihdr = new Byte[13];
            BinaryPrimitives.WriteUInt32BigEndian(ihdr, width);
            BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), height);
            ihdr[8] = bitDepth;
            ihdr[9] = colorType;
            ihdr[12] = interlace;
            Chunk(ms, "IHDR", ihdr);
            if (plte)
                Chunk(ms, "PLTE", new Byte[768]);
            Chunk(ms, "IDAT", zlibData);
            Chunk(ms, "IEND", []);
            return ms.ToArray();
        }

        /// <summary>GIF with a 2-color global palette followed by the given image descriptors (each: left, top, width, height, no LZW data).</summary>
        public static Byte[] Gif(UInt16 screenWidth, UInt16 screenHeight, params (UInt16 W, UInt16 H)[] images)
        {
            using var ms = new MemoryStream();
            ms.Write("GIF89a"u8);
            ms.Write(BitConverter.GetBytes(screenWidth));
            ms.Write(BitConverter.GetBytes(screenHeight));
            ms.Write([0x80, 0, 0]);
            ms.Write([0, 0, 0, 255, 255, 255]);
            foreach (var (w, h) in images)
            {
                ms.WriteByte(0x2C);
                ms.Write([0, 0, 0, 0]);
                ms.Write(BitConverter.GetBytes(w));
                ms.Write(BitConverter.GetBytes(h));
                ms.WriteByte(0);   // no local table, not interlaced
                ms.WriteByte(2);   // LZW minimum code size
                ms.WriteByte(0);   // no data sub-block
            }
            ms.WriteByte(0x3B);
            return ms.ToArray();
        }

        /// <summary>Minimal FRM: 62-byte header declaring one frame in direction 0, then that frame header with the given size and few pixels.</summary>
        public static Byte[] Frm(Int16 frameWidth, Int16 frameHeight, Int32 pixelBytes = 16)
        {
            using var ms = new MemoryStream();
            var b = new Byte[4];
            void S32(Int32 v)
            {
                BinaryPrimitives.WriteInt32BigEndian(b, v);
                ms.Write(b, 0, 4);
            }
            void S16(Int16 v)
            {
                BinaryPrimitives.WriteInt16BigEndian(b, v);
                ms.Write(b, 0, 2);
            }
            S32(4);          // version
            S16(10);         // fps
            S16(0);          // action frame
            S16(1);          // frames per direction
            for (Int32 i = 0; i < 12; i++)
                S16(0);      // shifts
            S32(0);          // data offset of direction 0
            for (Int32 i = 1; i < 6; i++)
                S32(0);
            S32(0);          // frame area size
            S16(frameWidth);
            S16(frameHeight);
            S32(frameWidth * frameHeight);
            S16(0);
            S16(0);
            ms.Write(new Byte[pixelBytes]);
            return ms.ToArray();
        }
    }

    /// <summary>Deterministic mutation engine and bounded-time runner shared by the fuzz tests.</summary>
    internal static class FuzzEngine
    {
        public const String IterationsVariable = "FA_FUZZ_ITERATIONS";

        /// <summary>Iterations per fuzz target: <c>FA_FUZZ_ITERATIONS</c> if set to a positive integer, otherwise <paramref name="defaultValue"/>.</summary>
        public static Int32 Iterations(Int32 defaultValue)
            => Int32.TryParse(Environment.GetEnvironmentVariable(IterationsVariable), out Int32 n) && n > 0 ? n : defaultValue;

        private static readonly Int32[] Interesting = [0, 1, -1, 2, 255, 256, 4096, 65535, 65536, 0x7FFF, 0x7FFFFFFF, Int32.MinValue, 0x00FFFFFF];

        /// <summary>Returns a mutated copy of <paramref name="seed"/> (seed bytes are never modified).</summary>
        public static Byte[] Mutate(Byte[] seed, Random rnd, out String description)
        {
            var d = (Byte[])seed.Clone();
            if (d.Length == 0)
            {
                description = "empty";
                return d;
            }
            Int32 headerLen = Math.Min(d.Length, 80);
            switch (rnd.Next(7))
            {
                case 0:
                    {
                        Int32 n = 1 + rnd.Next(8);
                        for (Int32 i = 0; i < n; i++)
                            d[rnd.Next(d.Length)] = (Byte)rnd.Next(256);
                        description = $"random byte flips x{n}";
                        return d;
                    }
                case 1:
                    {
                        Int32 n = 1 + rnd.Next(4);
                        for (Int32 i = 0; i < n; i++)
                            d[rnd.Next(headerLen)] ^= (Byte)(1 << rnd.Next(8));
                        description = $"header bit flips x{n}";
                        return d;
                    }
                case 2:
                    {
                        Int32 n = rnd.Next(d.Length);
                        description = $"truncated to {n}";
                        return d[..n];
                    }
                case 3:
                    {
                        Int32 n = 1 + rnd.Next(200);
                        var r = new Byte[n];
                        rnd.NextBytes(r);
                        Int32 keep = Math.Min(Math.Min(4, d.Length), n);
                        Array.Copy(d, r, keep);   // keep the magic number so that the decoder gets past the signature check
                        description = $"random bytes x{n}";
                        return r;
                    }
                case 4:
                    {
                        Int32 pos = rnd.Next(headerLen);
                        Int32 value = Interesting[rnd.Next(Interesting.Length)];
                        Span<Byte> four = stackalloc Byte[4];
                        if (rnd.Next(2) == 0)
                            BinaryPrimitives.WriteInt32LittleEndian(four, value);
                        else
                            BinaryPrimitives.WriteInt32BigEndian(four, value);
                        for (Int32 i = 0; i < 4 && pos + i < d.Length; i++)
                            d[pos + i] = four[i];
                        description = $"int {value} at {pos}";
                        return d;
                    }
                case 5:
                    {
                        Int32 n = rnd.Next(1, d.Length + 1);
                        Int32 pos = rnd.Next(headerLen);
                        d[pos] = (Byte)rnd.Next(256);
                        description = $"truncated to {n} + byte at {pos}";
                        return d[..n];
                    }
                default:
                    {
                        Int32 pos = rnd.Next(d.Length);
                        Int32 len = Math.Min(d.Length - pos, 1 + rnd.Next(32));
                        Array.Fill(d, rnd.Next(2) == 0 ? (Byte)0 : (Byte)0xFF, pos, len);
                        description = $"fill {len} at {pos}";
                        return d;
                    }
            }
        }

        private static readonly String[] InterestingTokens = ["0", "-1", "1", "2147483647", "-2147483648", "99999999999", "65535", "0x7FFFFFFF", "$FFFFFFFF", "", "abc", "*", "?", "999999", "-999999"];

        /// <summary>Returns a mutated copy of one line of text (script / project file).</summary>
        public static String MutateText(String line, Random rnd)
        {
            var sb = new StringBuilder(line);
            Int32 edits = 1 + rnd.Next(3);
            for (Int32 e = 0; e < edits; e++)
            {
                switch (rnd.Next(7))
                {
                    case 0 when sb.Length > 0:
                        sb[rnd.Next(sb.Length)] = (Char)rnd.Next(32, 127);
                        break;
                    case 1 when sb.Length > 0:
                        sb.Remove(rnd.Next(sb.Length), 1);
                        break;
                    case 2:
                        sb.Insert(rnd.Next(sb.Length + 1), (Char)rnd.Next(32, 127));
                        break;
                    case 3:
                        sb.Insert(rnd.Next(sb.Length + 1), ',');
                        break;
                    case 4 when sb.Length > 0:
                        sb.Length = rnd.Next(sb.Length);
                        break;
                    case 5:
                        sb.Insert(rnd.Next(sb.Length + 1), InterestingTokens[rnd.Next(InterestingTokens.Length)]);
                        break;
                    default:
                        {
                            // replace one comma-separated field by an interesting token
                            String[] parts = sb.ToString().Split(',');
                            parts[rnd.Next(parts.Length)] = InterestingTokens[rnd.Next(InterestingTokens.Length)];
                            sb.Clear().Append(String.Join(",", parts));
                            break;
                        }
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Runs <paramref name="action"/> on a pool thread and fails the test if it does not finish within <paramref name="timeout"/>.
        /// Returns the exception thrown by the action, or <c>null</c>.
        /// </summary>
        public static Exception? RunBounded(String label, Action action, TimeSpan? timeout = null)
        {
            var task = Task.Run(action);
            try
            {
                if (!task.Wait(timeout ?? TimeSpan.FromSeconds(8)))
                    Assert.Fail($"Timeout (possible hang or runaway allocation): {label}");
            }
            catch (AggregateException e)
            {
                return e.GetBaseException();
            }
            return null;
        }

        /// <summary>Fails with the full details unless <paramref name="e"/> is <c>null</c> or one of <paramref name="allowed"/>.</summary>
        public static void AssertAllowed(Exception? e, String label, params Type[] allowed)
        {
            if (e is null)
                return;
            foreach (Type t in allowed)
                if (t.IsInstanceOfType(e))
                    return;
            Assert.Fail($"Unexpected {e.GetType().FullName} for {label}: {e.Message}\n{e.StackTrace}");
        }
    }
}
