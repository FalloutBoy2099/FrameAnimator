namespace FrameAnimator.Core.Geometry
{
    using System;
    using Model;

    /// <summary>
    /// Position/offset formulas taken over unchanged from the original (specification §5.1 to §5.4).
    /// All divisions by 2 are integer divisions truncated toward 0 (like <c>sar</c>+<c>adc</c>).
    /// </summary>
    public static class FrameMath
    {
        /// <summary>Transparent color of a frame according to the mode (0x403470).</summary>
        public static Int32 TransparentColor(Project project, Frame frame)
        {
            return project.Settings.TransparentMode switch
            {
                TransparentMode.Auto => frame.Bitmap.Pixels.Length == 0 ? 0 : frame.Bitmap.Pixels[0] & 0xFFFFFF,
                TransparentMode.Palette => project.Palette.Colors[0],
                TransparentMode.Sample => project.Settings.SampleColor,
                _ => 0,// undefined value in the original (unreachable mode)
            };
        }

        /// <summary>
        /// Auto-cut (0x4036f0): computes and stores the useful rectangle of the frame. Comparisons are done on 24 bits.
        /// A fully transparent frame deliberately yields the same degenerate values as the original
        /// (<c>Width = 1 − bmpWidth</c>).
        /// </summary>
        public static void AutoCut(Project project, Frame frame)
        {
            var bmp = frame.Bitmap;
            if (!project.Settings.AutoCutting)
            {
                frame.Left = 0;
                frame.Top = 0;
                frame.Width = (Int16)bmp.Width;
                frame.Height = (Int16)bmp.Height;
                return;
            }

            var transparent = TransparentColor(project, frame);
            Int32 left = bmp.Width, top = bmp.Height, maxX = 0, maxY = 0;
            var px = bmp.Pixels;

            for (var y = 0; y < bmp.Height; y++)
            {
                var rowStart = y * bmp.Width;
                for (Int32 x = 0; x < bmp.Width; x++)
                {
                    if ((px[rowStart + x] & 0xFFFFFF) == transparent)
                        continue;
                    if (y < top)
                        top = y;
                    if (x < left)
                        left = x;
                    if (y > maxY)
                        maxY = y;
                    if (x > maxX)
                        maxX = x;
                }
            }

            frame.Left = (Int16)left;
            frame.Top = (Int16)top;
            frame.Width = (Int16)(maxX + 1 - left);
            frame.Height = (Int16)(maxY + 1 - top);
        }

        /// <summary>Horizontal center of the useful rectangle relative to the bitmap center (without offset).</summary>
        public static Int32 CenterX(Frame f) => f.Left + f.Width / 2 - f.Bitmap.Width / 2;

        /// <summary>Bottom edge of the useful rectangle relative to the bitmap bottom (without offset).</summary>
        public static Int32 CenterY(Frame f) => f.Top + f.Height - f.Bitmap.Height;

        public static Int32 PositionX(Frame f) => CenterX(f) + f.OffsetX;
        public static Int32 PositionY(Frame f) => CenterY(f) + f.OffsetY;

        /// <summary>Offset of <paramref name="cur"/> relative to <paramref name="prev"/> (0x403550) — value written to the FRM.</summary>
        public static (Int32 X, Int32 Y) Relative(Frame prev, Frame cur)
            => (PositionX(cur) - PositionX(prev), PositionY(cur) - PositionY(prev));

        /// <summary>Places <paramref name="cur"/> at <paramref name="x"/>,<paramref name="y"/> from <paramref name="prev"/> (0x4034a4).</summary>
        public static void SetRelative(Frame prev, Frame cur, Int32 x, Int32 y)
        {
            cur.OffsetX = PositionX(prev) + x - CenterX(cur);
            cur.OffsetY = PositionY(prev) + y - CenterY(cur);
        }

        /// <summary>Direction offset written to the FRM header (0x403678). Requires at least one frame.</summary>
        public static (Int32 X, Int32 Y) GetShift(Direction dir)
        {
            var f0 = dir.Frames[0];
            return (dir.ShiftBaseX + CenterX(f0), dir.ShiftBaseY + CenterY(f0));
        }

        /// <summary>Inverse of <see cref="GetShift"/> (0x403610).</summary>
        public static void SetShift(Direction dir, Int32 x, Int32 y)
        {
            var f0 = dir.Frames[0];
            dir.ShiftBaseX = x - CenterX(f0);
            dir.ShiftBaseY = y - CenterY(f0);
        }

        /// <summary>
        /// On-screen offset of frame <paramref name="index"/>, relative to the anchor point at the center of the drawing area
        /// (formula of <c>pbxFramePaint</c>, specification §5.4 "Display"). Returns the top-left corner of the full bitmap.
        /// </summary>
        public static (Int32 X, Int32 Y) DisplayPosition(Direction dir, Int32 index, Int32 centerX, Int32 centerY)
        {
            var f = dir.Frames[index];
            var f0 = dir.Frames[0];
            var x = centerX - f.Bitmap.Width / 2 + f.OffsetX + dir.ShiftBaseX + dir.FirstFrameOffsetX - f0.OffsetX;
            var y = centerY - f.Bitmap.Height + f.OffsetY + dir.ShiftBaseY + dir.FirstFrameOffsetY - f0.OffsetY;
            
            return (x, y);
        }
    }
}