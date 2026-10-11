namespace FrameAnimator.App.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Préférences de l'utilisateur propres à l'application (thème, langue, derniers dossiers, favoris) : lignes « clé=valeur »
    /// dans <c>%LOCALAPPDATA%\FrameAnimator\settings.txt</c>. Une clé peut apparaître plusieurs fois (liste de valeurs).
    /// </summary>
    public static class UserSettings
    {
        /// <summary>Dossier du fichier de préférences ; seuls les tests le changent (le développement utilise <c>FRAMEANIMATOR_SETTINGS</c>).</summary>
        internal static String? DirectoryOverride { get; set; }

        private static String FilePath => Path.Combine(
            DirectoryOverride ?? Environment.GetEnvironmentVariable("FRAMEANIMATOR_SETTINGS") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FrameAnimator"),
            "settings.txt");

        public static String? Get(String key) => GetAll(key).FirstOrDefault();

        public static IReadOnlyList<String> GetAll(String key)
            => Read().Where(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Select(entry => entry.Value).ToList();

        /// <summary>Mémorise une valeur unique pour la clé.</summary>
        public static void Set(String key, String value) => SetAll(key, [value]);

        /// <summary>Remplace toutes les valeurs de la clé ; les autres clés sont conservées telles quelles.</summary>
        public static void SetAll(String key, IEnumerable<String> values)
        {
            List<(String Key, String Value)> entries = Read();
            Int32 position = entries.FindIndex(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            entries.RemoveAll(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            entries.InsertRange(position < 0 ? entries.Count : position, values.Select(value => (key, value)));
            Write(entries);
        }

        private static List<(String Key, String Value)> Read()
        {
            List<(String Key, String Value)> entries = [];
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (String line in File.ReadAllLines(FilePath))
                    {
                        Int32 separator = line.IndexOf('=');
                        if (separator > 0 && separator < line.Length - 1)
                            entries.Add((line[..separator].Trim(), line[(separator + 1)..].Trim()));
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // un fichier illisible équivaut à aucune préférence
            }
            return entries;
        }

        private static void Write(List<(String Key, String Value)> entries)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllLines(FilePath, entries.Select(entry => entry.Key + "=" + entry.Value));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // les préférences ne sont qu'un confort : ne pas pouvoir les mémoriser n'est pas une erreur
            }
        }
    }
}