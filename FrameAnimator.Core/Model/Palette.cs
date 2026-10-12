namespace FrameAnimator.Core.Model
{
    using System;
    using System.IO;
    using System.Reflection;
    using FrameAnimator.Core.Services;

    public sealed class PaletteLoadException(UserMessage detail, Exception? inner = null) : Exception(detail.ToString(), inner)
    {
        /// <summary>The message to show to the user (translatable).</summary>
        public UserMessage Detail { get; } = detail;
    }

    /// <summary>Unknown palette extension (the original displays<c>Unknown palette type '*.EXT'!</c>).</summary>
    public sealed class UnknownPaletteTypeException(String extension) : Exception(new UserMessage(CoreMessages.UnknownPaletteType, extension).ToString())
    {
        public String Extension { get; } = extension;

        /// <summary>The message to show to the user (translatable).</summary>
        public UserMessage Detail { get; } = new(CoreMessages.UnknownPaletteType, extension);
    }

    /// <summary>
    /// 256-color palette of <c>0x00RRGGBB</c> entries (R in the high byte), optionally accompanied by the Fallout table of
    /// 32,768 bytes (RGB555 → index) read from a .PAL file. Reproduces <c>LoadPalette</c> (0x402ca4).
    /// </summary>
    public sealed class Palette
    {
        public const Int32 Size = 256;
        public const Int32 FalloutTableSize = 0x8000;

        public Int32[] Colors { get; } = new Int32[Size];

        /// <summary><c>null</c> for an .ACT palette.</summary>
        public Byte[]? FalloutTable { get; private set; }

        /// <summary>Path of the original file (empty for the built-in palette).</summary>
        public String FileName { get; set; } = "";

        public Int32 this[Int32 index] => Colors[index];

        public Palette Clone()
        {
            var p = new Palette { FileName = FileName, FalloutTable = (Byte[]?)FalloutTable?.Clone() };
            Array.Copy(Colors, p.Colors, Size);
            return p;
        }

        /// <summary>Default palette (embedded resource identical to Default.act).</summary>
        public static Palette LoadDefault()
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("FrameAnimator.Core.Default.act")
                ?? throw new InvalidOperationException("Ressource Default.act introuvable.");
            var p = LoadAct(s);
            return p;
        }

        /// <summary>Loads according to the extension (.ACT / .PAL, case-insensitive).</summary>
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
                throw new PaletteLoadException(new UserMessage(CoreMessages.UnableToLoadPalette, Path.GetFileName(path)), e);
            }
        }

        /// <summary>
        /// .ACT: 256 × (R,G,B). Missing bytes (truncated file) reuse the last value read, like the original,
        /// whose read buffer is not reset.
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
        /// Fallout .PAL: 256 × (R,G,B) on 6 bits (×4, truncated to 8 bits) followed by 32,768 bytes of RGB555 → index table.
        /// Entry 0 is shifted only if its value is &lt; 0x40.
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