namespace FrameAnimator.Core.Tests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using FrameAnimator.Core.Formats;
    using FrameAnimator.Core.Imaging;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Services;

    /// <summary>Size limits of the image readers: hostile headers must be rejected quickly and without a large allocation.</summary>
    public sealed class ImageLimitsTests
    {
        private static String GoldenRoot => Path.Combine(AppContext.BaseDirectory, "Golden");

        /// <summary>Allocation budget for a rejected file: far below the hundreds of MB the declared image would need.</summary>
        private const Int64 SmallAllocation = 8L * 1024 * 1024;

        /// <summary>Runs <paramref name="read"/> on the current thread; it must throw <see cref="ImageFormatException"/> quickly with a small allocation.</summary>
        private static void AssertRejected(Action read)
        {
            // warm-up so that JIT / static initialization allocations are not counted
            try
            { read(); }
            catch (ImageFormatException) { }

            Int64 before = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            Assert.Throws<ImageFormatException>(read);
            sw.Stop();
            Int64 allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"Rejection too slow: {sw.Elapsed}");
            Assert.True(allocated < SmallAllocation, $"Rejection allocated {allocated:N0} bytes");
        }

        [Fact]
        public void LimitsAreConsistent()
        {
            Assert.Equal(ImageLimits.MaxPixels, FrmFormat.MaxFramePixels);
            Assert.Equal(16 * 1024 * 1024, ImageLimits.MaxPixels);
            Assert.True(ImageLimits.MaxDimension > 0 && ImageLimits.MaxGifSubImages > 0);
            Assert.True(ImageLimits.IsValidSize(4096, 4096));
            Assert.False(ImageLimits.IsValidSize(4097, 4096));
            Assert.False(ImageLimits.IsValidSize(0, 10));
            Assert.False(ImageLimits.IsValidSize(10, -1));
            Assert.False(ImageLimits.IsValidSize(ImageLimits.MaxDimension + 1, 1));
        }

        // ------------------------------------------------------------------ BMP

        [Theory]
        [InlineData(65535, 65535, 32)]                   // 4 Gpx
        [InlineData(65535, -65535, 24)]                  // top-down
        [InlineData(4097, 4096, 24)]                     // one row over 16 Mpx
        [InlineData(65536, 1, 24)]                       // over the dimension limit
        [InlineData(Int32.MaxValue, Int32.MaxValue, 24)]
        [InlineData(Int32.MaxValue, Int32.MinValue, 24)] // -Int32.MinValue overflows
        [InlineData(0, 10, 24)]
        [InlineData(10, 0, 24)]
        [InlineData(-5, 10, 24)]
        [InlineData(Int32.MinValue, 10, 24)]
        [InlineData(10, 10, 0)]                          // unsupported bpp
        [InlineData(10, 10, 7)]
        [InlineData(10, 10, 65535)]
        public void BmpHostileDimensionsAreRejected(Int32 width, Int32 height, Int32 bpp)
        {
            Byte[] data = CraftedFiles.Bmp(width, height, bpp);
            AssertRejected(() => BmpCodec.Read(data));
        }

        [Theory]
        [InlineData(Int32.MaxValue, 40, 0)]    // header size overflowing 14 + headerSize
        [InlineData(Int32.MinValue, 40, 0)]
        [InlineData(-1, 40, 0)]
        [InlineData(39, 40, 0)]
        [InlineData(1000000, 40, 0)]           // larger than the file
        public void BmpBadHeaderSizeIsRejected(Int32 headerSize, Int32 pixelOffset, Int32 compression)
        {
            Byte[] data = CraftedFiles.Bmp(10, 10, 24, compression, headerSize: headerSize, pixelOffset: pixelOffset);
            AssertRejected(() => BmpCodec.Read(data));
        }

        [Theory]
        [InlineData(4, 1)]    // RLE8 declared on a 4 bpp image
        [InlineData(8, 2)]    // RLE4 declared on an 8 bpp image
        [InlineData(24, 1)]
        [InlineData(8, 4)]    // JPEG / PNG compression
        [InlineData(8, 99)]
        public void BmpBadCompressionIsRejected(Int32 bpp, Int32 compression)
        {
            Byte[] data = CraftedFiles.Bmp(10, 10, bpp, compression);
            AssertRejected(() => BmpCodec.Read(data));
        }

        [Fact]
        public void BmpNegativeOrHugePixelOffsetDoesNotEscape()
        {
            foreach (Int32 offset in new[] { -1, Int32.MinValue, Int32.MaxValue, Int32.MaxValue - 10 })
            {
                Byte[] data = CraftedFiles.Bmp(10, 10, 24, pixelOffset: offset);
                try
                { BmpCodec.Read(data); }
                catch (ImageFormatException) { }
            }
        }

        [Fact]
        public void BmpHugePaletteCountAndTruncatedRleStayInsideImageFormatException()
        {
            Byte[] palette = CraftedFiles.Bmp(10, 10, 8, colorsUsed: Int32.MaxValue);
            try
            { BmpCodec.Read(palette); }
            catch (ImageFormatException) { }

            // RLE8 absolute run announcing more bytes than the file contains
            Byte[] rle = CraftedFiles.Bmp(10, 10, 8, compression: 1, payload: 0);
            Array.Resize(ref rle, rle.Length + 4);
            rle[^4] = 0;
            rle[^3] = 200;
            Assert.Throws<ImageFormatException>(() => BmpCodec.Read(rle));
        }

        [Fact]
        public void BmpRleCannotWriteOutsideTheDeclaredImage()
        {
            // 2x2 RLE8 image, then runs of 255 pixels on and on: output must stay clipped, not throw
            Byte[] header = CraftedFiles.Bmp(2, 2, 8, compression: 1, payload: 0, pixelOffset: 54 + 1024);
            var data = new Byte[54 + 1024 + 4000];
            Array.Copy(header, data, header.Length);
            for (Int32 i = 54 + 1024; i + 1 < data.Length; i += 2)
            {
                data[i] = 255;
                data[i + 1] = 7;
            }
            var bmp = BmpCodec.Read(data);
            Assert.Equal(4, bmp.Pixels.Length);
        }

        // ------------------------------------------------------------------ PNG

        [Theory]
        [InlineData(0u, 10u)]
        [InlineData(10u, 0u)]
        [InlineData(4097u, 4096u)]                  // over 16 Mpx
        [InlineData(65536u, 1u)]                    // over the dimension limit
        [InlineData(0x7FFFFFFFu, 0x7FFFFFFFu)]
        [InlineData(0x80000000u, 1u)]               // negative as Int32
        [InlineData(0xFFFFFFFFu, 0xFFFFFFFFu)]
        public void PngHostileDimensionsAreRejected(UInt32 width, UInt32 height)
        {
            Byte[] data = CraftedFiles.Png(width, height, 8, 2, CraftedFiles.ZlibZeros(100));
            AssertRejected(() => PngCodec.Read(data));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(3, 2)]    // bit depth 3 does not exist
        [InlineData(8, 5)]    // color type 5 does not exist
        [InlineData(16, 3)]   // palette with 16 bits
        [InlineData(4, 2)]    // RGB 4 bits
        [InlineData(32, 6)]
        public void PngInvalidDepthOrColorTypeIsRejected(Int32 depth, Int32 colorType)
        {
            Byte[] data = CraftedFiles.Png(10, 10, (Byte)depth, (Byte)colorType, CraftedFiles.ZlibZeros(1000), plte: true);
            AssertRejected(() => PngCodec.Read(data));
        }

        [Fact]
        public void PngDecompressionBombIsAbortedEarly()
        {
            // 10x10 RGB declares 310 bytes of pixel data; the IDAT inflates to 256 MB (a few hundred KB compressed)
            Byte[] bomb = CraftedFiles.Png(10, 10, 8, 2, CraftedFiles.ZlibZeros(256L * 1024 * 1024));
            Assert.True(bomb.Length < 2 * 1024 * 1024, "the bomb itself must stay small");
            AssertRejected(() => PngCodec.Read(bomb));
        }

        [Fact]
        public void PngIdatInflatingByJustOneByteMoreThanDeclaredIsRejected()
        {
            Byte[] data = CraftedFiles.Png(10, 10, 8, 2, CraftedFiles.ZlibZeros(10 * (1 + 30) + 1));
            Assert.Throws<ImageFormatException>(() => PngCodec.Read(data));
            Byte[] exact = CraftedFiles.Png(10, 10, 8, 2, CraftedFiles.ZlibZeros(10 * (1 + 30)));
            Assert.Equal(100, PngCodec.Read(exact).Pixels.Length);
        }

        [Fact]
        public void PngTinyFileDeclaringAHugeImageIsRejectedWithoutAllocatingIt()
        {
            // 4096 x 4096 RGB16 is within the limits (and would need 100+ MB), but the IDAT only holds a few bytes
            Byte[] data = CraftedFiles.Png(4096, 4096, 16, 2, CraftedFiles.ZlibZeros(100));
            Assert.True(data.Length < 200);
            AssertRejected(() => PngCodec.Read(data));

            Byte[] interlaced = CraftedFiles.Png(4096, 4096, 16, 6, CraftedFiles.ZlibZeros(100), interlace: 1);
            AssertRejected(() => PngCodec.Read(interlaced));
        }

        [Fact]
        public void PngChunkLengthLargerThanTheRemainingDataIsRejected()
        {
            Byte[] valid = File.ReadAllBytes(Path.Combine(GoldenRoot, "p0.png"));
            foreach (UInt32 length in new[] { 0x7FFFFFFFu, 0x80000000u, 0xFFFFFFFFu, (UInt32)valid.Length })
            {
                Byte[] data = (Byte[])valid.Clone();
                data[8] = (Byte)(length >> 24);       // length of the IHDR chunk
                data[9] = (Byte)(length >> 16);
                data[10] = (Byte)(length >> 8);
                data[11] = (Byte)length;
                AssertRejected(() => PngCodec.Read(data));
            }
        }

        // ------------------------------------------------------------------ GIF

        [Theory]
        [InlineData(65535, 65535)]   // 4 Gpx, still a legal GIF descriptor
        [InlineData(4097, 4096)]     // one row over 16 Mpx
        public void GifHugeSubImageIsRejectedBeforeAllocation(Int32 w, Int32 h)
        {
            Byte[] data = CraftedFiles.Gif(10, 10, ((UInt16)w, (UInt16)h));
            AssertRejected(() => GifCodec.Read(data));
        }

        [Fact]
        public void GifTooManySubImagesAreRejected()
        {
            var images = new (UInt16, UInt16)[ImageLimits.MaxGifSubImages + 1];   // all empty (0 x 0), so cheap to decode
            Byte[] data = CraftedFiles.Gif(10, 10, images);
            AssertRejected(() => GifCodec.Read(data));

            var allowed = new (UInt16, UInt16)[ImageLimits.MaxGifSubImages];
            Assert.Empty(GifCodec.Read(CraftedFiles.Gif(10, 10, allowed)).SubImages);   // empty sub-images are ignored
        }

        [Fact]
        public void GifTruncatedStructuresAreImageFormatExceptions()
        {
            Byte[] valid = File.ReadAllBytes(Path.Combine(GoldenRoot, "g0.gif"));
            // every possible truncation point must give a clean result: either a (shorter) image or an ImageFormatException
            for (Int32 n = 0; n < valid.Length; n++)
            {
                try
                { GifCodec.Read(valid[..n]); }
                catch (ImageFormatException) { }
            }
        }

        [Fact]
        public void GifLzwOutputNeverExceedsTheDeclaredSize()
        {
            // 2x2 image whose LZW stream is a long run of valid codes: the decoder must stop at 4 pixels
            using var ms = new MemoryStream();
            ms.Write("GIF89a"u8);
            ms.Write([4, 0, 4, 0, 0x80, 0, 0, 0, 0, 0, 255, 255, 255]);
            ms.Write([0x2C, 0, 0, 0, 0, 2, 0, 2, 0, 0, 2]);
            for (Int32 block = 0; block < 100; block++)
            {
                ms.WriteByte(255);
                for (Int32 i = 0; i < 255; i++)
                    ms.WriteByte(0x44);
            }
            ms.WriteByte(0);
            ms.WriteByte(0x3B);
            GifImage gif;
            try
            { gif = GifCodec.Read(ms.ToArray()); }
            catch (ImageFormatException)
            { return; }
            foreach (var sub in gif.SubImages)
                Assert.Equal(4, sub.Bitmap.Pixels.Length);
        }

        // ------------------------------------------------------------------ FRM and valid files

        [Fact]
        public void FrmFrameLimitStillWorks()
        {
            using var dir = new TempDir();
            String ok = dir.File("ok.frm");
            File.WriteAllBytes(ok, CraftedFiles.Frm(4, 4));
            Samples.NewProject(out var uiOk, out var svcOk);
            Assert.True(svcOk.LoadFrm(ok), String.Join(" | ", uiOk.Errors));
            Assert.Equal(1, svcOk.Project.Directions[0].Count);

            String bad = dir.File("bad.frm");
            File.WriteAllBytes(bad, CraftedFiles.Frm(4097, 4096));   // 16 Mpx + one row
            Samples.NewProject(out var ui, out var svc);
            Assert.False(svc.LoadFrm(bad));
            Assert.NotEmpty(ui.Errors);
            Assert.Equal(0, svc.Project.Directions[0].Count);

            using var fs = File.OpenRead(bad);
            FrmFormat.ReadHeader(fs);
            var project = new Project();
            Assert.Throws<InvalidDataException>(() => FrmFormat.ReadFrame(project, fs, project.Directions[0], out _, out _));
        }

        [Fact]
        public void ValidGoldenFilesStillDecode()
        {
            var bmp = BmpCodec.ReadFile(Path.Combine(GoldenRoot, "a0.bmp"));
            Assert.True(bmp.Width > 0 && bmp.Height > 0);
            var png = PngCodec.ReadFile(Path.Combine(GoldenRoot, "p0.png"));
            Assert.True(png.Width > 0 && png.Height > 0);
            var gif = GifCodec.ReadFile(Path.Combine(GoldenRoot, "g0.gif"));
            Assert.NotEmpty(gif.SubImages);
            Assert.All(gif.SubImages, s => Assert.True(s.Width > 0 && s.Height > 0));
        }

        [Fact]
        public void RoundTripThroughTheCodecsStillWorks()
        {
            var src = Samples.Rect(37, 23, 0x102030, 0xFF8000, 3, 4, 20, 15);
            using var ms = new MemoryStream();

            BmpCodec.Write(src, ms);
            Assert.Equal(src.Pixels, BmpCodec.Read(ms.ToArray()).Pixels);

            ms.SetLength(0);
            PngCodec.Write(src, ms);
            Assert.Equal(src.Pixels, PngCodec.Read(ms.ToArray()).Pixels);

            ms.SetLength(0);
            GifCodec.Write(src, ms);
            var gif = GifCodec.Read(ms.ToArray());
            Assert.Single(gif.SubImages);
            Assert.Equal(src.Pixels, gif.SubImages[0].Bitmap.Pixels);
        }

        [Fact]
        public void ImageAtTheExactPixelLimitIsAcceptedByTheHeaderChecks()
        {
            // 4096 x 4096 = exactly 16 Mpx: not rejected by the size checks (no pixel data at all: blank image)
            Byte[] bmp = CraftedFiles.Bmp(4096, 4096, 24, pixelOffset: Int32.MaxValue);
            var decoded = BmpCodec.Read(bmp);
            Assert.Equal(4096 * 4096, decoded.Pixels.Length);
        }
    }
}
