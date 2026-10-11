namespace FrameAnimator.Core.Imaging
{
    using System;
    using System.IO;
    using Model;

    public sealed class ImageFormatException(String message) : Exception(message);

    /// <summary>
    /// Lecture (1/4/8/16/24/32 bits, RLE4/RLE8, bitfields, haut‑bas ou bas‑haut, en‑têtes OS/2, V3, V4, V5) et écriture
    /// (24 bits, comme l'original qui force <c>pf24bit</c> avant <c>SaveToFile</c>) de fichiers BMP.
    /// </summary>
    public static class BmpCodec
    {
        public static Bitmap32 Read(Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return Read(ms.ToArray());
        }

        public static Bitmap32 ReadFile(String path) => Read(File.ReadAllBytes(path));

        public static Bitmap32 Read(Byte[] d)
        {
            if (d.Length < 26 || d[0] != (Byte)'B' || d[1] != (Byte)'M')
                throw new ImageFormatException("Not a BMP file.");

            var pixelOffset = BitConverter.ToInt32(d, 10);
            var headerSize = BitConverter.ToInt32(d, 14);

            Int32 width, height, bpp, compression = 0, colorsUsed = 0;
            var os2 = headerSize == 12;

            if (os2)
            {
                width = BitConverter.ToUInt16(d, 18);
                height = BitConverter.ToUInt16(d, 20);
                bpp = BitConverter.ToUInt16(d, 24);
            }
            else
            {
                if (headerSize < 40 || d.Length < 14 + headerSize)
                    throw new ImageFormatException("Unsupported BMP header.");
                width = BitConverter.ToInt32(d, 18);
                height = BitConverter.ToInt32(d, 22);
                bpp = BitConverter.ToUInt16(d, 28);
                compression = BitConverter.ToInt32(d, 30);
                colorsUsed = BitConverter.ToInt32(d, 46);
            }

            var topDown = height < 0;
            if (topDown)
                height = -height;

            if (width <= 0 || height <= 0 || width > 65535 || height > 65535)
                throw new ImageFormatException("Invalid BMP dimensions.");

            var paletteOffset = 14 + headerSize;
            Int32[]? pal = null;
            UInt32 rMask = 0, gMask = 0, bMask = 0;

            if (bpp <= 8)
            {
                var count = colorsUsed > 0 ? colorsUsed : 1 << bpp;
                if (count > 256)
                    count = 256;
                var entry = os2 ? 3 : 4;
                pal = new Int32[256];
                for (var i = 0; i < count && paletteOffset + (i + 1) * entry <= d.Length; i++)
                {
                    var o = paletteOffset + i * entry;
                    pal[i] = Bitmap32.Pack(d[o + 2], d[o + 1], d[o]);
                }
            }
            if ((compression == 3 || compression == 6) && d.Length >= 66)
            {
                // Les masques RVB suivent l'en-tête de 40 octets (ou sont inclus dans les en-têtes V4/V5, à la même position).
                rMask = BitConverter.ToUInt32(d, 54);
                gMask = BitConverter.ToUInt32(d, 58);
                bMask = BitConverter.ToUInt32(d, 62);
            }

            var bmp = new Bitmap32(width, height);
            Int32 rowBytes = (bpp * width + 31) / 32 * 4;

            void Put(Int32 x, Int32 yFile, Int32 rgb)
            {
                Int32 y = topDown ? yFile : height - 1 - yFile;
                bmp.Pixels[y * width + x] = rgb & 0xFFFFFF;
            }

            if (compression == 1 || compression == 2) // RLE8 / RLE4
            {
                DecodeRle(d, pixelOffset, width, height, compression == 1, pal ?? throw new ImageFormatException("RLE sans palette."), Put);
                return bmp;
            }

            for (var yf = 0; yf < height; yf++)
            {
                var rowStart = pixelOffset + yf * rowBytes;
                if (rowStart >= d.Length)
                    break;
                for (var x = 0; x < width; x++)
                {
                    Int32 rgb;
                    switch (bpp)
                    {
                        case 1:
                            rgb = pal![(d[rowStart + (x >> 3)] >> (7 - (x & 7))) & 1];
                            break;
                        case 2:
                            rgb = pal![(d[rowStart + (x >> 2)] >> (6 - 2 * (x & 3))) & 3];
                            break;
                        case 4:
                            rgb = pal![(d[rowStart + (x >> 1)] >> ((x & 1) == 0 ? 4 : 0)) & 0xF];
                            break;
                        case 8:
                            rgb = pal![d[rowStart + x]];
                            break;
                        case 16:
                            {
                                Int32 v = BitConverter.ToUInt16(d, rowStart + 2 * x);
                                if (compression == 3 && (rMask | gMask | bMask) != 0)
                                    rgb = Bitmap32.Pack(Expand((UInt32)v, rMask), Expand((UInt32)v, gMask), Expand((UInt32)v, bMask));
                                else
                                    rgb = Bitmap32.Pack(Scale5((v >> 10) & 31), Scale5((v >> 5) & 31), Scale5(v & 31));
                                break;
                            }
                        case 24:
                            {
                                Int32 o = rowStart + 3 * x;
                                rgb = Bitmap32.Pack(d[o + 2], d[o + 1], d[o]);
                                break;
                            }
                        case 32:
                            {
                                Int32 o = rowStart + 4 * x;
                                if (compression == 3 && (rMask | gMask | bMask) != 0)
                                {
                                    UInt32 v = BitConverter.ToUInt32(d, o);
                                    rgb = Bitmap32.Pack(Expand(v, rMask), Expand(v, gMask), Expand(v, bMask));
                                }
                                else
                                    rgb = Bitmap32.Pack(d[o + 2], d[o + 1], d[o]);
                                break;
                            }
                        default:
                            throw new ImageFormatException("Unsupported bits per pixel.");
                    }
                    Put(x, yf, rgb);
                }
            }
            return bmp;
        }

        private static Int32 Scale5(Int32 v) => (v << 3) | (v >> 2);

        private static Int32 Expand(UInt32 value, UInt32 mask)
        {
            if (mask == 0)
                return 0;
            var shift = System.Numerics.BitOperations.TrailingZeroCount(mask);
            var bits = System.Numerics.BitOperations.PopCount(mask >> shift);
            var v = (value & mask) >> shift;
            if (bits >= 8)
                return (Int32)(v >> (bits - 8));
            return (Int32)(v * 255 / ((1u << bits) - 1));
        }

        private static void DecodeRle(Byte[] d, Int32 offset, Int32 width, Int32 height, Boolean rle8, Int32[] pal, Action<Int32, Int32, Int32> put)
        {
            Int32 x = 0, y = 0, i = offset;
            while (i + 1 < d.Length && y < height)
            {
                Int32 n = d[i++], c = d[i++];
                if (n > 0)
                {
                    for (var k = 0; k < n; k++)
                    {
                        var idx = rle8 ? c : ((k & 1) == 0 ? c >> 4 : c & 0xF);
                        if (x < width)
                            put(x, y, pal[idx]);
                        x++;
                    }
                    continue;
                }
                switch (c)
                {
                    case 0:
                        x = 0;
                        y++;
                        break;
                    case 1:
                        return;
                    case 2:
                        if (i + 1 >= d.Length)
                            return;
                        x += d[i++];
                        y += d[i++];
                        break;
                    default:
                        Int32 count = c;
                        for (var k = 0; k < count; k++)
                        {
                            Int32 idx;
                            if (rle8)
                                idx = d[i + k];
                            else
                            {
                                var b = d[i + k / 2];
                                idx = (k & 1) == 0 ? b >> 4 : b & 0xF;
                            }
                            if (x < width)
                                put(x, y, pal[idx]);
                            x++;
                        }
                        var used = rle8 ? count : (count + 1) / 2;
                        i += used + (used & 1);
                        break;
                }
            }
        }

        public static void Write(Bitmap32 bitmap, Stream stream)
        {
            Int32 w = bitmap.Width, h = bitmap.Height;
            var rowBytes = (24 * w + 31) / 32 * 4;
            var imageSize = rowBytes * h;

            using var bw = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);

            bw.Write((Byte)'B');
            bw.Write((Byte)'M');
            bw.Write(54 + imageSize);
            bw.Write(0);
            bw.Write(54);
            bw.Write(40);
            bw.Write(w);
            bw.Write(h);
            bw.Write((Int16)1);
            bw.Write((Int16)24);
            bw.Write(0);
            bw.Write(imageSize);
            bw.Write(0); // biXPelsPerMeter (0 comme TBitmap.SaveToFile de la VCL)
            bw.Write(0); // biYPelsPerMeter
            bw.Write(0);
            bw.Write(0);

            var row = new Byte[rowBytes];

            for (var y = h - 1; y >= 0; y--)
            {
                Array.Clear(row);

                for (var x = 0; x < w; x++)
                {
                    var p = bitmap.Pixels[y * w + x];

                    row[3 * x] = (Byte)Bitmap32.Blue(p);
                    row[3 * x + 1] = (Byte)Bitmap32.Green(p);
                    row[3 * x + 2] = (Byte)Bitmap32.Red(p);
                }

                bw.Write(row);
            }
        }

        public static void WriteFile(Bitmap32 bitmap, String path)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            Write(bitmap, fs);
        }
    }
}