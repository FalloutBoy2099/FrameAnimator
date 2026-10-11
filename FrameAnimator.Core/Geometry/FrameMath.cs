namespace FrameAnimator.Core.Geometry
{
    using System;
    using Model;

    /// <summary>
    /// Formules de position/décalage reprises à l'identique de l'original (spécification §5.1 à §5.4).
    /// Toutes les divisions par 2 sont des divisions entières tronquées vers 0 (comme <c>sar</c>+<c>adc</c>).
    /// </summary>
    public static class FrameMath
    {
        /// <summary>Couleur transparente d'une frame selon le mode (0x403470).</summary>
        public static Int32 TransparentColor(Project project, Frame frame)
        {
            return project.Settings.TransparentMode switch
            {
                TransparentMode.Auto => frame.Bitmap.Pixels.Length == 0 ? 0 : frame.Bitmap.Pixels[0] & 0xFFFFFF,
                TransparentMode.Palette => project.Palette.Colors[0],
                TransparentMode.Sample => project.Settings.SampleColor,
                _ => 0,// valeur indéfinie dans l'original (mode inatteignable)
            };
        }

        /// <summary>
        /// Auto‑coupe (0x4036f0) : calcule et mémorise le rectangle utile de la frame. Les comparaisons se font sur 24 bits.
        /// Une frame entièrement transparente donne volontairement les mêmes valeurs dégénérées que l'original
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

        /// <summary>Centre horizontal du rectangle utile par rapport au centre du bitmap (sans décalage).</summary>
        public static Int32 CenterX(Frame f) => f.Left + f.Width / 2 - f.Bitmap.Width / 2;

        /// <summary>Bord bas du rectangle utile par rapport au bas du bitmap (sans décalage).</summary>
        public static Int32 CenterY(Frame f) => f.Top + f.Height - f.Bitmap.Height;

        public static Int32 PositionX(Frame f) => CenterX(f) + f.OffsetX;
        public static Int32 PositionY(Frame f) => CenterY(f) + f.OffsetY;

        /// <summary>Décalage relatif de <paramref name="cur"/> par rapport à <paramref name="prev"/> (0x403550) — valeur écrite dans le FRM.</summary>
        public static (Int32 X, Int32 Y) Relative(Frame prev, Frame cur)
            => (PositionX(cur) - PositionX(prev), PositionY(cur) - PositionY(prev));

        /// <summary>Place <paramref name="cur"/> à <paramref name="x"/>,<paramref name="y"/> de <paramref name="prev"/> (0x4034a4).</summary>
        public static void SetRelative(Frame prev, Frame cur, Int32 x, Int32 y)
        {
            cur.OffsetX = PositionX(prev) + x - CenterX(cur);
            cur.OffsetY = PositionY(prev) + y - CenterY(cur);
        }

        /// <summary>Décalage de direction écrit dans l'en‑tête FRM (0x403678). Exige au moins une frame.</summary>
        public static (Int32 X, Int32 Y) GetShift(Direction dir)
        {
            var f0 = dir.Frames[0];
            return (dir.ShiftBaseX + CenterX(f0), dir.ShiftBaseY + CenterY(f0));
        }

        /// <summary>Inverse de <see cref="GetShift"/> (0x403610).</summary>
        public static void SetShift(Direction dir, Int32 x, Int32 y)
        {
            var f0 = dir.Frames[0];
            dir.ShiftBaseX = x - CenterX(f0);
            dir.ShiftBaseY = y - CenterY(f0);
        }

        /// <summary>
        /// Offset de la frame <paramref name="index"/> à l'écran, relativement au point d'ancrage du centre de la zone de dessin
        /// (formule de <c>pbxFramePaint</c>, spécification §5.4 « Affichage »). Retourne le coin haut‑gauche du bitmap complet.
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