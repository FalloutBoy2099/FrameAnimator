namespace FrameAnimator.Core.Model
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Une image de l'animation (interface <c>IFrame</c> de l'original, 0x1c octets).
    /// </summary>
    public sealed class Frame(Bitmap32 bitmap)
    {
        public const UInt16 NoSourceIndex = 0xFFFF;

        public Bitmap32 Bitmap { get; set; } = bitmap;

        /// <summary>
        /// Rectangle « utile » (résultat de l'auto‑coupe) : X gauche, Y haut, largeur, hauteur. Les champs sont des
        /// <c>int16</c> dans l'original ; ils sont recalculés par <see cref="Geometry.FrameMath.AutoCut"/>.
        /// </summary>
        public Int16 Left { get; set; }
        public Int16 Top { get; set; }
        public Int16 Width { get; set; } = (Int16)bitmap.Width;
        public Int16 Height { get; set; } = (Int16)bitmap.Height;

        /// <summary>Décalage ABSOLU de la frame (voir spécification §5.4).</summary>
        public Int32 OffsetX { get; set; }
        public Int32 OffsetY { get; set; }

        /// <summary>Rang de la sous‑image dans un GIF ; <see cref="NoSourceIndex"/> (0xFFFF) pour BMP/PNG/FRM.</summary>
        public UInt16 SourceIndex { get; set; } = NoSourceIndex;

        /// <summary>Chemin du fichier image source (peut ne pas exister pour une frame issue d'un FRM).</summary>
        public String FileName { get; set; } = "";

        public Boolean HasSourceIndex => SourceIndex != NoSourceIndex;

        /// <summary>Remet le rectangle utile à l'image entière.</summary>
        public void ResetRectangle()
        {
            Left = 0;
            Top = 0;
            Width = (Int16)Bitmap.Width;
            Height = (Int16)Bitmap.Height;
        }
    }

    /// <summary>
    /// Collection de frames d'une direction (interface <c>IFrames</c> de l'original, 0x14 octets).
    /// </summary>
    public sealed class Direction
    {
        public List<Frame> Frames { get; } = [];

        /// <summary>« Frames Offset » interne : voir <see cref="Geometry.FrameMath.GetShift"/>/<c>SetShift</c> (octets +4/+8).</summary>
        public Int32 ShiftBaseX { get; set; }
        public Int32 ShiftBaseY { get; set; }

        /// <summary>Décalage écrit pour la première frame dans le FRM (octets +0xc/+0x10).</summary>
        public Int32 FirstFrameOffsetX { get; set; }
        public Int32 FirstFrameOffsetY { get; set; }

        public Int32 Count => Frames.Count;

        public void Clear()
        {
            Frames.Clear();
            ShiftBaseX = ShiftBaseY = FirstFrameOffsetX = FirstFrameOffsetY = 0;
        }

        /// <summary>Vide uniquement les frames (comportement de <c>IFrames::Clear</c> 0x4019f0 utilisé par REMOVE_FRAMES).</summary>
        public void ClearFrames() => Frames.Clear();
    }
}