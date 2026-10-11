namespace FrameAnimator.Core.Geometry
{
    using System;
    using System.Collections.Generic;
    using Model;

    /// <summary>
    /// Color → palette index conversion (0x40381c). Index 0 is reserved for transparency; indices 1..255 are
    /// the only candidates. Despite the original's name "Dithering Method", there is no error diffusion.
    /// </summary>
    public sealed class ColorIndexer
    {
        private readonly Int32[] _palette;
        private readonly Byte[]? _falloutTable;
        private readonly DitherMethod _method;
        private readonly Int32 _transparent;
        private readonly Dictionary<Int32, Byte> _cache = [];
        private readonly Boolean _legacy;

        /// <param name="legacy">
        /// Reproduces the exact behavior of the original (bug: component differences are converted to 32-bit UNSIGNED
        /// integers before the x87 floating-point computation, see specification §5.3). <c>false</c> = mathematically correct distance.
        /// </param>
        public ColorIndexer(Palette palette, DitherMethod method, Int32 transparentColor, Boolean legacy = true)
        {
            _legacy = legacy;
            _palette = palette.Colors;
            _falloutTable = palette.FalloutTable;
            _method = method;
            _transparent = transparentColor & 0xFFFFFF;
        }

        public Byte Index(Int32 color)
        {
            color &= 0xFFFFFF;
            if (color == _transparent)
                return 0;

            if (_cache.TryGetValue(color, out Byte cached))
                return cached;

            var r = Compute(color);
            _cache[color] = r;

            return r;
        }

        private Byte Compute(Int32 color)
        {
            switch (_method)
            {
                case DitherMethod.Default:
                    return Nearest(color, 1, 1, 1);
                case DitherMethod.Advanced:
                    return Nearest(color, 30, 59, 11);
                case DitherMethod.Fallout:
                    if (_falloutTable is null)
                        return 0;
                    var idx = ((Bitmap32.Red(color) >> 3) << 10) | ((Bitmap32.Green(color) >> 3) << 5) | (Bitmap32.Blue(color) >> 3);
                    return _falloutTable[idx];
                default:
                    return 0;
            }
        }

        private Byte Nearest(Int32 color, Int32 wr, Int32 wg, Int32 wb)
        {
            Int32 r = Bitmap32.Red(color), g = Bitmap32.Green(color), b = Bitmap32.Blue(color);
            var best = Double.MaxValue;
            Byte bestIndex = 1;
            var weighted = wr != 1;
            for (Int32 i = 1; i < Palette.Size; i++)
            {
                var p = _palette[i];
                if (p == color)
                    return (Byte)i;
                Int32 dr = r - Bitmap32.Red(p), dg = g - Bitmap32.Green(p), db = b - Bitmap32.Blue(p);
                var d = _legacy
                    ? LegacyDistance(dr, dg, db, weighted)
                    : wr * (Double)dr * dr + wg * (Double)dg * dg + wb * (Double)db * db;
                if (d < best)
                {
                    best = d;
                    bestIndex = (Byte)i;
                }
            }
            return bestIndex;
        }

        // ---- x87 arithmetic of the original (64-bit extended precision, result stored as Double) -----------------------

        /// <summary>
        /// Distance of the original (0x40381c): each difference is a <c>uint32</c> (high byte of the <c>fild</c> forced to 0),
        /// squares/products and sums are rounded to a 64-bit mantissa (extended precision), the final result to 53 bits.
        /// </summary>
        internal static Double LegacyDistance(Int32 dr, Int32 dg, Int32 db, Boolean weighted)
        {
            UInt128 a = (UInt32)dr, b = (UInt32)dg, c = (UInt32)db;
            UInt128 sum;
            if (!weighted)
            {
                sum = RoundBits(a * a + b * b, 64);
                sum = RoundBits(sum + c * c, 64);
            }
            else
            {
                UInt128 t1 = RoundBits(30 * a * a, 64);
                UInt128 t2 = RoundBits(59 * b * b, 64);
                sum = RoundBits(t1 + t2, 64);
                UInt128 t3 = RoundBits(11 * c * c, 64);
                sum = RoundBits(sum + t3, 64);
            }
            return ToDouble(RoundBits(sum, 53));
        }

        private static Int32 BitLength(UInt128 x) => 128 - (Int32)UInt128.LeadingZeroCount(x);

        /// <summary>Rounds to <paramref name="bits"/> significant bits (to nearest, ties to even).</summary>
        private static UInt128 RoundBits(UInt128 x, Int32 bits)
        {
            var len = BitLength(x);
            if (len <= bits)
                return x;
            var shift = len - bits;
            UInt128 q = x >> shift;
            UInt128 rem = x & ((UInt128.One << shift) - 1);
            UInt128 half = UInt128.One << (shift - 1);
            if (rem > half || (rem == half && (q & 1) == 1))
                q++;
            return q << shift;
        }

        private static Double ToDouble(UInt128 x)
        {
            var len = BitLength(x);
            if (len <= 64)
                return (Double)(UInt64)x;

            var shift = len - 64;

            return (Double)(UInt64)(x >> shift) * Math.Pow(2, shift);
        }
    }
}