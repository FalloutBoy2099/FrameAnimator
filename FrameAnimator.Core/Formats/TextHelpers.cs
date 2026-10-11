namespace FrameAnimator.Core.Formats
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>Émulation de <c>TStringList.Values[]</c> de la VCL (noms insensibles à la casse, première occurrence).</summary>
    public sealed class KeyValueList
    {
        private readonly List<String> _lines = [];

        public IReadOnlyList<String> Lines => _lines;

        public void Add(String line) => _lines.Add(line);
        public void Add(String name, String value) => _lines.Add(name + "=" + value);

        /// <summary>Valeur de <paramref name="name"/> ou chaîne vide (comme <c>Values[name]</c>).</summary>
        public String this[String name]
        {
            get
            {
                foreach (var line in _lines)
                {
                    Int32 p = line.IndexOf('=');
                    if (p > 0 && String.Equals(line[..p], name, StringComparison.OrdinalIgnoreCase))
                        return line[(p + 1)..];
                }
                return "";
            }
        }

        public static KeyValueList Parse(String text)
        {
            var l = new KeyValueList();
            using var reader = new StringReader(text);
            String? line;
            while ((line = reader.ReadLine()) is not null)
                l.Add(line);
            return l;
        }

        public override String ToString() => String.Join("\r\n", _lines) + (_lines.Count > 0 ? "\r\n" : "");
    }

    /// <summary>Encodage ANSI utilisé par l'original pour les fichiers texte (FPR, FAS).</summary>
    public static class AnsiText
    {
        private static readonly Lazy<Encoding> AnsiEncoding = new(() =>
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var cp = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
                return cp > 0 ? Encoding.GetEncoding(cp) : Encoding.GetEncoding(1252);
            }
            catch
            {
                return Encoding.Latin1;
            }
        });

        public static Encoding Encoding => AnsiEncoding.Value;

        /// <summary>Lit un fichier texte : UTF‑8 (avec ou sans BOM) si valide, sinon page de code ANSI.</summary>
        public static String ReadFile(String path)
        {
            var bytes = File.ReadAllBytes(path);
            try
            {
                return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('﻿');
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetString(bytes);
            }
        }

        public static void WriteFile(String path, String text) => File.WriteAllText(path, text, Encoding);
    }

    /// <summary>Fonctions de texte propres à l'original.</summary>
    public static class Csv
    {
        /// <summary>
        /// <c>field(s, n)</c> (0x4026c0) : saute <paramref name="n"/> virgules, prend jusqu'à la suivante, puis <c>Trim</c>.
        /// Un champ absent donne une chaîne vide.
        /// </summary>
        public static String Field(String s, Int32 n)
        {
            for (var i = 0; i < n; i++)
            {
                var p = s.IndexOf(',');
                if (p < 0)
                    return "";
                s = s[(p + 1)..];
            }

            var end = s.IndexOf(',');
            if (end >= 0)
                s = s[..end];

            return s.Trim();
        }
    }
}