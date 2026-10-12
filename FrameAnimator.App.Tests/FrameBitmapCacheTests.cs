namespace FrameAnimator.App.Tests
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Windows.Media.Imaging;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.Core.Model;

    public sealed class FrameBitmapCacheTests
    {
        private static Bitmap32 Sample(Int32 width = 4, Int32 height = 3, Int32 seed = 1)
        {
            Bitmap32 bitmap = new(width, height);
            Random random = new(seed);
            for (Int32 i = 0; i < bitmap.Pixels.Length; i++)
                bitmap.Pixels[i] = random.Next(0, 3) switch { 0 => 0x112233, 1 => 0xFF00FF, _ => random.Next(0x1000000) };
            return bitmap;
        }

        private static Byte[] Pixels(BitmapSource source)
        {
            Byte[] data = new Byte[source.PixelWidth * source.PixelHeight * 4];
            source.CopyPixels(data, source.PixelWidth * 4, 0);
            return data;
        }

        [Fact]
        public void SameBitmapAndKeyReturnsSameInstance()
        {
            FrameBitmapCache cache = new();
            Bitmap32 bitmap = Sample();

            BitmapSource first = cache.Get(bitmap, 0x112233);
            BitmapSource second = cache.Get(bitmap, 0x112233);

            Assert.Same(first, second);
            Assert.Equal(1, cache.Count);
        }

        [Fact]
        public void DifferentKeysReturnDifferentImagesWithExpectedPixels()
        {
            FrameBitmapCache cache = new();
            Bitmap32 bitmap = new(2, 1);
            bitmap.Pixels[0] = 0x112233; // R=0x11 G=0x22 B=0x33
            bitmap.Pixels[1] = 0xFF00FF;

            BitmapSource keyed = cache.Get(bitmap, 0x112233);
            BitmapSource opaque = cache.Get(bitmap, -1);
            BitmapSource ghost = cache.Get(bitmap, 0x112233, 0xFF0000);
            BitmapSource otherKey = cache.Get(bitmap, 0xFF00FF);

            Assert.NotSame(keyed, opaque);
            Assert.NotSame(keyed, ghost);
            Assert.NotSame(keyed, otherKey);

            // BGRA order, alpha 0 for the transparent pixel
            Assert.Equal(new Byte[] { 0, 0, 0, 0, 0xFF, 0x00, 0xFF, 0xFF }, Pixels(keyed));
            Assert.Equal(new Byte[] { 0x33, 0x22, 0x11, 0xFF, 0xFF, 0x00, 0xFF, 0xFF }, Pixels(opaque));
            // ghost: red forced to 255, the key is OR-ed the same way (0x112233 | 0xFF0000 = 0xFF2233: the first pixel is transparent)
            Assert.Equal(new Byte[] { 0, 0, 0, 0, 0xFF, 0x00, 0xFF, 0xFF }, Pixels(ghost));
            Assert.Equal(new Byte[] { 0x33, 0x22, 0x11, 0xFF, 0, 0, 0, 0 }, Pixels(otherKey));
        }

        [Fact]
        public void TransparentColorsWithTheSameEffectiveKeyShareTheImage()
        {
            FrameBitmapCache cache = new();
            Bitmap32 bitmap = Sample();

            BitmapSource a = cache.Get(bitmap, 0x002233, 0xFF0000);
            BitmapSource b = cache.Get(bitmap, 0x112233, 0xFF0000);

            Assert.Same(a, b);
        }

        [Fact]
        public void ResultIsFrozen()
        {
            FrameBitmapCache cache = new();
            Assert.True(cache.Get(Sample(), 0).IsFrozen);
            Assert.True(cache.Get(Sample(), -1, 0xFF0000).IsFrozen);
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0x112233, 0)]
        [InlineData(0x112233, 0xFF0000)]
        [InlineData(0xFF00FF, 0xFF0000)]
        public void MatchesUncachedConversionPixelForPixel(Int32 transparent, Int32 orMask)
        {
            FrameBitmapCache cache = new();
            Bitmap32 bitmap = Sample(17, 9, 5);

            BitmapSource cached = cache.Get(bitmap, transparent, orMask);
            BitmapSource direct = ImageConversion.ToBitmapSource(bitmap, transparent, orMask);

            Assert.Equal(direct.PixelWidth, cached.PixelWidth);
            Assert.Equal(direct.PixelHeight, cached.PixelHeight);
            Assert.Equal(direct.Format, cached.Format);
            Assert.Equal(Pixels(direct), Pixels(cached));
        }

        [Fact]
        public void InvalidateForgetsOnlyThatBitmap()
        {
            FrameBitmapCache cache = new();
            Bitmap32 a = Sample(seed: 1);
            Bitmap32 b = Sample(seed: 2);
            BitmapSource firstA = cache.Get(a, 0);
            BitmapSource firstB = cache.Get(b, 0);

            a.Pixels[0] = 0x010203; // in-place change, then invalidation
            cache.Invalidate(a);

            Assert.Equal(1, cache.Count);
            BitmapSource secondA = cache.Get(a, 0);
            Assert.NotSame(firstA, secondA);
            Assert.Same(firstB, cache.Get(b, 0));
            Assert.Equal(0x03, Pixels(secondA)[0]);
        }

        [Fact]
        public void ClearForgetsEverything()
        {
            FrameBitmapCache cache = new();
            Bitmap32 bitmap = Sample();
            BitmapSource first = cache.Get(bitmap, 0);

            cache.Clear();

            Assert.Equal(0, cache.Count);
            Assert.Equal(0, cache.TotalBytes);
            Assert.NotSame(first, cache.Get(bitmap, 0));
        }

        [Fact]
        public void CacheStaysBounded()
        {
            // each 16x16 image weighs 1024 bytes: room for 4
            FrameBitmapCache cache = new(4 * 1024);
            Bitmap32[] bitmaps = new Bitmap32[50];
            for (Int32 i = 0; i < bitmaps.Length; i++)
            {
                bitmaps[i] = Sample(16, 16, i);
                cache.Get(bitmaps[i], 0);
                Assert.True(cache.TotalBytes <= 4 * 1024);
            }

            Assert.Equal(4, cache.Count);
            // the most recent one is still cached, the oldest was evicted
            BitmapSource recent = cache.Get(bitmaps[^1], 0);
            Assert.Same(recent, cache.Get(bitmaps[^1], 0));
            Assert.Equal(4, cache.Count);
        }

        [Fact]
        public void LeastRecentlyUsedImageIsEvictedFirst()
        {
            FrameBitmapCache cache = new(2 * 1024);
            Bitmap32 a = Sample(16, 16, 1);
            Bitmap32 b = Sample(16, 16, 2);
            Bitmap32 c = Sample(16, 16, 3);
            BitmapSource imageA = cache.Get(a, 0);
            cache.Get(b, 0);
            Assert.Same(imageA, cache.Get(a, 0)); // a is now more recent than b
            cache.Get(c, 0); // evicts b

            Assert.Same(imageA, cache.Get(a, 0));
            Assert.Equal(2, cache.Count);
        }

        [Fact]
        public void ImageLargerThanTheCapIsStillReturned()
        {
            FrameBitmapCache cache = new(16);
            Bitmap32 bitmap = Sample(16, 16);

            BitmapSource image = cache.Get(bitmap, 0);

            Assert.Equal(16, image.PixelWidth);
            Assert.Same(image, cache.Get(bitmap, 0));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference FillAndDrop(FrameBitmapCache cache)
        {
            Bitmap32 bitmap = Sample(32, 32);
            cache.Get(bitmap, 0);
            cache.Get(bitmap, -1, 0xFF0000);
            return new WeakReference(bitmap);
        }

        [Fact]
        public void CacheDoesNotKeepBitmapsAlive()
        {
            FrameBitmapCache cache = new();
            WeakReference weak = FillAndDrop(cache);

            for (Int32 i = 0; i < 5 && weak.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            Assert.False(weak.IsAlive);

            // entries of dead bitmaps are purged at the next miss
            cache.Get(Sample(), 0);
            Assert.Equal(1, cache.Count);
        }

        [Fact]
        public void CachedConversionIsMuchFasterThanUncached()
        {
            FrameBitmapCache cache = new();
            Bitmap32 bitmap = Sample(256, 256);
            const Int32 iterations = 2000;

            // warm up (JIT, WPF initialization)
            ImageConversion.ToBitmapSource(bitmap, 0);
            cache.Get(bitmap, 0);

            Stopwatch uncached = Stopwatch.StartNew();
            for (Int32 i = 0; i < iterations; i++)
                ImageConversion.ToBitmapSource(bitmap, 0);
            uncached.Stop();

            Stopwatch cached = Stopwatch.StartNew();
            for (Int32 i = 0; i < iterations; i++)
                cache.Get(bitmap, 0);
            cached.Stop();

            // typically hundreds of times faster; the margin is generous to stay stable on a loaded machine
            Assert.True(cached.Elapsed.TotalMilliseconds * 10 < uncached.Elapsed.TotalMilliseconds,
                $"cached {cached.Elapsed.TotalMilliseconds:F1} ms, uncached {uncached.Elapsed.TotalMilliseconds:F1} ms");
        }
    }
}
