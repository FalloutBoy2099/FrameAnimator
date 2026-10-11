namespace FrameAnimator.Core.Model
{
    using System;

    /// <summary>How the transparent color is determined (byte <c>+0x884</c> of the original).</summary>
    public enum TransparentMode : Byte
    {
        /// <summary>Pixel (0,0) of each frame.</summary>
        Auto = 0,
        /// <summary>Palette entry 0.</summary>
        Palette = 1,
        /// <summary>Sample color chosen by the user.</summary>
        Sample = 3,
    }

    /// <summary>Metric used to search for the closest palette color (byte <c>+0x887</c>).</summary>
    public enum DitherMethod : Byte
    {
        /// <summary>Euclidean distance dR²+dG²+dB².</summary>
        Default = 0,
        /// <summary>Weighted distance 30·dR² + 59·dG² + 11·dB².</summary>
        Advanced = 1,
        /// <summary>RGB555 → index table of a Fallout palette (.PAL).</summary>
        Fallout = 2,
    }

    /// <summary>Project settings. Initial values identical to those of the original (copied from 0x4a14e6).</summary>
    public sealed class ProjectSettings
    {
        public TransparentMode TransparentMode { get; set; } = TransparentMode.Auto;
        public Boolean AutoCutting { get; set; } = true;
        /// <summary>Not persisted in the FPR.</summary>
        public Boolean LockOffsets { get; set; } = true;
        public DitherMethod DitherMethod { get; set; } = DitherMethod.Default;
        /// <summary>Step of the "ghost" frame (Ghost +/-). Not persisted in the FPR.</summary>
        public Int16 GhostIncrement { get; set; } = -1;
        public UInt16 Fps { get; set; }
        public UInt16 ActionFrame { get; set; }
        /// <summary>
        /// <c>true</c> (default): reproduces the original's bug in the closest-color search (unsigned differences,
        /// see specification §5.3) to obtain byte-for-byte identical FRMs. <c>false</c>: correct distance.
        /// Not persisted in the FPR.
        /// </summary>
        public Boolean LegacyColorMatching { get; set; } = true;

        /// <summary>Sample color 0xRRGGBB (default white).</summary>
        public Int32 SampleColor { get; set; } = 0xFFFFFF;

        public void ResetToDefaults()
        {
            TransparentMode = TransparentMode.Auto;
            AutoCutting = true;
            LockOffsets = true;
            DitherMethod = DitherMethod.Default;
            GhostIncrement = -1;
            Fps = 0;
            ActionFrame = 0;
            SampleColor = 0xFFFFFF;
        }
    }
}