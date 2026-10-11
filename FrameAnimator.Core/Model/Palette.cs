namespace FrameAnimator.Core.Model
{
    using System;
    using System.IO;
    using System.Reflection;

    public sealed class PaletteLoadException(String message, Exception? inner = null) : Exception(message, inner);

    /// <summary>Extension de palette inconnue (l'original affiche <c>Unknown palette type '*.EXT'!</c>).</summary>
    public sealed class UnknownPaletteTypeException(String extension) : Exception($"Unknown palette type '*{extension}'!")
    {
        public String Extension { get; } = extension;
    }

    /// <summary>
    /// Palette de 256 couleurs <c>0x00RRGGBB</c> (R en poids fort), éventuellement accompagnée de la table Fallout de
    /// 32 768 octets (RGB555 → index) lue dans un fichier .PAL. Reproduit <c>LoadPalette</c> (0x402ca4).
    /// </summary>
    public sealed class Palette
    {
        public const Int32 Size = 256;
        public const Int32 FalloutTableSize = 0x8000;

        public Int32[] Colors { get; } = new Int32[Size];

        /// <summary><c>null</c> pour une palette .ACT.</summary>
        public Byte[]? FalloutTable { get; private set; }

        /// <summary>Chemin du fichier d'origine (vide pour la palette interne).</summary>
        public String FileName { get; set; } = "";

        public Int32 this[Int32 index] => Colors[index];

        public Palette Clone()
        {
            var p = new Palette { FileName = FileName, FalloutTable = (Byte[]?)FalloutTable?.Clone() };
            Array.Copy(Colors, p.Colors, Size);
            return p;
        }

        /// <summary>Palette par défaut (ressource embarquée identique à Default.act).</summary>
        public static Palette LoadDefault()
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("FrameAnimator.Core.Default.act")
                ?? throw new InvalidOperationException("Ressource Default.act introuvable.");
            var p = LoadAct(s);
            return p;
        }

        /// <summary>Charge selon l'extension (.ACT / .PAL, insensible à la casse).</summary>
        public static Palette LoadFromFile(String path)
        {
            String rawExt = Path.GetExtension(path);
            String ext = rawExt.ToUpperInvariant();
            try
            {
                if (ext is not (".ACT" or ".PAL"))
                    throw new UnknownPaletteTypeException(rawExt);
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return ext == ".ACT" ? LoadAct(fs, path) : LoadPal(fs, path);
            }
            catch (UnknownPaletteTypeException) { throw; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new PaletteLoadException($"Unable to load palette '{Path.GetFileName(path)}'!", e);
            }
        }

        /// <summary>
        /// .ACT : 256 × (R,G,B). Les octets manquants (fichier tronqué) reprennent la dernière valeur lue, comme l'original
        /// dont le tampon de lecture n'est pas réinitialisé.
        /// </summary>
        public static Palette LoadAct(Stream stream, String fileName = "")
        {
            var p = new Palette { FileName = fileName };
            Int32 last = 0;
            for (Int32 i = 0; i < Size; i++)
            {
                Int32 c = 0;
                for (Int32 k = 0; k < 3; k++)
                {
                    Int32 b = stream.ReadByte();
                    if (b >= 0)
                        last = b;
                    c = (c << 8) + last;
                }
                p.Colors[i] = c;
            }
            return p;
        }

        /// <summary>
        /// .PAL Fallout : 256 × (R,G,B) sur 6 bits (×4, troncature sur 8 bits) puis 32 768 octets de table RGB555 → index.
        /// L'entrée 0 n'est décalée que si sa valeur est &lt; 0x40.
        /// </summary>
        public static Palette LoadPal(Stream stream, String fileName = "")
        {
            var p = new Palette { FileName = fileName };
            Int32 last = 0;
            for (Int32 i = 0; i < Size; i++)
            {
                Int32 c = 0;
                for (Int32 k = 0; k < 3; k++)
                {
                    Int32 b = stream.ReadByte();
                    if (b >= 0)
                        last = b;
                    Int32 v = last;
                    if (i > 0 || v < 0x40)
                        v = (v << 2) & 0xFF;
                    c = (c << 8) + v;
                }
                p.Colors[i] = c;
            }
            var table = new Byte[FalloutTableSize];
            Byte lastTable = 0;
            for (Int32 i = 0; i < table.Length; i++)
            {
                Int32 b = stream.ReadByte();
                if (b >= 0)
                    lastTable = (Byte)b;
                table[i] = lastTable;
            }
            p.FalloutTable = table;
            return p;
        }
    }
}