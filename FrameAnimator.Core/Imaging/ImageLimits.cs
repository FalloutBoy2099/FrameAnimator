namespace FrameAnimator.Core.Imaging
{
    using System;

    /// <summary>
    /// Shared size limits applied by the image readers (BMP, PNG, GIF, FRM) so that a corrupt or hostile file can never
    /// trigger a huge allocation or an endless decoding. Every check is done on the declared header values, with 64-bit
    /// arithmetic, <b>before</b> any pixel buffer is allocated.
    /// </summary>
    public static class ImageLimits
    {
        /// <summary>Maximum number of pixels (width × height) of one decoded image or frame: 16 Mpx, far beyond any game image.</summary>
        public const Int32 MaxPixels = 16 * 1024 * 1024;

        /// <summary>Maximum width or height, in pixels, of one decoded image (the GIF format cannot express more).</summary>
        public const Int32 MaxDimension = 65535;

        /// <summary>Maximum number of image descriptors (sub-images, empty ones included) accepted in one GIF file.</summary>
        public const Int32 MaxGifSubImages = 4096;

        /// <summary>Maximum cumulated number of pixels of all the decoded sub-images of one GIF file (64 Mpx = 256 MB of pixels).</summary>
        public const Int32 MaxGifTotalPixels = 4 * MaxPixels;

        /// <summary>
        /// <c>true</c> if <paramref name="width"/> × <paramref name="height"/> is a valid image size: both strictly positive,
        /// at most <see cref="MaxDimension"/> each, and at most <see cref="MaxPixels"/> pixels in total.
        /// </summary>
        public static Boolean IsValidSize(Int64 width, Int64 height)
            => width > 0 && height > 0 && width <= MaxDimension && height <= MaxDimension && width * height <= MaxPixels;
    }
}
