namespace FrameAnimator.App.Infrastructure
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    /// <summary>Development helper: saves the rendering of a window to a PNG (switches <c>--snapshot</c> / <c>--dialog</c>).</summary>
    public static class DevSnapshot
    {
        public static void Save(Window window, String path)
        {
            window.UpdateLayout();
            Int32 width = Math.Max(1, (Int32)Math.Ceiling(window.ActualWidth));
            Int32 height = Math.Max(1, (Int32)Math.Ceiling(window.ActualHeight));
            RenderTargetBitmap target = new(width, height, 96, 96, PixelFormats.Pbgra32);
            target.Render(window);
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(target));
            using FileStream stream = File.Create(path);
            encoder.Save(stream);
        }
    }
}