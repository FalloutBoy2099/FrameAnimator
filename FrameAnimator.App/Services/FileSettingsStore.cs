namespace FrameAnimator.App.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// <see cref="ISettingsStore"/> kept in <c>%LOCALAPPDATA%\FrameAnimator\settings.txt</c> ("key=value" lines, a key may appear
    /// several times). A different folder can be given (tests) or set with the <c>FRAMEANIMATOR_SETTINGS</c> environment variable (development).
    /// </summary>
    public sealed class FileSettingsStore : ISettingsStore
    {
        private readonly String _filePath;

        public FileSettingsStore(String? directory = null)
        {
            _filePath = Path.Combine(
                directory ?? Environment.GetEnvironmentVariable("FRAMEANIMATOR_SETTINGS")
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FrameAnimator"),
                "settings.txt");
        }

        public String? Get(String key) => GetAll(key).FirstOrDefault();

        public IReadOnlyList<String> GetAll(String key)
            => Read().Where(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Select(entry => entry.Value).ToList();

        public void Set(String key, String value) => SetAll(key, [value]);

        public void SetAll(String key, IEnumerable<String> values)
        {
            List<(String Key, String Value)> entries = Read();
            Int32 position = entries.FindIndex(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            entries.RemoveAll(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            entries.InsertRange(position < 0 ? entries.Count : position, values.Select(value => (key, value)));
            Write(entries);
        }

        private List<(String Key, String Value)> Read()
        {
            List<(String Key, String Value)> entries = [];
            try
            {
                if (File.Exists(_filePath))
                {
                    foreach (String line in File.ReadAllLines(_filePath))
                    {
                        Int32 separator = line.IndexOf('=');
                        if (separator > 0 && separator < line.Length - 1)
                            entries.Add((line[..separator].Trim(), line[(separator + 1)..].Trim()));
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // an unreadable file is equivalent to no preferences
            }
            return entries;
        }

        private void Write(List<(String Key, String Value)> entries)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                File.WriteAllLines(_filePath, entries.Select(entry => entry.Key + "=" + entry.Value));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // preferences are only a convenience: being unable to store them is not an error
            }
        }
    }
}
