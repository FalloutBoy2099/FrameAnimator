namespace FrameAnimator.Core.Imaging
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Model;

    /// <summary>Sous‑image d'un GIF : bitmap propre (taille de la sous‑image) et sa position dans l'écran logique.</summary>
    public sealed class GifSubImage(Bitmap32 bitmap, Int32 left, Int32 top, Int32 index)
    {
        public Bitmap32 Bitmap { get; } = bitmap;
        public Int32 Left { get; } = left;
        public Int32 Top { get; } = top;
        public Int32 Width => Bitmap.Width;
        public Int32 Height => Bitmap.Height;
        /// <summary>Rang de la sous‑image dans le fichier (compte aussi les sous‑images vides).</summary>
        public Int32 Index { get; } = index;
    }

    public sealed class GifImage
    {
        public Int32 ScreenWidth { get; init; }
        public Int32 ScreenHeight { get; init; }
        public List<GifSubImage> SubImages { get; } = [];
    }

    /// <summary>
    /// Codec GIF sans dépendance. La lecture renvoie chaque sous‑image <b>sans composition</b> ni disposal (comme
    /// <c>TGIFSubImage.Bitmap</c> de la bibliothèque GIFImage utilisée par l'original) : les pixels « transparents » prennent
    /// la couleur de la palette à l'index transparent. L'écriture produit un GIF87a d'une seule image.
    /// </summary>
    public static class GifCodec
    {
        public static GifImage ReadFile(String path) => Read(File.ReadAllBytes(path));

        public static GifImage Read(Byte[] d)
        {
            if (d.Length < 13 || d[0] != 'G' || d[1] != 'I' || d[2] != 'F')
                throw new ImageFormatException("Invalid GIF signature");
            if (!(d[3] == '8' && (d[4] == '7' || d[4] == '9') && d[5] == 'a'))
                throw new ImageFormatException("Unsupported GIF version");

            Int32 sw = BitConverter.ToUInt16(d, 6), sh = BitConverter.ToUInt16(d, 8);
            var flags = d[10];
            var pos = 13;
            Int32[]? globalPal = null;

            if ((flags & 0x80) != 0)
            {
                var n = 2 << (flags & 7);
                globalPal = ReadPalette(d, ref pos, n);
            }

            var gif = new GifImage { ScreenWidth = sw, ScreenHeight = sh };
            var imageIndex = 0;

            while (pos < d.Length)
            {
                var block = d[pos++];
                if (block == 0x3B)
                    break;                  // trailer
                if (block == 0x21)          // extension
                {
                    pos++;                  // étiquette
                    SkipSubBlocks(d, ref pos);
                    continue;
                }
                if (block != 0x2C)
                    throw new ImageFormatException("Unknown GIF block type");

                Int32 left = BitConverter.ToUInt16(d, pos), top = BitConverter.ToUInt16(d, pos + 2);
                Int32 w = BitConverter.ToUInt16(d, pos + 4), h = BitConverter.ToUInt16(d, pos + 6);

                var f = d[pos + 8];
                pos += 9;

                Int32[]? pal = globalPal;

                if ((f & 0x80) != 0)
                {
                    Int32 n = 2 << (f & 7);
                    pal = ReadPalette(d, ref pos, n);
                }

                var interlaced = (f & 0x40) != 0;
                var minCode = d[pos++];

                var data = ReadSubBlocks(d, ref pos);

                if (w == 0 || h == 0)
                {
                    imageIndex++;
                    continue; // sous‑image « vide » : ignorée par l'original
                }
                if (pal is null)
                    throw new ImageFormatException("No global or local color table defined");

                var indices = LzwDecode(data, minCode, w * h);
                var bmp = new Bitmap32(w, h);

                Int32[] rowOrder = interlaced ? InterlaceOrder(h) : Enumerable.Range(0, h).ToArray();

                for (var i = 0; i < h; i++)
                {
                    var dstRow = rowOrder[i];
                    for (var x = 0; x < w; x++)
                    {
                        var idx = indices[i * w + x];
                        bmp.Pixels[dstRow * w + x] = idx < pal.Length ? pal[idx] : 0;
                    }
                }

                gif.SubImages.Add(new GifSubImage(bmp, left, top, imageIndex));
                imageIndex++;
            }

            return gif;
        }

        private static Int32[] ReadPalette(Byte[] d, ref Int32 pos, Int32 count)
        {
            var pal = new Int32[count];

            for (var i = 0; i < count; i++)
            {
                pal[i] = Bitmap32.Pack(d[pos], d[pos + 1], d[pos + 2]);
                pos += 3;
            }

            return pal;
        }

        private static void SkipSubBlocks(Byte[] d, ref Int32 pos)
        {
            while (pos < d.Length)
            {
                var n = d[pos++];
                if (n == 0)
                    return;
                pos += n;
            }
        }

        private static Byte[] ReadSubBlocks(Byte[] d, ref Int32 pos)
        {
            using var ms = new MemoryStream();

            while (pos < d.Length)
            {
                var n = d[pos++];
                if (n == 0)
                    break;

                ms.Write(d, pos, Math.Min(n, d.Length - pos));
                pos += n;
            }

            return ms.ToArray();
        }

        private static Int32[] InterlaceOrder(Int32 h)
        {
            var order = new Int32[h];
            var i = 0;

            foreach (var (start, step) in new[] { (0, 8), (4, 8), (2, 4), (1, 2) })
                for (Int32 y = start; y < h; y += step)
                    order[i++] = y;

            return order;
        }

        private static Byte[] LzwDecode(Byte[] data, Int32 minCode, Int32 pixelCount)
        {
            if (minCode < 1 || minCode > 11)
                throw new ImageFormatException("Invalid GIF data");
            var output = new Byte[pixelCount];
            Int32 clear = 1 << minCode, eoi = clear + 1;
            Int32 codeSize = minCode + 1, next = eoi + 1;
            var prefix = new Int32[4096];
            var suffix = new Byte[4096];
            var stack = new Byte[4097];
            for (Int32 i = 0; i < clear; i++)
                suffix[i] = (Byte)i;

            Int32 bitPos = 0, outPos = 0, prev = -1;
            Byte first = 0;
            Int32 totalBits = data.Length * 8;
            while (outPos < pixelCount)
            {
                if (bitPos + codeSize > totalBits)
                    break;
                Int32 code = 0;
                for (Int32 b = 0; b < codeSize; b++)
                {
                    Int32 bp = bitPos + b;
                    code |= ((data[bp >> 3] >> (bp & 7)) & 1) << b;
                }
                bitPos += codeSize;

                if (code == clear)
                {
                    codeSize = minCode + 1;
                    next = eoi + 1;
                    prev = -1;
                    continue;
                }
                if (code == eoi)
                    break;

                Int32 sp = 0, cur = code;
                if (prev == -1)
                {
                    if (code >= clear)
                        throw new ImageFormatException("Invalid GIF data");
                    output[outPos++] = suffix[code];
                    prev = code;
                    first = suffix[code];
                    continue;
                }
                if (code >= next)
                {
                    if (code > next)
                        throw new ImageFormatException("Invalid GIF data");
                    stack[sp++] = first;
                    cur = prev;
                }
                while (cur >= clear)
                {
                    stack[sp++] = suffix[cur];
                    cur = prefix[cur];
                }
                first = suffix[cur];
                stack[sp++] = first;
                while (sp > 0 && outPos < pixelCount)
                    output[outPos++] = stack[--sp];

                if (next < 4096)
                {
                    prefix[next] = prev;
                    suffix[next] = first;
                    next++;
                    if (next == (1 << codeSize) && codeSize < 12)
                        codeSize++;
                }
                prev = code;
            }
            return output;
        }

        // ------------------------------------------------------------------ écriture

        /// <summary>
        /// Écrit une image unique. Palette = couleurs distinctes si ≤ 256, sinon chaque pixel est ramené à la couleur la
        /// plus proche de <paramref name="fallback"/> (palette du projet).
        /// </summary>
        public static void Write(Bitmap32 bitmap, Stream stream, Palette? fallback = null)
        {
            var distinct = new Dictionary<Int32, Int32>();
            foreach (Int32 p in bitmap.Pixels)
            {
                if (!distinct.ContainsKey(p))
                {
                    if (distinct.Count == 256)
                    { distinct = null!; break; }
                    distinct.Add(p, distinct.Count);
                }
            }

            Int32[] palette;
            Byte[] indices = new Byte[bitmap.Pixels.Length];
            if (distinct is not null)
            {
                palette = new Int32[distinct.Count];
                foreach (var (c, i) in distinct)
                    palette[i] = c;
                for (Int32 i = 0; i < indices.Length; i++)
                    indices[i] = (Byte)distinct[bitmap.Pixels[i]];
            }
            else
            {
                palette = (fallback ?? Palette.LoadDefault()).Colors.ToArray();
                var indexer = new Geometry.ColorIndexer(fallback ?? Palette.LoadDefault(), DitherMethod.Default, -1);
                for (Int32 i = 0; i < indices.Length; i++)
                    indices[i] = indexer.Index(bitmap.Pixels[i]);
            }

            Int32 bits = 1;
            while ((1 << bits) < palette.Length)
                bits++;
            Int32 tableSize = 1 << bits;

            using var bw = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
            bw.Write("GIF87a"u8.ToArray());
            bw.Write((UInt16)bitmap.Width);
            bw.Write((UInt16)bitmap.Height);
            bw.Write((Byte)(0x80 | ((bits - 1) << 4) | (bits - 1)));
            bw.Write((Byte)0);
            bw.Write((Byte)0);
            for (Int32 i = 0; i < tableSize; i++)
            {
                Int32 c = i < palette.Length ? palette[i] : 0;
                bw.Write((Byte)Bitmap32.Red(c));
                bw.Write((Byte)Bitmap32.Green(c));
                bw.Write((Byte)Bitmap32.Blue(c));
            }
            bw.Write((Byte)0x2C);
            bw.Write((UInt16)0);
            bw.Write((UInt16)0);
            bw.Write((UInt16)bitmap.Width);
            bw.Write((UInt16)bitmap.Height);
            bw.Write((Byte)0);
            Int32 minCode = Math.Max(2, bits);
            bw.Write((Byte)minCode);
            var packed = LzwEncode(indices, minCode);
            for (Int32 i = 0; i < packed.Length; i += 255)
            {
                Int32 n = Math.Min(255, packed.Length - i);
                bw.Write((Byte)n);
                bw.Write(packed, i, n);
            }
            bw.Write((Byte)0);
            bw.Write((Byte)0x3B);
        }

        public static void WriteFile(Bitmap32 bitmap, String path, Palette? fallback = null)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            Write(bitmap, fs, fallback);
        }

        private static Byte[] LzwEncode(Byte[] indices, Int32 minCode)
        {
            Int32 clear = 1 << minCode, eoi = clear + 1;
            Int32 codeSize = minCode + 1, next = eoi + 1;
            var dict = new Dictionary<Int32, Int32>();
            using var ms = new MemoryStream();
            Int32 acc = 0, accBits = 0;

            void Emit(Int32 code)
            {
                acc |= code << accBits;
                accBits += codeSize;
                while (accBits >= 8)
                {
                    ms.WriteByte((Byte)(acc & 0xFF));
                    acc >>= 8;
                    accBits -= 8;
                }
            }

            Emit(clear);
            if (indices.Length == 0)
            { Emit(eoi); }
            else
            {
                Int32 prefix = indices[0];
                for (Int32 i = 1; i < indices.Length; i++)
                {
                    Int32 k = indices[i];
                    Int32 key = (prefix << 8) | k;
                    if (dict.TryGetValue(key, out Int32 code))
                    { prefix = code; continue; }
                    Emit(prefix);
                    if (next < 4096)
                    {
                        dict[key] = next++;
                        if (next - 1 == (1 << codeSize) && codeSize < 12)
                            codeSize++;
                    }
                    else
                    {
                        Emit(clear);
                        dict.Clear();
                        codeSize = minCode + 1;
                        next = eoi + 1;
                    }
                    prefix = k;
                }
                Emit(prefix);
                Emit(eoi);
            }
            if (accBits > 0)
                ms.WriteByte((Byte)(acc & 0xFF));
            return ms.ToArray();
        }
    }
}