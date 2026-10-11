namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Windows.Controls;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Infrastructure;
    using Localization;

    /// <summary>Parameters of the application's file dialog (open or save).</summary>
    public sealed record FileDialogOptions
    {
        /// <summary>Save dialog (one file, name may not exist yet) instead of open.</summary>
        public Boolean Save { get; init; }

        /// <summary>Title of the window (translated by the view model).</summary>
        public String Title { get; init; } = "";

        /// <summary>Filters in the <c>Name (*.ext)|*.ext|…</c> format; names are displayed as given.</summary>
        public String Filter { get; init; } = "";

        public Boolean Multiselect { get; init; }

        /// <summary>Initial file name, with or without folder (save).</summary>
        public String? FileName { get; init; }

        /// <summary>Extension appended to a typed name that has none (save).</summary>
        public String DefaultExt { get; init; } = "";

        /// <summary>1-based index of the initially selected filter.</summary>
        public Int32 FilterIndex { get; init; } = 1;

        /// <summary>Asks before replacing an existing file (save).</summary>
        public Boolean OverwritePrompt { get; init; }

        /// <summary>The chosen files must exist (open).</summary>
        public Boolean MustExist { get; init; }

        public static FileDialogOptions ForOpen(String title, String filter, Boolean multiselect)
            => new() { Title = title, Filter = filter, Multiselect = multiselect, MustExist = true };

        public static FileDialogOptions ForSave(String title, String filter, Int32 filterIndex, String? fileName, String defaultExt, Boolean overwritePrompt)
            => new()
            {
                Save = true,
                Title = title,
                Filter = filter,
                FilterIndex = filterIndex,
                FileName = fileName,
                DefaultExt = defaultExt,
                OverwritePrompt = overwritePrompt,
            };
    }

    /// <summary>A filter of the file type box: displayed name and file name patterns.</summary>
    public sealed record FilterEntry(String Display, String[] Patterns)
    {
        public override String ToString() => Display;
    }

    /// <summary>A place (special folder, drive) or a favorite folder.</summary>
    public sealed record PlaceEntry(String Label, String Path);

    /// <summary>A row of the file list.</summary>
    public sealed record FileEntry(String Name, String FullPath, Boolean IsDirectory, String Modified, String SizeText)
    {
        public String Glyph => IsDirectory ? "" : "";
    }

    /// <summary>
    /// View model of the application's own file picker (system dialogs do not follow the dark theme): navigation, filters,
    /// favorites, places, and the open / save rules. The list selection stays a view concern: the view forwards it with
    /// <see cref="SetSelectedEntries"/>.
    /// </summary>
    public sealed partial class FileDialogViewModel : DialogViewModelBase
    {
        private readonly FileDialogOptions _options;
        private readonly IMessageService _messages;
        private readonly Func<String, String, String, String?> _promptForText;
        private readonly Stack<String> _history = new();
        private IReadOnlyList<FileEntry> _selected = [];

        /// <param name="options">What kind of dialog to show.</param>
        /// <param name="messages">Warnings and questions.</param>
        /// <param name="promptForText">Asks for a line of text (title, prompt, initial text); <c>null</c> if canceled.</param>
        public FileDialogViewModel(FileDialogOptions options, IMessageService messages, Func<String, String, String, String?> promptForText)
        {
            _options = options;
            _messages = messages;
            _promptForText = promptForText;
            Title = Localizer.T(options.Title);
            OkText = Localizer.T(options.Save ? "Save" : "Open");

            List<FilterEntry> filters = ParseFilter(options.Filter).ToList();
            if (filters.Count == 0)
                filters.Add(new FilterEntry(Localizer.T("All") + " (*.*)", ["*.*"]));
            Filters = filters;
            SelectedFilter = filters[Math.Clamp(options.FilterIndex - 1, 0, filters.Count - 1)];

            Places = BuildPlaces().ToList();
            RefreshFavorites();

            String start = FileDialogMemory.GetLastDirectory(options.Save) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            String name = "";
            if (!String.IsNullOrEmpty(options.FileName))
            {
                String? directory = Path.GetDirectoryName(options.FileName);
                if (!String.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    start = directory;
                name = Path.GetFileName(options.FileName);
            }
            NameText = name;
            NavigateTo(start, record: false);
        }

        // ------------------------------------------------------------------ state

        public String Title { get; }

        /// <summary>Text of the default button ("Open" or "Save").</summary>
        public String OkText { get; }

        public Boolean Multiselect => _options.Multiselect;

        /// <summary>Selection mode of the file list.</summary>
        public SelectionMode SelectionMode => _options.Multiselect ? SelectionMode.Extended : SelectionMode.Single;

        public IReadOnlyList<FilterEntry> Filters { get; }

        public IReadOnlyList<PlaceEntry> Places { get; }

        public ObservableCollection<PlaceEntry> Favorites { get; } = [];

        /// <summary>The hint "click the star…" is shown while there is no favorite.</summary>
        [ObservableProperty]
        public partial Boolean ShowFavoritesHint { get; private set; }

        [ObservableProperty]
        public partial FilterEntry? SelectedFilter { get; set; }

        /// <summary>Folder being displayed (full path); empty if the start folder could not be opened.</summary>
        public String CurrentDirectory { get; private set; } = "";

        /// <summary>Content of the path box (edited by the user, reset to <see cref="CurrentDirectory"/> after navigation).</summary>
        [ObservableProperty]
        public partial String PathText { get; set; } = "";

        /// <summary>Content of the file name box.</summary>
        [ObservableProperty]
        public partial String NameText { get; set; } = "";

        [ObservableProperty]
        public partial IReadOnlyList<FileEntry> Entries { get; private set; } = [];

        /// <summary>Glyph of the star button: filled when the current folder is a favorite.</summary>
        [ObservableProperty]
        public partial String FavoriteGlyph { get; private set; } = "";

        /// <summary>Tooltip of the star button: what a click will do.</summary>
        [ObservableProperty]
        public partial String FavoriteToolTip { get; private set; } = "";

        /// <summary>Header of the file list's context menu entry (add or remove the targeted folder).</summary>
        [ObservableProperty]
        public partial String FileMenuFavoriteHeader { get; private set; } = "";

        /// <summary>The favorite targeted by a right click in the favorites list (for the "remove" entry); set by the view.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveFavoriteCommand))]
        public partial PlaceEntry? ContextFavorite { get; set; }

        /// <summary>Chosen files once the dialog was accepted.</summary>
        public String[] Result { get; private set; } = [];

        private Boolean CanGoBack => _history.Count > 0;

        private Boolean CanGoUp => CurrentDirectory.Length > 0 && Directory.GetParent(CurrentDirectory) is not null;

        private Boolean CanRemoveFavorite => ContextFavorite is not null;

        // ------------------------------------------------------------------ selection

        /// <summary>
        /// The view forwards the selected rows of the file list (in selection order). The file name box follows the selected
        /// files: one name, or several quoted names; folders and an empty selection leave it untouched.
        /// </summary>
        public void SetSelectedEntries(IReadOnlyList<FileEntry> selected)
        {
            _selected = selected;
            List<String> names = selected.Where(entry => !entry.IsDirectory).Select(entry => entry.Name).ToList();
            if (names.Count == 1)
                NameText = names[0];
            else if (names.Count > 1)
                NameText = String.Join(' ', names.Select(name => "\"" + name + "\""));
            UpdateFavoriteState();
        }

        /// <summary>The selected folder, if the (first) selected row is one.</summary>
        private FileEntry? SelectedFolder => _selected.Count > 0 && _selected[0].IsDirectory ? _selected[0] : null;

        partial void OnSelectedFilterChanged(FilterEntry? value)
        {
            if (CurrentDirectory.Length > 0)
                Reload();
        }

        // ------------------------------------------------------------------ filters and places

        private static IEnumerable<FilterEntry> ParseFilter(String filter)
        {
            String[] parts = filter.Split('|');
            for (Int32 i = 0; i + 1 < parts.Length; i += 2)
                yield return new FilterEntry(parts[i], parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        private static IEnumerable<PlaceEntry> BuildPlaces()
        {
            (String Label, Environment.SpecialFolder Folder)[] known =
            [
                ("Desktop", Environment.SpecialFolder.DesktopDirectory),
                ("Documents", Environment.SpecialFolder.MyDocuments),
                ("Pictures", Environment.SpecialFolder.MyPictures),
            ];
            foreach ((String label, Environment.SpecialFolder folder) in known)
            {
                String path = Environment.GetFolderPath(folder);
                if (path.Length > 0 && Directory.Exists(path))
                    yield return new PlaceEntry(Localizer.T(label), path);
            }
            String downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(downloads))
                yield return new PlaceEntry(Localizer.T("Downloads"), downloads);
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                Boolean ready;
                try
                {
                    ready = drive.IsReady;
                }
                catch (IOException)
                {
                    ready = false;
                }
                if (ready)
                {
                    String volume = "";
                    try
                    {
                        volume = drive.VolumeLabel;
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        // the volume label is optional
                    }
                    String letter = drive.Name.TrimEnd('\\');
                    yield return new PlaceEntry(volume.Length > 0 ? volume + " (" + letter + ")" : drive.Name, drive.Name);
                }
            }
        }

        private static Boolean Matches(String fileName, String[] patterns)
            => patterns.Any(pattern => Regex.IsMatch(fileName, "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase));

        // ------------------------------------------------------------------ navigation

        private void NavigateTo(String directory, Boolean record = true)
        {
            String full;
            try
            {
                full = Path.GetFullPath(directory);
                if (!Directory.Exists(full))
                    return;
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return;
            }

            if (record && CurrentDirectory.Length > 0 && !String.Equals(CurrentDirectory, full, StringComparison.OrdinalIgnoreCase))
                _history.Push(CurrentDirectory);
            CurrentDirectory = full;
            OnPropertyChanged(nameof(CurrentDirectory));
            PathText = full;
            UpdateFavoriteState();
            BackCommand.NotifyCanExecuteChanged();
            UpCommand.NotifyCanExecuteChanged();
            Reload();
        }

        private void Reload()
        {
            List<FileEntry> entries = [];
            FilterEntry? filter = SelectedFilter;
            try
            {
                DirectoryInfo info = new(CurrentDirectory);
                foreach (DirectoryInfo dir in info.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    if ((dir.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                        entries.Add(new FileEntry(dir.Name, dir.FullName, true, dir.LastWriteTime.ToString("g"), ""));
                }
                foreach (FileInfo file in info.EnumerateFiles().OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    if ((file.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0 && (filter is null || Matches(file.Name, filter.Patterns)))
                        entries.Add(new FileEntry(file.Name, file.FullName, false, file.LastWriteTime.ToString("g"), FormatSize(file.Length)));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // an unreadable folder simply appears empty
            }
            _selected = [];
            Entries = entries;
        }

        private static String FormatSize(Int64 bytes) => bytes < 1024 ? bytes + " B" : (bytes + 1023) / 1024 + " KB";

        [RelayCommand(CanExecute = nameof(CanGoBack))]
        private void Back()
        {
            if (_history.Count > 0)
                NavigateTo(_history.Pop(), record: false);
        }

        [RelayCommand(CanExecute = nameof(CanGoUp))]
        private void Up()
        {
            if (Directory.GetParent(CurrentDirectory) is { } parent)
                NavigateTo(parent.FullName);
        }

        /// <summary>Opens a folder (ignored if it does not exist).</summary>
        [RelayCommand]
        private void Navigate(String? path)
        {
            if (!String.IsNullOrEmpty(path))
                NavigateTo(path);
        }

        /// <summary>Opens the folder typed in the path box (Enter); the box then shows the folder actually displayed.</summary>
        [RelayCommand]
        private void NavigateToPath()
        {
            NavigateTo(PathText.Trim());
            PathText = CurrentDirectory;
        }

        /// <summary>Opens a place of the places list.</summary>
        [RelayCommand]
        private void OpenPlace(PlaceEntry? place)
        {
            if (place is not null)
                NavigateTo(place.Path);
        }

        /// <summary>Opens a favorite; a favorite whose folder disappeared is reported.</summary>
        [RelayCommand]
        private void OpenFavorite(PlaceEntry? favorite)
        {
            if (favorite is null)
                return;
            if (Directory.Exists(favorite.Path))
                NavigateTo(favorite.Path);
            else
                _messages.ShowWarning(Localizer.F("'{0}' was not found.", favorite.Path));
        }

        /// <summary>Double click on the (first) selected row: enters a folder, accepts a file.</summary>
        [RelayCommand]
        private void OpenSelected()
        {
            if (_selected.Count == 0)
                return;
            if (_selected[0].IsDirectory)
                NavigateTo(_selected[0].FullPath);
            else
                Ok();
        }

        // ------------------------------------------------------------------ favorites

        private static String FavoriteLabel(String path)
        {
            String trimmed = path.TrimEnd('\\', '/');
            String name = Path.GetFileName(trimmed);
            return name.Length > 0 ? name : path;
        }

        private void RefreshFavorites()
        {
            Favorites.Clear();
            foreach (String favorite in FileDialogMemory.GetFavorites())
                Favorites.Add(new PlaceEntry(FavoriteLabel(favorite), favorite));
            ShowFavoritesHint = Favorites.Count == 0;
            UpdateFavoriteState();
        }

        /// <summary>The star is filled when the current folder is a favorite; its tooltip says what a click will do.</summary>
        private void UpdateFavoriteState()
        {
            if (CurrentDirectory.Length == 0)
                return;
            Boolean favorite = FileDialogMemory.IsFavorite(CurrentDirectory);
            FavoriteGlyph = favorite ? "" : "";
            FavoriteToolTip = Localizer.T(favorite ? "Remove from favorites" : "Add to favorites");
            // "Add to favorites" only applies to a folder; with no targeted folder, it applies to the current folder
            FileMenuFavoriteHeader = Localizer.T(FileDialogMemory.IsFavorite(SelectedFolder?.FullPath ?? CurrentDirectory)
                ? "Remove from favorites"
                : "Add to favorites");
        }

        private void ToggleFavoriteOf(String path)
        {
            if (FileDialogMemory.IsFavorite(path))
                FileDialogMemory.RemoveFavorite(path);
            else
                FileDialogMemory.AddFavorite(path);
            RefreshFavorites();
        }

        /// <summary>Star button: adds or removes the current folder.</summary>
        [RelayCommand]
        private void ToggleFavorite() => ToggleFavoriteOf(CurrentDirectory);

        /// <summary>Entry of the file list's context menu: adds or removes the selected folder, or the current folder.</summary>
        [RelayCommand]
        private void AddToFavorites() => ToggleFavoriteOf(SelectedFolder?.FullPath ?? CurrentDirectory);

        /// <summary>Entry of the favorites' context menu: removes <see cref="ContextFavorite"/>.</summary>
        [RelayCommand(CanExecute = nameof(CanRemoveFavorite))]
        private void RemoveFavorite()
        {
            if (ContextFavorite is { } favorite)
            {
                FileDialogMemory.RemoveFavorite(favorite.Path);
                ContextFavorite = null;
                RefreshFavorites();
            }
        }

        // ------------------------------------------------------------------ new folder, refresh

        [RelayCommand]
        private void NewFolder()
        {
            String? name = _promptForText("New folder", "Folder name:", Localizer.T("New folder"));
            if (name is null)
                return;
            try
            {
                String path = Path.Combine(CurrentDirectory, name);
                Directory.CreateDirectory(path);
                NavigateTo(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                _messages.ShowWarning(exception.Message);
            }
        }

        [RelayCommand]
        private void Refresh() => Reload();

        // ------------------------------------------------------------------ validation

        private static IEnumerable<String> SplitNames(String text)
        {
            MatchCollection quoted = Regex.Matches(text, "\"([^\"]+)\"");
            if (quoted.Count > 0)
                return quoted.Select(match => match.Groups[1].Value);
            String trimmed = text.Trim();
            return trimmed.Length > 0 ? [trimmed] : [];
        }

        private String Resolve(String name)
        {
            try
            {
                return Path.GetFullPath(Path.IsPathRooted(name) ? name : Path.Combine(CurrentDirectory, name));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return Path.Combine(CurrentDirectory, name);
            }
        }

        /// <summary>Accepts the file name box: a folder navigates, otherwise the open / save rules apply.</summary>
        [RelayCommand]
        private void Ok()
        {
            List<String> names = SplitNames(NameText).ToList();
            if (names.Count == 0)
                return;

            // a folder was entered: navigate to it
            if (names.Count == 1 && Directory.Exists(Resolve(names[0])))
            {
                NavigateTo(Resolve(names[0]));
                NameText = "";
                return;
            }

            if (_options.Save)
            {
                String path = Resolve(names[0]);
                if (_options.DefaultExt.Length > 0 && Path.GetExtension(path).Length == 0)
                    path += "." + _options.DefaultExt.TrimStart('.');
                if (_options.OverwritePrompt && File.Exists(path)
                    && !_messages.AskYesNo(Localizer.F("'{0}' already exists.\nDo you want to replace it?", Path.GetFileName(path))))
                {
                    return;
                }
                Accept([path]);
                return;
            }

            if (names.Count > 1 && !_options.Multiselect)
                names = [names[0]];
            List<String> paths = names.Select(Resolve).ToList();
            String? missing = _options.MustExist ? paths.FirstOrDefault(path => !File.Exists(path)) : null;
            if (missing is not null)
            {
                _messages.ShowWarning(Localizer.F("'{0}' was not found.", Path.GetFileName(missing)));
                return;
            }
            Accept(paths);
        }

        private void Accept(IEnumerable<String> paths)
        {
            Result = paths.ToArray();
            if (Path.GetDirectoryName(Result[0]) is { Length: > 0 } directory && Directory.Exists(directory))
                FileDialogMemory.SetLastDirectory(_options.Save, directory);
            RequestClose(true);
        }
    }
}
