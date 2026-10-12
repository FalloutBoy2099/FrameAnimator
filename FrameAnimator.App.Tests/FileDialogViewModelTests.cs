namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Services;
    using FrameAnimator.App.ViewModels;

    /// <summary>
    /// View model of the application's file picker: filters, listing, navigation, favorites, and the open / save rules.
    /// The settings (last folders, favorites) are kept in memory, one store per test.
    /// </summary>
    public sealed class FileDialogViewModelTests : IDisposable
    {
        private const String Filter = "All (*.FPR,*.FRM)|*.FPR;*.FRM|Project (*.FPR)|*.FPR|Text (*.TXT)|*.TXT";

        private readonly TempDir _dir = new();
        private readonly MemorySettingsStore _settings = new();
        private readonly FileDialogMemory _memory;
        private readonly FakeDialogService _messages = new();

        public FileDialogViewModelTests() => _memory = new FileDialogMemory(_settings);

        public void Dispose() => _dir.Dispose();

        private FileDialogViewModel Create(FileDialogOptions options, Func<String, String, String, String?>? prompt = null)
            => new(options, _messages, prompt ?? ((_, _, _) => null), TestServices.Localizer(), _memory);

        /// <summary>Open dialog that starts in the temporary folder.</summary>
        private FileDialogViewModel Open(Boolean multiselect = false, String filter = Filter, Int32 filterIndex = 1)
        {
            _memory.SetLastDirectory(save: false, _dir.Path);
            return Create(FileDialogOptions.ForOpen("Open", filter, multiselect) with { FilterIndex = filterIndex });
        }

        private FileDialogViewModel Save(String? fileName = null, String defaultExt = "fpr", Boolean overwritePrompt = true)
            => Create(FileDialogOptions.ForSave("Save", Filter, 1, fileName ?? _dir.File("untitled"), defaultExt, overwritePrompt));

        private String Touch(String name, Int32 size = 0)
        {
            String path = _dir.File(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, new Byte[size]);
            return Path.GetFullPath(path);
        }

        private static List<String> Names(FileDialogViewModel viewModel) => viewModel.Entries.Select(entry => entry.Name).ToList();

        private static Boolean? CloseOf(FileDialogViewModel viewModel, Action action)
        {
            Boolean? closed = null;
            viewModel.CloseRequested += (_, accepted) => closed = accepted;
            action();
            return closed;
        }

        private static void Ok(FileDialogViewModel viewModel) => viewModel.OkCommand.Execute(null);

        // ------------------------------------------------------------------ filters and listing

        [Fact]
        public void ParsesTheFilterString()
        {
            FileDialogViewModel viewModel = Open();

            Assert.Equal(["All (*.FPR,*.FRM)", "Project (*.FPR)", "Text (*.TXT)"], viewModel.Filters.Select(filter => filter.Display));
            Assert.Equal(["*.FPR", "*.FRM"], viewModel.Filters[0].Patterns);
            Assert.Equal(["*.FPR"], viewModel.Filters[1].Patterns);
            Assert.Same(viewModel.Filters[0], viewModel.SelectedFilter);
        }

        [Fact]
        public void TheInitialFilterIsTheRequestedOneAndIsClamped()
        {
            FileDialogViewModel second = Open(filterIndex: 2);
            Assert.Equal("Project (*.FPR)", second.SelectedFilter!.Display);
            Assert.Equal("Text (*.TXT)", Open(filterIndex: 99).SelectedFilter!.Display);
            Assert.Equal("All (*.FPR,*.FRM)", Open(filterIndex: 0).SelectedFilter!.Display);
        }

        [Fact]
        public void AnEmptyFilterMeansAllFiles()
        {
            Touch("a.fpr");
            Touch("b.anything");
            FileDialogViewModel viewModel = Open(filter: "");

            Assert.Single(viewModel.Filters);
            Assert.Equal(["*.*"], viewModel.Filters[0].Patterns);
            Assert.Equal("All (*.*)", viewModel.Filters[0].Display);
            Assert.Equal(["a.fpr", "b.anything"], Names(viewModel));
        }

        [Fact]
        public void ListsTheFilesOfTheSelectedFilterIgnoringCase()
        {
            Touch("a.fpr");
            Touch("B.FRM");
            Touch("c.txt");
            Touch("d.bmp");
            FileDialogViewModel viewModel = Open();

            Assert.Equal(["a.fpr", "B.FRM"], Names(viewModel));

            viewModel.SelectedFilter = viewModel.Filters[1];
            Assert.Equal(["a.fpr"], Names(viewModel));

            viewModel.SelectedFilter = viewModel.Filters[2];
            Assert.Equal(["c.txt"], Names(viewModel));
        }

        [Fact]
        public void FoldersComeFirstAndHiddenEntriesAreSkipped()
        {
            Touch("b.fpr", 10);
            Touch("A.fpr", 2000);
            Directory.CreateDirectory(_dir.File("zeta"));
            Directory.CreateDirectory(_dir.File("alpha"));
            Directory.CreateDirectory(_dir.File("secret"));
            File.SetAttributes(_dir.File("secret"), FileAttributes.Hidden | FileAttributes.Directory);
            File.SetAttributes(Touch("hidden.fpr"), FileAttributes.Hidden);
            FileDialogViewModel viewModel = Open();

            Assert.Equal(["alpha", "zeta", "A.fpr", "b.fpr"], Names(viewModel));
            Assert.All(viewModel.Entries.Take(2), entry => Assert.True(entry.IsDirectory));
            Assert.Equal("", viewModel.Entries[0].SizeText);
            Assert.Equal("2 KB", viewModel.Entries[2].SizeText);
            Assert.Equal("10 B", viewModel.Entries[3].SizeText);
            Assert.Equal(new FileInfo(_dir.File("b.fpr")).LastWriteTime.ToString("g"), viewModel.Entries[3].Modified);
            Assert.NotEqual(viewModel.Entries[0].Glyph, viewModel.Entries[2].Glyph);
        }

        [Fact]
        public void ARefreshPicksUpNewFiles()
        {
            Touch("a.fpr");
            FileDialogViewModel viewModel = Open();
            Touch("b.fpr");

            Assert.Equal(["a.fpr"], Names(viewModel));

            viewModel.RefreshCommand.Execute(null);

            Assert.Equal(["a.fpr", "b.fpr"], Names(viewModel));
        }

        // ------------------------------------------------------------------ navigation

        [Fact]
        public void StartsInTheLastOpenFolderAndShowsItInThePathBox()
        {
            FileDialogViewModel viewModel = Open();

            Assert.Equal(_dir.Path, viewModel.CurrentDirectory);
            Assert.Equal(_dir.Path, viewModel.PathText);
            Assert.False(viewModel.BackCommand.CanExecute(null));
            Assert.True(viewModel.UpCommand.CanExecute(null));
        }

        [Fact]
        public void UpAndBackWalkTheHistory()
        {
            Touch("sub/inner.fpr");
            FileDialogViewModel viewModel = Open();

            viewModel.NavigateCommand.Execute(_dir.File("sub"));
            Assert.Equal(_dir.File("sub"), viewModel.CurrentDirectory);
            Assert.Equal(["inner.fpr"], Names(viewModel));
            Assert.True(viewModel.BackCommand.CanExecute(null));

            viewModel.UpCommand.Execute(null);
            Assert.Equal(_dir.Path, viewModel.CurrentDirectory);
            Assert.Equal(_dir.Path, viewModel.PathText);

            viewModel.BackCommand.Execute(null);
            Assert.Equal(_dir.File("sub"), viewModel.CurrentDirectory);
            Assert.True(viewModel.BackCommand.CanExecute(null));

            viewModel.BackCommand.Execute(null);
            Assert.Equal(_dir.Path, viewModel.CurrentDirectory);
            Assert.False(viewModel.BackCommand.CanExecute(null));
        }

        [Fact]
        public void UpIsDisabledAtTheRootOfADrive()
        {
            FileDialogViewModel viewModel = Open();

            viewModel.NavigateCommand.Execute(Path.GetPathRoot(_dir.Path));

            Assert.False(viewModel.UpCommand.CanExecute(null));
        }

        [Fact]
        public void ATypedPathNavigatesAndABadOneIsResetToTheCurrentFolder()
        {
            Touch("sub/inner.fpr");
            FileDialogViewModel viewModel = Open();

            viewModel.PathText = "  " + _dir.File("sub") + "  ";
            viewModel.NavigateToPathCommand.Execute(null);
            Assert.Equal(_dir.File("sub"), viewModel.CurrentDirectory);
            Assert.Equal(_dir.File("sub"), viewModel.PathText);

            viewModel.PathText = _dir.File("does-not-exist");
            viewModel.NavigateToPathCommand.Execute(null);
            Assert.Equal(_dir.File("sub"), viewModel.CurrentDirectory);
            Assert.Equal(_dir.File("sub"), viewModel.PathText);
            Assert.Empty(_messages.Warnings);
        }

        [Fact]
        public void NavigatingToTheSameFolderDoesNotGrowTheHistory()
        {
            FileDialogViewModel viewModel = Open();

            viewModel.NavigateCommand.Execute(_dir.Path);

            Assert.False(viewModel.BackCommand.CanExecute(null));
        }

        [Fact]
        public void PlacesIncludeTheDrivesAndTheyOpenTheirFolder()
        {
            FileDialogViewModel viewModel = Open();
            PlaceEntry drive = viewModel.Places.First(place => place.Path == Path.GetPathRoot(_dir.Path));

            viewModel.OpenPlaceCommand.Execute(drive);

            Assert.Equal(drive.Path, viewModel.CurrentDirectory);
        }

        // ------------------------------------------------------------------ file name box

        [Fact]
        public void TheNameBoxFollowsTheSelectedFiles()
        {
            Touch("a.fpr");
            Touch("b.fpr");
            Directory.CreateDirectory(_dir.File("sub"));
            FileDialogViewModel viewModel = Open(multiselect: true);
            FileEntry folder = viewModel.Entries.First(entry => entry.IsDirectory);
            FileEntry a = viewModel.Entries.First(entry => entry.Name == "a.fpr");
            FileEntry b = viewModel.Entries.First(entry => entry.Name == "b.fpr");

            viewModel.SetSelectedEntries([a]);
            Assert.Equal("a.fpr", viewModel.NameText);

            viewModel.SetSelectedEntries([a, b]);
            Assert.Equal("\"a.fpr\" \"b.fpr\"", viewModel.NameText);

            viewModel.SetSelectedEntries([folder]);
            Assert.Equal("\"a.fpr\" \"b.fpr\"", viewModel.NameText);

            viewModel.SetSelectedEntries([]);
            Assert.Equal("\"a.fpr\" \"b.fpr\"", viewModel.NameText);

            viewModel.SetSelectedEntries([folder, b]);
            Assert.Equal("b.fpr", viewModel.NameText);
        }

        [Fact]
        public void TheInitialFileNameFillsTheNameBoxAndItsFolderIsTheStart()
        {
            Directory.CreateDirectory(_dir.File("sub"));
            FileDialogViewModel viewModel = Save(_dir.File("sub/project.fpr"));

            Assert.Equal("project.fpr", viewModel.NameText);
            Assert.Equal(_dir.File("sub"), viewModel.CurrentDirectory);
        }

        [Fact]
        public void TheDialogAdaptsToOpenAndSave()
        {
            Assert.Equal("Open", Open().OkText);
            Assert.Equal("Save", Save().OkText);
            Assert.Equal("Save", Save().Title);
            Assert.Equal(System.Windows.Controls.SelectionMode.Extended, Open(multiselect: true).SelectionMode);
            Assert.Equal(System.Windows.Controls.SelectionMode.Single, Open().SelectionMode);
        }

        // ------------------------------------------------------------------ open

        [Fact]
        public void OpenAcceptsASingleExistingFile()
        {
            String path = Touch("a.fpr");
            FileDialogViewModel viewModel = Open();
            viewModel.NameText = "a.fpr";

            Boolean? closed = CloseOf(viewModel, () => Ok(viewModel));

            Assert.True(closed);
            Assert.Equal([path], viewModel.Result);
            Assert.Empty(_messages.Warnings);
        }

        [Fact]
        public void OpenAcceptsSeveralQuotedFilesWhenMultiselect()
        {
            String a = Touch("a.fpr");
            String b = Touch("b with space.fpr");
            FileDialogViewModel viewModel = Open(multiselect: true);
            viewModel.NameText = "\"a.fpr\" \"b with space.fpr\"";

            Boolean? closed = CloseOf(viewModel, () => Ok(viewModel));

            Assert.True(closed);
            Assert.Equal([a, b], viewModel.Result);
        }

        [Fact]
        public void OpenKeepsOnlyTheFirstFileWithoutMultiselect()
        {
            String a = Touch("a.fpr");
            Touch("b.fpr");
            FileDialogViewModel viewModel = Open(multiselect: false);
            viewModel.NameText = "\"a.fpr\" \"b.fpr\"";

            Ok(viewModel);

            Assert.Equal([a], viewModel.Result);
        }

        [Fact]
        public void OpenAcceptsAnAbsolutePathTypedInTheNameBox()
        {
            String path = Touch("sub/inner.fpr");
            FileDialogViewModel viewModel = Open();
            viewModel.NameText = path;

            Ok(viewModel);

            Assert.Equal([path], viewModel.Result);
        }

        [Fact]
        public void OpenWarnsWhenAFileIsMissing()
        {
            Touch("a.fpr");
            FileDialogViewModel viewModel = Open(multiselect: true);
            viewModel.NameText = "\"a.fpr\" \"zz.fpr\"";

            Boolean? closed = CloseOf(viewModel, () => Ok(viewModel));

            Assert.Null(closed);
            Assert.Empty(viewModel.Result);
            Assert.Equal(["'zz.fpr' was not found."], _messages.Warnings);
        }

        [Fact]
        public void AnEmptyNameDoesNothing()
        {
            FileDialogViewModel viewModel = Open();
            viewModel.NameText = "   ";

            Boolean? closed = CloseOf(viewModel, () => Ok(viewModel));

            Assert.Null(closed);
            Assert.Empty(_messages.Warnings);
        }

        [Fact]
        public void ATypedFolderNavigatesInsteadOfAccepting()
        {
            Touch("sub/inner.fpr");
            FileDialogViewModel viewModel = Open();
            viewModel.NameText = "sub";

            Boolean? closed = CloseOf(viewModel, () => Ok(viewModel));

            Assert.Null(closed);
            Assert.Equal(_dir.File("sub"), viewModel.CurrentDirectory);
            Assert.Equal("", viewModel.NameText);
            Assert.Equal(["inner.fpr"], Names(viewModel));
        }

        [Fact]
        public void OpeningTheSelectedRowEntersAFolderOrAcceptsAFile()
        {
            String file = Touch("a.fpr");
            Directory.CreateDirectory(_dir.File("sub"));
            FileDialogViewModel viewModel = Open();

            viewModel.SetSelectedEntries([viewModel.Entries.First(entry => entry.IsDirectory)]);
            viewModel.OpenSelectedCommand.Execute(null);
            Assert.Equal(_dir.File("sub"), viewModel.CurrentDirectory);

            viewModel.UpCommand.Execute(null);
            viewModel.SetSelectedEntries([viewModel.Entries.First(entry => !entry.IsDirectory)]);
            Boolean? closed = CloseOf(viewModel, () => viewModel.OpenSelectedCommand.Execute(null));

            Assert.True(closed);
            Assert.Equal([file], viewModel.Result);
        }

        // ------------------------------------------------------------------ save

        [Fact]
        public void SaveAppendsTheDefaultExtensionWhenThereIsNone()
        {
            FileDialogViewModel viewModel = Save(defaultExt: ".fpr");
            viewModel.NameText = "project";

            Boolean? closed = CloseOf(viewModel, () => Ok(viewModel));

            Assert.True(closed);
            Assert.Equal([_dir.File("project.fpr")], viewModel.Result);
        }

        [Fact]
        public void SaveKeepsAnExplicitExtensionAndWorksWithoutDefaultExtension()
        {
            FileDialogViewModel withExtension = Save();
            withExtension.NameText = "project.frm";
            Ok(withExtension);
            Assert.Equal([_dir.File("project.frm")], withExtension.Result);

            FileDialogViewModel without = Save(defaultExt: "");
            without.NameText = "project";
            Ok(without);
            Assert.Equal([_dir.File("project")], without.Result);
        }

        [Fact]
        public void SaveDoesNotRequireTheFileToExist()
        {
            FileDialogViewModel viewModel = Save();
            viewModel.NameText = "brand-new.fpr";

            Ok(viewModel);

            Assert.Equal([_dir.File("brand-new.fpr")], viewModel.Result);
            Assert.Empty(_messages.Warnings);
        }

        [Fact]
        public void SaveAsksBeforeReplacingAndStaysOpenOnNo()
        {
            Touch("project.fpr");
            FileDialogViewModel viewModel = Save();
            viewModel.NameText = "project";
            _messages.YesNoAnswer = false;

            Boolean? closed = CloseOf(viewModel, () => Ok(viewModel));

            Assert.Null(closed);
            Assert.Empty(viewModel.Result);
            Assert.Equal(["'project.fpr' already exists.\nDo you want to replace it?"], _messages.Questions);
        }

        [Fact]
        public void SaveReplacesOnYes()
        {
            Touch("project.fpr");
            FileDialogViewModel viewModel = Save();
            viewModel.NameText = "project.fpr";
            _messages.YesNoAnswer = true;

            Boolean? closed = CloseOf(viewModel, () => Ok(viewModel));

            Assert.True(closed);
            Assert.Equal([_dir.File("project.fpr")], viewModel.Result);
            Assert.Single(_messages.Questions);
        }

        [Fact]
        public void SaveDoesNotAskWhenTheOverwritePromptIsOff()
        {
            Touch("project.fpr");
            FileDialogViewModel viewModel = Save(overwritePrompt: false);
            viewModel.NameText = "project.fpr";

            Ok(viewModel);

            Assert.Equal([_dir.File("project.fpr")], viewModel.Result);
            Assert.Empty(_messages.Questions);
        }

        [Fact]
        public void SaveWithATypedFolderNavigates()
        {
            Directory.CreateDirectory(_dir.File("sub"));
            FileDialogViewModel viewModel = Save();
            viewModel.NameText = "sub";

            Boolean? closed = CloseOf(viewModel, () => Ok(viewModel));

            Assert.Null(closed);
            Assert.Equal(_dir.File("sub"), viewModel.CurrentDirectory);
        }

        // ------------------------------------------------------------------ remembered folders

        [Fact]
        public void TheLastFolderIsRememberedSeparatelyForOpenAndSave()
        {
            String openFolder = _dir.File("for-open");
            String saveFolder = _dir.File("for-save");
            Touch("for-open/a.fpr");
            Directory.CreateDirectory(saveFolder);

            _memory.SetLastDirectory(save: false, openFolder);
            FileDialogViewModel open = Create(FileDialogOptions.ForOpen("Open", Filter, false));
            open.NameText = "a.fpr";
            Ok(open);

            _memory.SetLastDirectory(save: true, saveFolder);
            FileDialogViewModel save = Create(FileDialogOptions.ForSave("Save", Filter, 1, null, "fpr", true));
            save.NameText = "out.fpr";
            Ok(save);

            Assert.Equal(openFolder, _memory.GetLastDirectory(save: false));
            Assert.Equal(saveFolder, _memory.GetLastDirectory(save: true));
            Assert.Equal(openFolder, Create(FileDialogOptions.ForOpen("Open", Filter, false)).CurrentDirectory);
            Assert.Equal(saveFolder, Create(FileDialogOptions.ForSave("Save", Filter, 1, null, "fpr", true)).CurrentDirectory);
        }

        [Fact]
        public void AcceptingStoresTheFolderOfTheChosenFile()
        {
            Touch("sub/inner.fpr");
            FileDialogViewModel viewModel = Open();
            viewModel.NameText = _dir.File("sub/inner.fpr");

            Ok(viewModel);

            Assert.Equal(_dir.File("sub"), _memory.GetLastDirectory(save: false));
            Assert.Null(_memory.GetLastDirectory(save: true));
        }

        // ------------------------------------------------------------------ favorites

        [Fact]
        public void TheStarAddsAndRemovesTheCurrentFolder()
        {
            FileDialogViewModel viewModel = Open();
            Assert.True(viewModel.ShowFavoritesHint);
            Assert.Empty(viewModel.Favorites);
            Assert.Equal("Add to favorites", viewModel.FavoriteToolTip);
            String emptyStar = viewModel.FavoriteGlyph;

            viewModel.ToggleFavoriteCommand.Execute(null);

            Assert.False(viewModel.ShowFavoritesHint);
            PlaceEntry favorite = Assert.Single(viewModel.Favorites);
            Assert.Equal(_dir.Path, favorite.Path);
            Assert.Equal(Path.GetFileName(_dir.Path), favorite.Label);
            Assert.Equal("Remove from favorites", viewModel.FavoriteToolTip);
            Assert.NotEqual(emptyStar, viewModel.FavoriteGlyph);
            Assert.Equal([_dir.Path], _memory.GetFavorites());

            viewModel.ToggleFavoriteCommand.Execute(null);

            Assert.True(viewModel.ShowFavoritesHint);
            Assert.Empty(viewModel.Favorites);
            Assert.Equal("Add to favorites", viewModel.FavoriteToolTip);
            Assert.Equal(emptyStar, viewModel.FavoriteGlyph);
            Assert.Empty(_memory.GetFavorites());
        }

        [Fact]
        public void TheStarFollowsTheNavigation()
        {
            Directory.CreateDirectory(_dir.File("sub"));
            FileDialogViewModel viewModel = Open();
            viewModel.ToggleFavoriteCommand.Execute(null);
            String filledStar = viewModel.FavoriteGlyph;

            viewModel.NavigateCommand.Execute(_dir.File("sub"));
            Assert.NotEqual(filledStar, viewModel.FavoriteGlyph);
            Assert.Equal("Add to favorites", viewModel.FavoriteToolTip);

            viewModel.UpCommand.Execute(null);
            Assert.Equal(filledStar, viewModel.FavoriteGlyph);
        }

        [Fact]
        public void FavoritesArePersistedAndOpenTheirFolder()
        {
            Directory.CreateDirectory(_dir.File("sub"));
            FileDialogViewModel first = Open();
            first.NavigateCommand.Execute(_dir.File("sub"));
            first.ToggleFavoriteCommand.Execute(null);

            FileDialogViewModel second = Open();

            PlaceEntry favorite = Assert.Single(second.Favorites);
            Assert.Equal("sub", favorite.Label);
            second.OpenFavoriteCommand.Execute(favorite);
            Assert.Equal(_dir.File("sub"), second.CurrentDirectory);
            Assert.Empty(_messages.Warnings);
        }

        [Fact]
        public void AMissingFavoriteIsReportedAndNavigationStays()
        {
            String gone = _dir.File("gone");
            _settings.SetAll("favorite", [gone]);
            FileDialogViewModel viewModel = Open();

            viewModel.OpenFavoriteCommand.Execute(Assert.Single(viewModel.Favorites));

            Assert.Equal(_dir.Path, viewModel.CurrentDirectory);
            Assert.Equal(["'" + gone + "' was not found."], _messages.Warnings);
        }

        [Fact]
        public void TheContextMenuRemovesTheTargetedFavorite()
        {
            Directory.CreateDirectory(_dir.File("one"));
            Directory.CreateDirectory(_dir.File("two"));
            _settings.SetAll("favorite", [_dir.File("one"), _dir.File("two")]);
            FileDialogViewModel viewModel = Open();
            Assert.False(viewModel.RemoveFavoriteCommand.CanExecute(null));

            viewModel.ContextFavorite = viewModel.Favorites[0];
            Assert.True(viewModel.RemoveFavoriteCommand.CanExecute(null));
            viewModel.RemoveFavoriteCommand.Execute(null);

            Assert.Equal([_dir.File("two")], viewModel.Favorites.Select(favorite => favorite.Path));
            Assert.Equal([_dir.File("two")], _memory.GetFavorites());
            Assert.False(viewModel.RemoveFavoriteCommand.CanExecute(null));
        }

        [Fact]
        public void TheFileMenuTogglesTheSelectedFolderOrTheCurrentOne()
        {
            Directory.CreateDirectory(_dir.File("sub"));
            FileDialogViewModel viewModel = Open();
            FileEntry folder = viewModel.Entries.Single();

            Assert.Equal("Add to favorites", viewModel.FileMenuFavoriteHeader);

            viewModel.SetSelectedEntries([folder]);
            viewModel.AddToFavoritesCommand.Execute(null);
            Assert.Equal([_dir.File("sub")], _memory.GetFavorites());
            Assert.Equal("Remove from favorites", viewModel.FileMenuFavoriteHeader);

            viewModel.SetSelectedEntries([]);
            Assert.Equal("Add to favorites", viewModel.FileMenuFavoriteHeader);
            viewModel.AddToFavoritesCommand.Execute(null);
            Assert.Equal([_dir.File("sub"), _dir.Path], _memory.GetFavorites());

            viewModel.AddToFavoritesCommand.Execute(null);
            Assert.Equal([_dir.File("sub")], _memory.GetFavorites());
        }

        // ------------------------------------------------------------------ new folder

        [Fact]
        public void NewFolderCreatesAndOpensTheFolderTheUserNamed()
        {
            List<(String Title, String Prompt, String Initial)> asked = [];
            _memory.SetLastDirectory(save: false, _dir.Path);
            FileDialogViewModel viewModel = Create(FileDialogOptions.ForOpen("Open", Filter, false), (title, prompt, initial) =>
            {
                asked.Add((title, prompt, initial));
                return "made";
            });

            viewModel.NewFolderCommand.Execute(null);

            Assert.True(Directory.Exists(_dir.File("made")));
            Assert.Equal(_dir.File("made"), viewModel.CurrentDirectory);
            Assert.Equal(("New folder", "Folder name:", "New folder"), asked.Last());
            Assert.True(viewModel.BackCommand.CanExecute(null));
        }

        [Fact]
        public void NewFolderDoesNothingWhenThePromptIsCanceled()
        {
            _memory.SetLastDirectory(save: false, _dir.Path);
            FileDialogViewModel viewModel = Create(FileDialogOptions.ForOpen("Open", Filter, false), (_, _, _) => null);

            viewModel.NewFolderCommand.Execute(null);

            Assert.Equal(_dir.Path, viewModel.CurrentDirectory);
            Assert.Empty(Directory.GetDirectories(_dir.Path));
            Assert.Empty(_messages.Warnings);
        }

        [Fact]
        public void NewFolderReportsAnInvalidName()
        {
            _memory.SetLastDirectory(save: false, _dir.Path);
            FileDialogViewModel viewModel = Create(FileDialogOptions.ForOpen("Open", Filter, false), (_, _, _) => "bad\0name");

            viewModel.NewFolderCommand.Execute(null);

            Assert.Equal(_dir.Path, viewModel.CurrentDirectory);
            Assert.Single(_messages.Warnings);
        }
    }
}
