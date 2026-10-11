namespace FrameAnimator.App.Infrastructure
{
    using System;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using FrameAnimator.Core.Model;

    /// <summary>Conversions entre <see cref="Bitmap32"/> (0x00RRGGBB) et sources WPF.</summary>
    public static class ImageConversion
    {
        /// <summary>
        /// Convertit un bitmap en <see cref="BitmapSource"/> BGRA. Les pixels dont la couleur (24 bits, après OU avec
        /// <paramref name="orMask"/>) vaut <paramref name="transparent"/> sont transparents ; <paramref name="transparent"/> &lt; 0 : opaque.
        /// </summary>
        public static BitmapSource ToBitmapSource(Bitmap32 bmp, Int32 transparent, Int32 orMask = 0)
        {
            var width = Math.Max(1, bmp.Width);
            var height = Math.Max(1, bmp.Height);
            var data = new Byte[width * height * 4];
            var key = transparent < 0 ? -1 : (transparent | orMask) & 0xFFFFFF;
            for (var i = 0; i < bmp.Pixels.Length; i++)
            {
                var c = (bmp.Pixels[i] & 0xFFFFFF) | orMask;
                var o = i * 4;
                if (c == key)
                    continue; // octets à 0 : totalement transparent
                data[o] = (Byte)(c & 0xFF);
                data[o + 1] = (Byte)((c >> 8) & 0xFF);
                data[o + 2] = (Byte)((c >> 16) & 0xFF);
                data[o + 3] = 0xFF;
            }
            var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, data, width * 4);
            source.Freeze();
            return source;
        }

        public static Color ToColor(Int32 rgb)
            => Color.FromRgb((Byte)((rgb >> 16) & 0xFF), (Byte)((rgb >> 8) & 0xFF), (Byte)(rgb & 0xFF));

        public static Int32 ToRgb(Color c) => (c.R << 16) | (c.G << 8) | c.B;
    }
}