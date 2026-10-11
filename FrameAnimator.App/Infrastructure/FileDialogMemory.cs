namespace FrameAnimator.App.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// What the file dialogs remember from one use to the next: the last folder used (one for opening, another one
    /// for saving) and the list of favorite folders. Everything is stored in <see cref="UserSettings"/>.
    /// </summary>
    public static class FileDialogMemory
    {
        private const String LastOpenKey = "lastOpenDirectory";
        private const String LastSaveKey = "lastSaveDirectory";
        private const String FavoriteKey = "favorite";

        /// <summary>Last folder used, if it still exists; <c>null</c> otherwise.</summary>
        public static String? GetLastDirectory(Boolean save)
        {
            String? directory = UserSettings.Get(save ? LastSaveKey : LastOpenKey);
            return directory is not null && Directory.Exists(directory) ? directory : null;
        }

        public static void SetLastDirectory(Boolean save, String directory)
            => UserSettings.Set(save ? LastSaveKey : LastOpenKey, directory);

        public static IReadOnlyList<String> GetFavorites() => UserSettings.GetAll(FavoriteKey);

        public static Boolean IsFavorite(String directory)
            => GetFavorites().Any(favorite => SamePath(favorite, directory));

        public static void AddFavorite(String directory)
        {
            List<String> favorites = GetFavorites().ToList();
            if (!favorites.Any(favorite => SamePath(favorite, directory)))
            {
                favorites.Add(directory);
                UserSettings.SetAll(FavoriteKey, favorites);
            }
        }

        public static void RemoveFavorite(String directory)
        {
            List<String> favorites = GetFavorites().Where(favorite => !SamePath(favorite, directory)).ToList();
            UserSettings.SetAll(FavoriteKey, favorites);
        }

        private static Boolean SamePath(String a, String b)
            => String.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }
}