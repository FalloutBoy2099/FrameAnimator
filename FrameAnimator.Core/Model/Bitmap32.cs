namespace FrameAnimator.Core.Model
{
    using System;

    /// <summary>
    /// 32 bits/pixel bitmap without alpha. Each pixel is a <c>0x00RRGGBB</c> (same representation as palette entries
    /// and as the 32 bpp pixels of the original's TBitmap once masked with <c>0xFFFFFF</c>).
    /// </summary>
    public sealed class Bitmap32
    {
        public Int32 Width { get; }
        public Int32 Height { get; }

        /// <summary>Pixels row by row, top to bottom,<c>0x00RRGGBB</c>.</summary>
        public Int32[] Pixels { get; }

        public Bitmap32(Int32 width, Int32 height)
        {
            if (width < 0 || height < 0)
                throw new ArgumentOutOfRangeException(nameof(width));

            Width = width;
            Height = height;
            Pixels = new Int32[checked(width * height)];
        }

        public Bitmap32(Int32 width, Int32 height, Int32[] pixels)
        {
            if (pixels.Length != checked(width * height))
                throw new ArgumentException("Taille de tampon incorrecte.", nameof(pixels));

            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public Int32 this[Int32 x, Int32 y]
        {
            get => Pixels[y * Width + x];
            set => Pixels[y * Width + x] = value & 0xFFFFFF;
        }

        public ReadOnlySpan<Int32> Row(Int32 y) => new(Pixels, y * Width, Width);

        public Bitmap32 Clone() => new(Width, Height, (Int32[])Pixels.Clone());

        public static Int32 Pack(Int32 r, Int32 g, Int32 b) => (r << 16) | (g << 8) | b;
        public static Int32 Red(Int32 rgb) => (rgb >> 16) & 0xFF;
        public static Int32 Green(Int32 rgb) => (rgb >> 8) & 0xFF;
        public static Int32 Blue(Int32 rgb) => rgb & 0xFF;

        /// <summary>Swaps R and B (equivalent of <c>SwapRB</c> at 0x40342c: VCL TColor ↔ 0xRRGGBB).</summary>
        public static Int32 SwapRb(Int32 c) => (Int32)(((UInt32)c & 0xFF000000u) | (UInt32)((c & 0xFF) << 16) | (UInt32)(c & 0xFF00) | (UInt32)((c >> 16) & 0xFF));
    }
}