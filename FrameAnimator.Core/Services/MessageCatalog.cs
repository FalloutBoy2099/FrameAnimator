namespace FrameAnimator.Core.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text.Json;

    /// <summary>
    /// Translations of the core messages (<see cref="CoreMessages"/>), embedded as <c>Messages/&lt;language&gt;.json</c> where the
    /// key is the English format string. The graphical interface and the console mode both use it; a message that has no
    /// translation (or a language that has no file, such as English) is returned unchanged.
    /// </summary>
    public static class MessageCatalog
    {
        private static readonly ConcurrentDictionary<String, IReadOnlyDictionary<String, String>> Dictionaries = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Translates a format string into <paramref name="language"/> (two-letter code), or returns it unchanged.</summary>
        public static String Translate(String format, String language)
            => Load(language).TryGetValue(format, out String? translated) ? translated : format;

        /// <summary>The whole dictionary of a language (empty when there is none).</summary>
        public static IReadOnlyDictionary<String, String> Load(String language)
            => Dictionaries.GetOrAdd(language, code =>
            {
                String resource = "FrameAnimator.Core.Messages." + code.ToLowerInvariant() + ".json";
                using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
                return stream is null
                    ? new Dictionary<String, String>()
                    : JsonSerializer.Deserialize<Dictionary<String, String>>(stream) ?? [];
            });
    }
}
