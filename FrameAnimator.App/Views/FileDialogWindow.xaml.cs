namespace FrameAnimator.App.Views
{
    using System;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using ViewModels;

    /// <summary>
    /// File picker using the application's theme (system dialogs do not follow the dark theme). Everything is in
    /// <see cref="FileDialogViewModel"/>; this view only forwards what a binding cannot express (list selections, clicks, keys).
    /// </summary>
    public partial class FileDialogWindow : Window
    {
        public FileDialogWindow()
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);
            Loaded += (_, _) =>
            {
                NameBox.Focus();
                NameBox.SelectAll();
            };
        }

        private FileDialogViewModel? ViewModel => DataContext as FileDialogViewModel;

        /// <summary>Development preview (<c>--dialog open</c> switch): a ready-to-show window with its view model.</summary>
        internal static Window CreateForPreview() => new FileDialogWindow
        {
            DataContext = FileDialogs.CreateViewModel(FileDialogOptions.ForOpen("Open Project",
                DialogService.TranslateFilter("All (*.FPR,*.FRM)|*.FPR;*.FRM|Project (*.FPR)|*.FPR"), multiselect: true)),
        };

        // ------------------------------------------------------------------ places and favorites (a selection opens the folder)

        private void OnPlaceChanged(Object sender, SelectionChangedEventArgs e)
        {
            if (PlacesList.SelectedItem is PlaceEntry place)
            {
                PlacesList.SelectedItem = null;
                ViewModel?.OpenPlaceCommand.Execute(place);
            }
        }

        private void OnFavoriteChanged(Object sender, SelectionChangedEventArgs e)
        {
            if (FavoritesList.SelectedItem is PlaceEntry favorite)
            {
                FavoritesList.SelectedItem = null;
                ViewModel?.OpenFavoriteCommand.Execute(favorite);
            }
        }

        private void OnFavoritesRightClick(Object sender, MouseButtonEventArgs e)
        {
            // the favorite targeted by the right click (without selecting it: a selection would open the folder)
            if (ViewModel is { } viewModel)
            {
                viewModel.ContextFavorite = e.OriginalSource is DependencyObject source
                    && ItemsControl.ContainerFromElement(FavoritesList, source) is ListBoxItem { DataContext: PlaceEntry favorite }
                    ? favorite
                    : null;
            }
        }

        // ------------------------------------------------------------------ file list

        private void OnFileSelectionChanged(Object sender, SelectionChangedEventArgs e)
            => ViewModel?.SetSelectedEntries(FilesList.SelectedItems.OfType<FileEntry>().ToList());

        private void OnFileDoubleClick(Object sender, MouseButtonEventArgs e)
        {
            if (FilesList.SelectedItem is FileEntry)
                ViewModel?.OpenSelectedCommand.Execute(null);
        }

        private void OnFilesKeyDown(Object sender, KeyEventArgs e)
        {
            if (ViewModel is not { } viewModel)
                return;
            if (e.Key == Key.Enter && FilesList.SelectedItem is FileEntry { IsDirectory: true })
            {
                viewModel.OpenSelectedCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Back)
            {
                viewModel.UpCommand.Execute(null);
                e.Handled = true;
            }
        }

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
    }
}