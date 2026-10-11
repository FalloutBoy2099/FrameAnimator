namespace FrameAnimator.Core.Model
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One frame of the animation (<c>IFrame</c> interface of the original, 0x1c bytes).
    /// </summary>
    public sealed class Frame(Bitmap32 bitmap)
    {
        public const UInt16 NoSourceIndex = 0xFFFF;

        public Bitmap32 Bitmap { get; set; } = bitmap;

        /// <summary>
        /// "Useful" rectangle (result of the auto-cut): left X, top Y, width, height. The fields are
        /// <c>int16</c> in the original; they are recomputed by <see cref="Geometry.FrameMath.AutoCut"/>.
        /// </summary>
        public Int16 Left { get; set; }
        public Int16 Top { get; set; }
        public Int16 Width { get; set; } = (Int16)bitmap.Width;
        public Int16 Height { get; set; } = (Int16)bitmap.Height;

        /// <summary>ABSOLUTE offset of the frame (see specification §5.4).</summary>
        public Int32 OffsetX { get; set; }
        public Int32 OffsetY { get; set; }

        /// <summary>Index of the sub-image within a GIF; <see cref="NoSourceIndex"/> (0xFFFF) for BMP/PNG/FRM.</summary>
        public UInt16 SourceIndex { get; set; } = NoSourceIndex;

        /// <summary>Path of the source image file (may not exist for a frame coming from an FRM).</summary>
        public String FileName { get; set; } = "";

        public Boolean HasSourceIndex => SourceIndex != NoSourceIndex;

        /// <summary>Resets the useful rectangle to the whole image.</summary>
        public void ResetRectangle()
        {
            Left = 0;
            Top = 0;
            Width = (Int16)Bitmap.Width;
            Height = (Int16)Bitmap.Height;
        }
    }

    /// <summary>
    /// Collection of frames for one direction (<c>IFrames</c> interface of the original, 0x14 bytes).
    /// </summary>
    public sealed class Direction
    {
        public List<Frame> Frames { get; } = [];

        /// <summary>Internal "Frames Offset": see <see cref="Geometry.FrameMath.GetShift"/>/<c>SetShift</c> (bytes +4/+8).</summary>
        public Int32 ShiftBaseX { get; set; }
        public Int32 ShiftBaseY { get; set; }

        /// <summary>Offset written for the first frame in the FRM (bytes +0xc/+0x10).</summary>
        public Int32 FirstFrameOffsetX { get; set; }
        public Int32 FirstFrameOffsetY { get; set; }

        public Int32 Count => Frames.Count;

        public void Clear()
        {
            Frames.Clear();
            ShiftBaseX = ShiftBaseY = FirstFrameOffsetX = FirstFrameOffsetY = 0;
        }

        /// <summary>Clears only the frames (behavior of <c>IFrames::Clear</c> 0x4019f0, used by REMOVE_FRAMES).</summary>
        public void ClearFrames() => Frames.Clear();
    }
}