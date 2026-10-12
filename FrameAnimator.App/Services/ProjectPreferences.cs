namespace FrameAnimator.App.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>What the user chose about how projects are saved and reopened, kept from one run to the next.</summary>
    public interface IProjectPreferences
    {
        /// <summary>Write the paths of a project file relative to its folder, so that the project and its images can be moved together.</summary>
        Boolean SaveRelativePaths { get; set; }

        /// <summary>The projects opened or saved last, the most recent first.</summary>
        IReadOnlyList<String> RecentFiles { get; }

        /// <summary>Puts the file at the top of <see cref="RecentFiles"/> (once), dropping the oldest ones.</summary>
        void AddRecentFile(String path);

        void RemoveRecentFile(String path);

        void ClearRecentFiles();
    }

    /// <summary><see cref="IProjectPreferences"/> stored in an <see cref="ISettingsStore"/>.</summary>
    public sealed class ProjectPreferences(ISettingsStore settings) : IProjectPreferences
    {
        /// <summary>Number of recent files kept.</summary>
        public const Int32 MaxRecentFiles = 8;

        private const String RelativeKey = "relativePaths";
        private const String RecentKey = "recent";

        public Boolean SaveRelativePaths
        {
            get => settings.Get(RelativeKey) == "1";
            set => settings.Set(RelativeKey, value ? "1" : "0");
        }

        public IReadOnlyList<String> RecentFiles => settings.GetAll(RecentKey);

        public void AddRecentFile(String path)
        {
            List<String> files = RecentFiles.Where(file => !SamePath(file, path)).ToList();
            files.Insert(0, path);
            settings.SetAll(RecentKey, files.Take(MaxRecentFiles));
        }

        public void RemoveRecentFile(String path)
            => settings.SetAll(RecentKey, RecentFiles.Where(file => !SamePath(file, path)));

        public void ClearRecentFiles() => settings.SetAll(RecentKey, []);

        private static Boolean SamePath(String a, String b) => String.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
