namespace FrameAnimator.Core.Imaging
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using System.IO.Compression;
    using Model;

    /// <summary>
    /// Codec PNG sans dépendance (zlib via <see cref="ZLibStream"/>). Lecture : tous les types de couleur, profondeurs 1–16,
    /// entrelacement Adam7. <b>Le canal alpha et tRNS sont ignorés</b> : l'original (<c>TPNGObject.Assign</c> vers un
    /// <c>TBitmap</c>) conserve les composantes RVB brutes (vérifié par comparaison d'octets, voir spécification §4.4).
    /// Écriture : RGB 8 bits, comme <c>TPNGObject.Assign(bitmap 24 bits)</c>.
    /// </summary>
    public static class PngCodec
    {
        private static readonly Byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        public static Bitmap32 ReadFile(String path) => Read(File.ReadAllBytes(path));

        public static Bitmap32 Read(Byte[] data)
        {
            if (data.Length < 8 || !data.AsSpan(0, 8).SequenceEqual(Signature))
                throw new ImageFormatException("The file being readed is not a valid \"Portable Network Graphics\" image because it contains an invalid header.");

            Int32 pos = 8;
            Int32 width = 0, height = 0, bitDepth = 0, colorType = 0, interlace = 0;
            Byte[]? plte = null, trns = null;
            using var idat = new MemoryStream();
            Boolean sawHeader = false;

            while (pos + 12 <= data.Length)
            {
                UInt32 len = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pos));
                String type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
                if (pos + 12L + len > data.Length)
                    throw new ImageFormatException("This \"Portable Network Graphics\" image is invalid because the decoder found an unexpected end of the file.");
                var body = data.AsSpan(pos + 8, (Int32)len);
                UInt32 crc = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pos + 8 + (Int32)len));
                if (Crc32.Compute(data.AsSpan(pos + 4, 4 + (Int32)len)) != crc)
                    throw new ImageFormatException("This \"Portable Network Graphics\" image is not valid because it contains invalid pieces of data (crc error)");
                pos += 12 + (Int32)len;

                switch (type)
                {
                    case "IHDR":
                        if (len < 13)
                            throw new ImageFormatException("IHDR corrupted.");
                        width = (Int32)BinaryPrimitives.ReadUInt32BigEndian(body);
                        height = (Int32)BinaryPrimitives.ReadUInt32BigEndian(body[4..]);
                        bitDepth = body[8];
                        colorType = body[9];
                        interlace = body[12];
                        sawHeader = true;
                        break;
                    case "PLTE":
                        plte = body.ToArray();
                        break;
                    case "tRNS":
                        trns = body.ToArray();
                        break;
                    case "IDAT":
                        idat.Write(body);
                        break;
                }
                if (type == "IEND")
                    break;
            }
            if (!sawHeader)
                throw new ImageFormatException("IHDR chunk is not the first.");
            if (width <= 0 || height <= 0 || width > 65535 || height > 65535)
                throw new ImageFormatException("Invalid PNG size.");
            if (idat.Length == 0)
                throw new ImageFormatException("This \"Portable Network Graphics\" image contains no data.");

            Int32 channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new ImageFormatException("Unsupported PNG color type.") };
            if (colorType == 3 && plte is null)
                throw new ImageFormatException("This \"Portable Network Graphics\" could not be loaded because it uses a color table which is missing.");
            Int32 bitsPerPixel = channels * bitDepth;

            idat.Position = 0;
            Byte[] raw;
            try
            {
                using var z = new ZLibStream(idat, CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                z.CopyTo(outMs);
                raw = outMs.ToArray();
            }
            catch (InvalidDataException e)
            {
                throw new ImageFormatException("Could not decompress the image because it contains invalid compressed data. " + e.Message);
            }

            var bmp = new Bitmap32(width, height);
            Int32 rawPos = 0;

            void DecodePass(Int32 x0, Int32 y0, Int32 dx, Int32 dy, Int32 pw, Int32 ph)
            {
                if (pw == 0 || ph == 0)
                    return;
                Int32 rowBytes = (pw * bitsPerPixel + 7) / 8;
                Int32 bpp = Math.Max(1, bitsPerPixel / 8);
                var prev = new Byte[rowBytes];
                var cur = new Byte[rowBytes];
                for (Int32 row = 0; row < ph; row++)
                {
                    if (rawPos + 1 + rowBytes > raw.Length)
                        throw new ImageFormatException("Premature end of PNG data.");
                    Int32 filter = raw[rawPos++];
                    Buffer.BlockCopy(raw, rawPos, cur, 0, rowBytes);
                    rawPos += rowBytes;
                    Unfilter(filter, cur, prev, bpp);
                    for (Int32 col = 0; col < pw; col++)
                    {
                        Int32 rgb = Pixel(cur, col, colorType, bitDepth, plte, trns);
                        bmp.Pixels[(y0 + row * dy) * width + x0 + col * dx] = rgb;
                    }
                    (prev, cur) = (cur, prev);
                }
            }

            if (interlace == 0)
                DecodePass(0, 0, 1, 1, width, height);
            else
            {
                ReadOnlySpan<(Int32 X, Int32 Y, Int32 Dx, Int32 Dy)> passes = [(0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)];
                foreach (var (px, py, dx, dy) in passes)
                    DecodePass(px, py, dx, dy, (width - px + dx - 1) / dx, (height - py + dy - 1) / dy);
            }
            return bmp;
        }

        private static void Unfilter(Int32 filter, Byte[] cur, Byte[] prev, Int32 bpp)
        {
            switch (filter)
            {
                case 0:
                    break;
                case 1:
                    for (Int32 i = bpp; i < cur.Length; i++)
                        cur[i] = (Byte)(cur[i] + cur[i - bpp]);
                    break;
                case 2:
                    for (Int32 i = 0; i < cur.Length; i++)
                        cur[i] = (Byte)(cur[i] + prev[i]);
                    break;
                case 3:
                    for (Int32 i = 0; i < cur.Length; i++)
                    {
                        Int32 left = i >= bpp ? cur[i - bpp] : 0;
                        cur[i] = (Byte)(cur[i] + ((left + prev[i]) >> 1));
                    }
                    break;
                case 4:
                    for (Int32 i = 0; i < cur.Length; i++)
                    {
                        Int32 a = i >= bpp ? cur[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
                        Int32 p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                        Int32 pred = pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
                        cur[i] = (Byte)(cur[i] + pred);
                    }
                    break;
                default:
                    throw new ImageFormatException("Unknown PNG filter.");
            }
        }

        private static Int32 Sample(Byte[] row, Int32 index, Int32 depth)
        {
            switch (depth)
            {
                case 8:
                    return row[index];
                case 16:
                    return (row[2 * index] << 8) | row[2 * index + 1];
                default:
                    Int32 bit = index * depth;
                    Int32 shift = 8 - depth - (bit & 7);
                    return (row[bit >> 3] >> shift) & ((1 << depth) - 1);
            }
        }

        private static Int32 To8(Int32 v, Int32 depth) => depth switch { 8 => v, 16 => v >> 8, _ => v * 255 / ((1 << depth) - 1) };

        private static Int32 Pixel(Byte[] row, Int32 col, Int32 colorType, Int32 depth, Byte[]? plte, Byte[]? trns)
        {
            Int32 r, g, b, a = 255;
            switch (colorType)
            {
                case 0:
                    {
                        Int32 v = Sample(row, col, depth);
                        r = g = b = To8(v, depth);
                        if (trns is { Length: >= 2 } && v == ((trns[0] << 8) | trns[1]))
                            a = 0;
                        break;
                    }
                case 2:
                    {
                        Int32 rr = Sample(row, 3 * col, depth), gg = Sample(row, 3 * col + 1, depth), bb = Sample(row, 3 * col + 2, depth);
                        r = To8(rr, depth);
                        g = To8(gg, depth);
                        b = To8(bb, depth);
                        if (trns is { Length: >= 6 } && rr == ((trns[0] << 8) | trns[1]) && gg == ((trns[2] << 8) | trns[3]) && bb == ((trns[4] << 8) | trns[5]))
                            a = 0;
                        break;
                    }
                case 3:
                    {
                        Int32 i = Sample(row, col, depth);
                        if (i * 3 + 2 >= plte!.Length)
                            throw new ImageFormatException("The \"Portable Network Graphics\" image contains an invalid palette.");
                        r = plte[3 * i];
                        g = plte[3 * i + 1];
                        b = plte[3 * i + 2];
                        if (trns is not null && i < trns.Length)
                            a = trns[i];
                        break;
                    }
                case 4:
                    {
                        r = g = b = To8(Sample(row, 2 * col, depth), depth);
                        a = To8(Sample(row, 2 * col + 1, depth), depth);
                        break;
                    }
                default:
                    {
                        r = To8(Sample(row, 4 * col, depth), depth);
                        g = To8(Sample(row, 4 * col + 1, depth), depth);
                        b = To8(Sample(row, 4 * col + 2, depth), depth);
                        a = To8(Sample(row, 4 * col + 3, depth), depth);
                        break;
                    }
            }
            // Comportement vérifié sur l'original : le canal alpha (et tRNS) est ignoré, les composantes RVB brutes sont conservées.
            _ = a;
            return Bitmap32.Pack(r, g, b);
        }

        public static void WriteFile(Bitmap32 bitmap, String path)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            Write(bitmap, fs);
        }

        public static void Write(Bitmap32 bitmap, Stream stream)
        {
            Int32 w = bitmap.Width, h = bitmap.Height;
            var scan = new Byte[h * (1 + 3 * w)];
            Int32 o = 0;
            for (Int32 y = 0; y < h; y++)
            {
                scan[o++] = 0;
                for (Int32 x = 0; x < w; x++)
                {
                    Int32 p = bitmap.Pixels[y * w + x];
                    scan[o++] = (Byte)Bitmap32.Red(p);
                    scan[o++] = (Byte)Bitmap32.Green(p);
                    scan[o++] = (Byte)Bitmap32.Blue(p);
                }
            }
            using var comp = new MemoryStream();
            using (var z = new ZLibStream(comp, CompressionLevel.Optimal, leaveOpen: true))
                z.Write(scan);

            stream.Write(Signature);
            var ihdr = new Byte[13];
            BinaryPrimitives.WriteUInt32BigEndian(ihdr, (UInt32)w);
            BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), (UInt32)h);
            ihdr[8] = 8;  // profondeur
            ihdr[9] = 2;  // RGB
            WriteChunk(stream, "IHDR", ihdr);
            WriteChunk(stream, "IDAT", comp.ToArray());
            WriteChunk(stream, "IEND", []);
        }

        private static void WriteChunk(Stream s, String type, Byte[] body)
        {
            Span<Byte> len = stackalloc Byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(len, (UInt32)body.Length);
            s.Write(len);
            var tb = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(tb);
            s.Write(body);
            var crcInput = new Byte[4 + body.Length];
            tb.CopyTo(crcInput, 0);
            body.CopyTo(crcInput, 4);
            BinaryPrimitives.WriteUInt32BigEndian(len, Crc32.Compute(crcInput));
            s.Write(len);
        }

        internal static class Crc32
        {
            private static readonly UInt32[] Table = BuildTable();

            private static UInt32[] BuildTable()
            {
                var t = new UInt32[256];
                for (UInt32 n = 0; n < 256; n++)
                {
                    UInt32 c = n;
                    for (Int32 k = 0; k < 8; k++)
                        c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    t[n] = c;
                }
                return t;
            }

            public static UInt32 Compute(ReadOnlySpan<Byte> data)
            {
                UInt32 c = 0xFFFFFFFFu;
                foreach (Byte b in data)
                    c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
                return c ^ 0xFFFFFFFFu;
            }
        }
    }
}