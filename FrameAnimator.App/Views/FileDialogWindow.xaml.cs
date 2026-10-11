namespace FrameAnimator.App.Views
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using Infrastructure;
    using Localization;

    /// <summary>
    /// File picker using the application's theme (system dialogs do not follow the dark theme):
    /// open (single or multiple) and save, with filters, navigation and overwrite confirmation.
    /// </summary>
    public partial class FileDialogWindow : Window
    {
        private sealed record FilterItem(String Display, String[] Patterns)
        {
            public override String ToString() => Display;
        }

        private sealed record PlaceItem(String Label, String Path);

        private sealed record Entry(String Name, String FullPath, Boolean IsDirectory, String Modified, String SizeText)
        {
            public String Glyph => IsDirectory ? "" : "";
        }

        private readonly Boolean _save;
        private readonly Boolean _multiselect;
        private readonly Boolean _overwritePrompt;
        private readonly Boolean _mustExist;
        private readonly String _defaultExt;
        private readonly Stack<String> _history = new();
        private String _directory = "";

        private FileDialogWindow(Boolean save, String title, String filter, Boolean multiselect, String? fileName,
            String defaultExt, Int32 filterIndex, Boolean overwritePrompt, Boolean mustExist)
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);
            _save = save;
            _multiselect = multiselect;
            _overwritePrompt = overwritePrompt;
            _mustExist = mustExist;
            _defaultExt = defaultExt;
            Title = Localizer.T(title);
            OkButton.Content = Localizer.T(save ? "Save" : "Open");
            FilesList.SelectionMode = multiselect ? SelectionMode.Extended : SelectionMode.Single;

            foreach (FilterItem item in ParseFilter(filter))
                FilterBox.Items.Add(item);
            if (FilterBox.Items.Count == 0)
                FilterBox.Items.Add(new FilterItem(Localizer.T("All") + " (*.*)", ["*.*"]));
            FilterBox.SelectedIndex = Math.Clamp(filterIndex - 1, 0, FilterBox.Items.Count - 1);

            foreach (PlaceItem place in BuildPlaces())
                PlacesList.Items.Add(place);
            RefreshFavorites();

            String start = FileDialogMemory.GetLastDirectory(save) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            String name = "";
            if (!String.IsNullOrEmpty(fileName))
            {
                String? directory = Path.GetDirectoryName(fileName);
                if (!String.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    start = directory;
                name = Path.GetFileName(fileName);
            }
            NameBox.Text = name;
            Navigate(start, record: false);
            Loaded += (_, _) =>
            {
                NameBox.Focus();
                NameBox.SelectAll();
            };
        }

        /// <summary>Open dialog; empty array if canceled.</summary>
        public static String[] ShowOpen(Window? owner, String title, String filter, Boolean multiselect)
        {
            FileDialogWindow dialog = new(false, title, filter, multiselect, null, "", 1, false, true);
            return dialog.Run(owner);
        }

        /// <summary>Save dialog; <c>null</c> if canceled.</summary>
        public static String? ShowSave(Window? owner, String title, String filter, Int32 filterIndex, String? fileName,
            String defaultExt, Boolean overwritePrompt = true)
        {
            FileDialogWindow dialog = new(true, title, filter, false, fileName, defaultExt, filterIndex, overwritePrompt, false);
            String[] result = dialog.Run(owner);
            return result.Length > 0 ? result[0] : null;
        }

        /// <summary>Development preview (<c>--dialog open</c> switch).</summary>
        internal static Window CreateForPreview() => new FileDialogWindow(false, "Open Project",
            DialogService.TranslateFilter("All (*.FPR,*.FRM)|*.FPR;*.FRM|Project (*.FPR)|*.FPR"), true, null, "", 1, false, true);

        private String[] Result { get; set; } = [];

        private String[] Run(Window? owner)
        {
            if (owner is { IsLoaded: true })
                Owner = owner;
            else
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowDialog();
            return Result;
        }

        // ------------------------------------------------------------------ filters and places

        private static IEnumerable<FilterItem> ParseFilter(String filter)
        {
            String[] parts = filter.Split('|');
            for (Int32 i = 0; i + 1 < parts.Length; i += 2)
                yield return new FilterItem(parts[i], parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        private static IEnumerable<PlaceItem> BuildPlaces()
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
                    yield return new PlaceItem(Localizer.T(label), path);
            }
            String downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(downloads))
                yield return new PlaceItem(Localizer.T("Downloads"), downloads);
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
                    yield return new PlaceItem(volume.Length > 0 ? volume + " (" + letter + ")" : drive.Name, drive.Name);
                }
            }
        }

        private static Boolean Matches(String fileName, String[] patterns)
            => patterns.Any(pattern => Regex.IsMatch(fileName, "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase));

        // ------------------------------------------------------------------ navigation

        private void Navigate(String directory, Boolean record = true)
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

            if (record && _directory.Length > 0 && !String.Equals(_directory, full, StringComparison.OrdinalIgnoreCase))
                _history.Push(_directory);
            _directory = full;
            PathBox.Text = full;
            UpdateFavoriteButton();
            BackButton.IsEnabled = _history.Count > 0;
            UpButton.IsEnabled = Directory.GetParent(full) is not null;
            Reload();
        }

        private void Reload()
        {
            List<Entry> entries = [];
            FilterItem? filter = FilterBox.SelectedItem as FilterItem;
            try
            {
                DirectoryInfo info = new(_directory);
                foreach (DirectoryInfo dir in info.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    if ((dir.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                        entries.Add(new Entry(dir.Name, dir.FullName, true, dir.LastWriteTime.ToString("g"), ""));
                }
                foreach (FileInfo file in info.EnumerateFiles().OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    if ((file.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0 && (filter is null || Matches(file.Name, filter.Patterns)))
                        entries.Add(new Entry(file.Name, file.FullName, false, file.LastWriteTime.ToString("g"), FormatSize(file.Length)));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // an unreadable folder simply appears empty
            }
            FilesList.ItemsSource = entries;
        }

        private static String FormatSize(Int64 bytes) => bytes < 1024 ? bytes + " B" : (bytes + 1023) / 1024 + " KB";

        private void OnBack(Object sender, RoutedEventArgs e)
        {
            if (_history.Count > 0)
                Navigate(_history.Pop(), record: false);
        }

        private void OnUp(Object sender, RoutedEventArgs e)
        {
            if (Directory.GetParent(_directory) is { } parent)
                Navigate(parent.FullName);
        }

        private void OnPathKeyDown(Object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Navigate(PathBox.Text.Trim());
                PathBox.Text = _directory;
                e.Handled = true;
            }
        }

        private void OnPlaceChanged(Object sender, SelectionChangedEventArgs e)
        {
            if (PlacesList.SelectedItem is PlaceItem place)
            {
                Navigate(place.Path);
                PlacesList.SelectedItem = null;
            }
        }

        private void OnFilterChanged(Object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded || _directory.Length > 0)
                Reload();
        }

        private void OnFileSelectionChanged(Object sender, SelectionChangedEventArgs e)
        {
            List<String> names = FilesList.SelectedItems.OfType<Entry>().Where(entry => !entry.IsDirectory).Select(entry => entry.Name).ToList();
            if (names.Count == 1)
                NameBox.Text = names[0];
            else if (names.Count > 1)
                NameBox.Text = String.Join(' ', names.Select(name => "\"" + name + "\""));
        }

        private void OnFileDoubleClick(Object sender, MouseButtonEventArgs e)
        {
            if (FilesList.SelectedItem is Entry entry)
            {
                if (entry.IsDirectory)
                    Navigate(entry.FullPath);
                else
                    OnOk(sender, e);
            }
        }

        private void OnFilesKeyDown(Object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && FilesList.SelectedItem is Entry { IsDirectory: true } entry)
            {
                Navigate(entry.FullPath);
                e.Handled = true;
            }
            else if (e.Key == Key.Back)
            {
                OnUp(sender, e);
                e.Handled = true;
            }
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
            FavoritesList.Items.Clear();
            foreach (String favorite in FileDialogMemory.GetFavorites())
                FavoritesList.Items.Add(new PlaceItem(FavoriteLabel(favorite), favorite));
            FavoritesHint.Visibility = FavoritesList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateFavoriteButton();
        }

        /// <summary>The star is filled when the current folder is a favorite; its tooltip says what a click will do.</summary>
        private void UpdateFavoriteButton()
        {
            if (_directory.Length == 0)
                return;
            Boolean favorite = FileDialogMemory.IsFavorite(_directory);
            FavoriteGlyph.Text = favorite ? "\uE735" : "\uE734";
            String tip = Localizer.T(favorite ? "Remove from favorites" : "Add to favorites");
            FavoriteButton.ToolTip = tip;
            System.Windows.Automation.AutomationProperties.SetName(FavoriteButton, tip);
        }

        private void OnToggleFavorite(Object sender, RoutedEventArgs e)
        {
            if (FileDialogMemory.IsFavorite(_directory))
                FileDialogMemory.RemoveFavorite(_directory);
            else
                FileDialogMemory.AddFavorite(_directory);
            RefreshFavorites();
        }

        private void OnFavoriteChanged(Object sender, SelectionChangedEventArgs e)
        {
            if (FavoritesList.SelectedItem is not PlaceItem favorite)
                return;
            FavoritesList.SelectedItem = null;
            if (Directory.Exists(favorite.Path))
                Navigate(favorite.Path);
            else
                MessageDialog.Show(this, Localizer.F("'{0}' was not found.", favorite.Path), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private PlaceItem? _contextFavorite;

        private void OnFavoritesRightClick(Object sender, MouseButtonEventArgs e)
        {
            // the favorite targeted by the right click (without selecting it: a selection would open the folder)
            _contextFavorite = e.OriginalSource is DependencyObject source
                && ItemsControl.ContainerFromElement(FavoritesList, source) is ListBoxItem { DataContext: PlaceItem favorite }
                ? favorite
                : null;
        }

        private void OnFavoritesMenuOpened(Object sender, RoutedEventArgs e) => RemoveFavoriteMenu.IsEnabled = _contextFavorite is not null;

        private void OnRemoveFavorite(Object sender, RoutedEventArgs e)
        {
            if (_contextFavorite is { } favorite)
            {
                FileDialogMemory.RemoveFavorite(favorite.Path);
                _contextFavorite = null;
                RefreshFavorites();
            }
        }

        // ------------------------------------------------------------------ context menu of the file list

        private void OnFilesRightClick(Object sender, MouseButtonEventArgs e)
        {
            // a right click selects the targeted item, as in Explorer
            if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(FilesList, source) is ListBoxItem item
                && !item.IsSelected)
            {
                FilesList.SelectedItems.Clear();
                item.IsSelected = true;
            }
        }

        private Entry? ContextFolder => FilesList.SelectedItem is Entry { IsDirectory: true } entry ? entry : null;

        private void OnFilesMenuOpened(Object sender, RoutedEventArgs e)
        {
            // "Add to favorites" only applies to a folder; with no targeted folder, it applies to the current folder
            String path = ContextFolder?.FullPath ?? _directory;
            FileMenuFavorite.Header = Localizer.T(FileDialogMemory.IsFavorite(path) ? "Remove from favorites" : "Add to favorites");
        }

        private void OnFileMenuFavorite(Object sender, RoutedEventArgs e)
        {
            String path = ContextFolder?.FullPath ?? _directory;
            if (FileDialogMemory.IsFavorite(path))
                FileDialogMemory.RemoveFavorite(path);
            else
                FileDialogMemory.AddFavorite(path);
            RefreshFavorites();
        }

        // ------------------------------------------------------------------ new folder, refresh

        private void OnNewFolder(Object sender, RoutedEventArgs e)
        {
            String? name = TextPromptDialog.Show(this, "New folder", "Folder name:", Localizer.T("New folder"));
            if (name is null)
                return;
            try
            {
                String path = Path.Combine(_directory, name);
                Directory.CreateDirectory(path);
                Navigate(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                MessageDialog.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnRefresh(Object sender, System.Windows.Input.ExecutedRoutedEventArgs e) => Reload();

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
                return Path.GetFullPath(Path.IsPathRooted(name) ? name : Path.Combine(_directory, name));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return Path.Combine(_directory, name);
            }
        }

        private void OnOk(Object sender, RoutedEventArgs e)
        {
            List<String> names = SplitNames(NameBox.Text).ToList();
            if (names.Count == 0)
                return;

            // a folder was entered: navigate to it
            if (names.Count == 1 && Directory.Exists(Resolve(names[0])))
            {
                Navigate(Resolve(names[0]));
                NameBox.Text = "";
                return;
            }

            if (_save)
            {
                String path = Resolve(names[0]);
                if (_defaultExt.Length > 0 && Path.GetExtension(path).Length == 0)
                    path += "." + _defaultExt.TrimStart('.');
                if (_overwritePrompt && File.Exists(path)
                    && MessageDialog.Show(this, Localizer.F("'{0}' already exists.\nDo you want to replace it?", Path.GetFileName(path)),
                        Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }
                Accept([path]);
                return;
            }

            if (names.Count > 1 && !_multiselect)
                names = [names[0]];
            List<String> paths = names.Select(Resolve).ToList();
            String? missing = _mustExist ? paths.FirstOrDefault(path => !File.Exists(path)) : null;
            if (missing is not null)
            {
                MessageDialog.Show(this, Localizer.F("'{0}' was not found.", Path.GetFileName(missing)), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Accept(paths);
        }

        private void Accept(IEnumerable<String> paths)
        {
            Result = paths.ToArray();
            if (Path.GetDirectoryName(Result[0]) is { Length: > 0 } directory && Directory.Exists(directory))
                FileDialogMemory.SetLastDirectory(_save, directory);
            DialogResult = true;
        }
    }
}