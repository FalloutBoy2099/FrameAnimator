namespace FrameAnimator.App.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Windows.Media.Imaging;
    using FrameAnimator.Core.Model;

    /// <summary>
    /// Bounded cache of frozen <see cref="BitmapSource"/>s converted from <see cref="Bitmap32"/>s. An image is identified by the
    /// <see cref="Bitmap32"/> instance, the transparent color and the OR mask. The pixels of a <see cref="Bitmap32"/> are never changed
    /// once a frame is loaded (a frame whose image changes receives a new instance), so an entry stays valid until its key changes;
    /// <see cref="Invalidate"/> and <see cref="Clear"/> cover any code that would still change the pixels in place.
    /// The bitmaps are only referenced weakly, and the total size of the cached images is capped (least recently used images are dropped
    /// first). Not thread-safe: UI thread only.
    /// </summary>
    public sealed class FrameBitmapCache
    {
        /// <summary>Default size cap of the shared cache (BGRA bytes).</summary>
        public const Int64 DefaultMaxBytes = 128L * 1024 * 1024;

        private sealed class Entry(WeakReference<Bitmap32> owner, Int32 key, Int32 orMask, BitmapSource image, Int64 bytes)
        {
            public WeakReference<Bitmap32> Owner { get; } = owner;
            public Int32 Key { get; } = key;
            public Int32 OrMask { get; } = orMask;
            public BitmapSource Image { get; } = image;
            public Int64 Bytes { get; } = bytes;
            public LinkedListNode<Entry>? Node { get; set; }
        }

        // variants of each live bitmap (the table does not keep the bitmap alive)
        private readonly ConditionalWeakTable<Bitmap32, List<Entry>> _variants = new();

        // most recently used first; also holds the entries of bitmaps that died until they are purged
        private readonly LinkedList<Entry> _lru = new();
        private readonly Int64 _maxBytes;
        private Int64 _totalBytes;

        /// <summary>The cache used by the drawing area.</summary>
        public static FrameBitmapCache Shared { get; } = new();

        public FrameBitmapCache(Int64 maxBytes = DefaultMaxBytes)
        {
            if (maxBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBytes));
            _maxBytes = maxBytes;
        }

        /// <summary>Number of cached images.</summary>
        public Int32 Count => _lru.Count;

        /// <summary>Total size in bytes of the cached images.</summary>
        public Int64 TotalBytes => _totalBytes;

        /// <summary>
        /// Returns the frozen BGRA image of <paramref name="bitmap"/> (see <see cref="ImageConversion.ToBitmapSource"/> for the meaning of
        /// <paramref name="transparent"/> and <paramref name="orMask"/>), converting it only when it is not cached.
        /// </summary>
        public BitmapSource Get(Bitmap32 bitmap, Int32 transparent, Int32 orMask = 0)
        {
            // different transparent colors that end up as the same key give the same image
            Int32 key = transparent < 0 ? -1 : (transparent | orMask) & 0xFFFFFF;

            List<Entry>? list = null;
            if (_variants.TryGetValue(bitmap, out list))
            {
                for (Int32 i = 0; i < list.Count; i++)
                {
                    Entry e = list[i];
                    if (e.Key == key && e.OrMask == orMask)
                    {
                        if (e.Node != _lru.First)
                        {
                            _lru.Remove(e.Node!);
                            _lru.AddFirst(e.Node!);
                        }
                        return e.Image;
                    }
                }
            }

            BitmapSource image = ImageConversion.ToBitmapSource(bitmap, transparent, orMask);
            Int64 bytes = (Int64)image.PixelWidth * image.PixelHeight * 4;
            if (list is null)
            {
                list = [];
                _variants.Add(bitmap, list);
            }

            Entry entry = new(new WeakReference<Bitmap32>(bitmap), key, orMask, image, bytes);
            entry.Node = _lru.AddFirst(entry);
            list.Add(entry);
            _totalBytes += bytes;
            Trim();
            return image;
        }

        /// <summary>Forgets every image of <paramref name="bitmap"/> (to call if its pixels were changed in place).</summary>
        public void Invalidate(Bitmap32 bitmap)
        {
            if (!_variants.TryGetValue(bitmap, out List<Entry>? list))
                return;
            foreach (Entry e in list)
                Remove(e);
            list.Clear();
        }

        /// <summary>Forgets everything.</summary>
        public void Clear()
        {
            foreach (Entry e in _lru)
            {
                if (e.Owner.TryGetTarget(out Bitmap32? bitmap) && _variants.TryGetValue(bitmap, out List<Entry>? list))
                    list.Clear();
            }
            _lru.Clear();
            _totalBytes = 0;
        }

        private void Remove(Entry entry)
        {
            if (entry.Node is { List: not null } node)
            {
                _lru.Remove(node);
                _totalBytes -= entry.Bytes;
            }
            entry.Node = null;
        }

        private void Trim()
        {
            // entries of dead bitmaps can never be requested again: drop them first (a miss is far costlier than this scan)
            LinkedListNode<Entry>? node = _lru.Last;
            while (node is not null)
            {
                LinkedListNode<Entry>? previous = node.Previous;
                if (!node.Value.Owner.TryGetTarget(out _))
                    Remove(node.Value);
                node = previous;
            }

            // then the least recently used ones, always keeping the image that was just requested
            while (_totalBytes > _maxBytes && _lru.Count > 1)
            {
                Entry oldest = _lru.Last!.Value;
                if (oldest.Owner.TryGetTarget(out Bitmap32? bitmap) && _variants.TryGetValue(bitmap, out List<Entry>? list))
                    list.Remove(oldest);
                Remove(oldest);
            }
        }
    }
}
