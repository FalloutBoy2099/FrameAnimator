namespace FrameAnimator.Core.Model
{
    using System;

    /// <summary>Mode de détermination de la couleur transparente (octet <c>+0x884</c> de l'original).</summary>
    public enum TransparentMode : Byte
    {
        /// <summary>Pixel (0,0) de chaque frame.</summary>
        Auto = 0,
        /// <summary>Entrée 0 de la palette.</summary>
        Palette = 1,
        /// <summary>Couleur d'échantillon choisie par l'utilisateur.</summary>
        Sample = 3,
    }

    /// <summary>Métrique de recherche de la couleur palette la plus proche (octet <c>+0x887</c>).</summary>
    public enum DitherMethod : Byte
    {
        /// <summary>Distance euclidienne dR²+dG²+dB².</summary>
        Default = 0,
        /// <summary>Distance pondérée 30·dR² + 59·dG² + 11·dB².</summary>
        Advanced = 1,
        /// <summary>Table RGB555 → index d'une palette Fallout (.PAL).</summary>
        Fallout = 2,
    }

    /// <summary>Réglages du projet. Valeurs initiales identiques à celles de l'original (copie depuis 0x4a14e6).</summary>
    public sealed class ProjectSettings
    {
        public TransparentMode TransparentMode { get; set; } = TransparentMode.Auto;
        public Boolean AutoCutting { get; set; } = true;
        /// <summary>Non persisté dans le FPR.</summary>
        public Boolean LockOffsets { get; set; } = true;
        public DitherMethod DitherMethod { get; set; } = DitherMethod.Default;
        /// <summary>Pas du frame « fantôme » (Ghost +/-). Non persisté dans le FPR.</summary>
        public Int16 GhostIncrement { get; set; } = -1;
        public UInt16 Fps { get; set; }
        public UInt16 ActionFrame { get; set; }
        /// <summary>
        /// <c>true</c> (défaut) : reproduit le bug de l'original dans la recherche de la couleur la plus proche (différences
        /// non signées, voir spécification §5.3) pour obtenir des FRM identiques octet pour octet. <c>false</c> : distance correcte.
        /// Non persisté dans le FPR.
        /// </summary>
        public Boolean LegacyColorMatching { get; set; } = true;

        /// <summary>Couleur d'échantillon 0xRRGGBB (défaut blanc).</summary>
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