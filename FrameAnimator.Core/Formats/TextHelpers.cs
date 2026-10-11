namespace FrameAnimator.Core.Formats
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>Emulation of the VCL's <c>TStringList.Values[]</c> (case-insensitive names, first occurrence).</summary>
    public sealed class KeyValueList
    {
        private readonly List<String> _lines = [];

        public IReadOnlyList<String> Lines => _lines;

        public void Add(String line) => _lines.Add(line);
        public void Add(String name, String value) => _lines.Add(name + "=" + value);

        /// <summary>Value of <paramref name="name"/>, or an empty string (like <c>Values[name]</c>).</summary>
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

    /// <summary>ANSI encoding used by the original for text files (FPR, FAS).</summary>
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

        /// <summary>Reads a text file: UTF-8 (with or without BOM) if valid, otherwise the ANSI code page.</summary>
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

    /// <summary>Text functions specific to the original.</summary>
    public static class Csv
    {
        /// <summary>
        /// <c>field(s, n)</c> (0x4026c0) : skips <paramref name="n"/> commas, takes up to the next one, then <c>Trim</c>.
        /// A missing field yields an empty string.
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