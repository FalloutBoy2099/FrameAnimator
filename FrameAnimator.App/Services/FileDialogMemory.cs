namespace FrameAnimator.App.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// What the file dialogs remember from one use to the next: the last folder used (one for opening, another one
    /// for saving) and the list of favorite folders.
    /// </summary>
    public interface IFileDialogMemory
    {
        /// <summary>Last folder used, if it still exists; <c>null</c> otherwise.</summary>
        String? GetLastDirectory(Boolean save);

        void SetLastDirectory(Boolean save, String directory);

        IReadOnlyList<String> GetFavorites();

        Boolean IsFavorite(String directory);

        void AddFavorite(String directory);

        void RemoveFavorite(String directory);
    }

    /// <summary><see cref="IFileDialogMemory"/> stored in an <see cref="ISettingsStore"/>.</summary>
    public sealed class FileDialogMemory(ISettingsStore settings) : IFileDialogMemory
    {
        private const String LastOpenKey = "lastOpenDirectory";
        private const String LastSaveKey = "lastSaveDirectory";
        private const String FavoriteKey = "favorite";

        public String? GetLastDirectory(Boolean save)
        {
            String? directory = settings.Get(save ? LastSaveKey : LastOpenKey);
            return directory is not null && Directory.Exists(directory) ? directory : null;
        }

        public void SetLastDirectory(Boolean save, String directory)
            => settings.Set(save ? LastSaveKey : LastOpenKey, directory);

        public IReadOnlyList<String> GetFavorites() => settings.GetAll(FavoriteKey);

        public Boolean IsFavorite(String directory)
            => GetFavorites().Any(favorite => SamePath(favorite, directory));

        public void AddFavorite(String directory)
        {
            List<String> favorites = GetFavorites().ToList();
            if (!favorites.Any(favorite => SamePath(favorite, directory)))
            {
                favorites.Add(directory);
                settings.SetAll(FavoriteKey, favorites);
            }
        }

        public void RemoveFavorite(String directory)
        {
            List<String> favorites = GetFavorites().Where(favorite => !SamePath(favorite, directory)).ToList();
            settings.SetAll(FavoriteKey, favorites);
        }

        private static Boolean SamePath(String a, String b)
            => String.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }
}
