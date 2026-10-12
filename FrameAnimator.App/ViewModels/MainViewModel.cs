namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;
    using FrameAnimator.App.Services;
    using FrameAnimator.Core.Formats;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;
    using FrameAnimator.Core.Services;

    /// <summary>
    /// View model of the main window: the file and project actions (<c>TfrmFrameAnimator</c> of the original, specification §6.1 to §6.4)
    /// and the parts of the window, each one a view model of its own: <see cref="FrameList"/> (directions and frames),
    /// <see cref="Canvas"/> (drawing area), <see cref="Status"/> (status bar), <see cref="Appearance"/> (theme, language, options) and
    /// <see cref="Preview"/> (animated preview). They share one <see cref="ProjectSession"/>.
    /// </summary>
    public sealed partial class MainViewModel : ObservableObject
    {
        private const String FrmFilter = "All (*.FPR,*.FRM,*.FR0-*.FR5)|*.FPR;*.FRM;*.FR0;*.FR1;*.FR2;*.FR3;*.FR4;*.FR5|Project (*.FPR)|*.FPR|FRM (*.FRM,*.FR0-*.FR5)|*.FRM;*.FR0;*.FR1;*.FR2;*.FR3;*.FR4;*.FR5";
        private const String PaletteFilter = "All (*.ACT, *.PAL)|*.ACT;*.PAL|Palette (*.ACT)|*.ACT|Fallout Palette (*.PAL)|*.PAL";

        private readonly IDialogService _dialogs;
        private readonly IProjectPreferences _preferences;
        private readonly ILocalizer _localizer;
        private Boolean _closeConfirmed;

        public MainViewModel(
            ProjectSession session,
            FrameListViewModel frameList,
            CanvasViewModel canvas,
            StatusBarViewModel status,
            AppearanceViewModel appearance,
            AnimationPreviewViewModel preview,
            IDialogService dialogs,
            IProjectPreferences preferences,
            ILocalizer localizer)
        {
            Session = session;
            FrameList = frameList;
            Canvas = canvas;
            Status = status;
            Appearance = appearance;
            Preview = preview;
            _dialogs = dialogs;
            _preferences = preferences;
            _localizer = localizer;

            Service.SaveRelativePaths = preferences.SaveRelativePaths;
            LoadRecentFiles();
            session.Refreshed += (_, _) =>
            {
                OnPropertyChanged(nameof(Title));
                UpdateCommands();
            };
            session.StateChanged += (_, _) => UpdateCommands();
            session.Edited += (_, _) =>
            {
                UndoCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(Title)); // a drag changed the project: the modified mark appears
            };
            frameList.SelectionChanged += (_, _) => UpdateCommands();
            session.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(ProjectSession.IsBusy) or nameof(ProjectSession.IsIdle) or nameof(ProjectSession.IsBatchRunning))
                    OnPropertyChanged(e.PropertyName);
            };

            session.Refresh();
        }

        // ------------------------------------------------------------------ parts of the window

        public ProjectSession Session { get; }

        public FrameListViewModel FrameList { get; }

        public CanvasViewModel Canvas { get; }

        public StatusBarViewModel Status { get; }

        public AppearanceViewModel Appearance { get; }

        public AnimationPreviewViewModel Preview { get; }

        // ------------------------------------------------------------------ state

        public Project Project => Session.Project;

        public ProjectService Service => Session.Service;

        public ProjectEditor Editor => Session.Editor;

        public Boolean IsBusy => Session.IsBusy;

        public Boolean IsIdle => Session.IsIdle;

        public Boolean IsBatchRunning => Session.IsBatchRunning;

        /// <summary>Window title: the project name (starred while it has unsaved changes) and the application name.</summary>
        public String Title => $"{(Service.IsModified ? "*" : "")}{Project.Title} — Frame Animator";

        /// <summary>Write the paths of the project file relative to its folder (the project can then be moved with its images).</summary>
        public Boolean SaveRelativePaths
        {
            get => _preferences.SaveRelativePaths;
            set
            {
                if (_preferences.SaveRelativePaths == value)
                    return;
                _preferences.SaveRelativePaths = value;
                Service.SaveRelativePaths = value;
                OnPropertyChanged();
            }
        }

        // ------------------------------------------------------------------ enabling rules

        private Boolean CanStartJob() => Session.CanStartJob;

        private Boolean CanCreateFrm() => IsIdle && FrmFormat.IsValidForFrm(Project);

        private Boolean CanUndo() => IsIdle && Editor.CanUndo;

        private Boolean CanCancel() => Session.IsBusy || Session.IsBatchRunning;

        private Boolean CanClearRecentFiles() => AnyRecentFiles && Session.CanStartJob;

        /// <summary>
        /// Commands do not poll WPF: after any change that can alter an enabling rule, this re-evaluates them
        /// (busy state, frames, undo stack).
        /// </summary>
        private void UpdateCommands()
        {
            NewCommand.NotifyCanExecuteChanged();
            OpenCommand.NotifyCanExecuteChanged();
            OpenRecentCommand.NotifyCanExecuteChanged();
            ClearRecentFilesCommand.NotifyCanExecuteChanged();
            ExecuteScriptCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            SaveAsCommand.NotifyCanExecuteChanged();
            CreateFrmCommand.NotifyCanExecuteChanged();
            AutomateCommand.NotifyCanExecuteChanged();
            ExitCommand.NotifyCanExecuteChanged();
            UndoCommand.NotifyCanExecuteChanged();
            LoadPaletteCommand.NotifyCanExecuteChanged();
            OptionsCommand.NotifyCanExecuteChanged();
            LoadBackgroundCommand.NotifyCanExecuteChanged();
            AboutCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }

        // ------------------------------------------------------------------ unsaved changes

        /// <summary>
        /// Before something replaces the project: if it has unsaved changes, asks whether to save them. Returns <c>false</c> when the
        /// user cancelled (or a requested save was cancelled), in which case nothing must be replaced.
        /// </summary>
        public Boolean ConfirmDiscardChanges()
        {
            if (!Service.IsModified)
                return true;
            switch (_dialogs.AskYesNoCancel(_localizer.F("Save changes to '{0}'?", Project.Title)))
            {
                case MessageBoxResult.Yes:
                    return SaveCore();
                case MessageBoxResult.No:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The window is closing (or the application exits): <c>false</c> when the user wants to keep working.</summary>
        public Boolean ConfirmClose() => _closeConfirmed || ConfirmDiscardChanges();

        // ------------------------------------------------------------------ recent files

        /// <summary>The "Recent projects" menu: the projects opened or saved last, the most recent first, then an entry that clears the list.</summary>
        public ObservableCollection<RecentFileItem> RecentFiles { get; } = [];

        /// <summary>There is at least one recent project (the menu is enabled).</summary>
        public Boolean AnyRecentFiles => _preferences.RecentFiles.Count > 0;

        private void LoadRecentFiles()
        {
            RecentFiles.Clear();
            foreach (String path in _preferences.RecentFiles)
                RecentFiles.Add(new RecentFileItem(path, OpenRecentCommand));
            if (RecentFiles.Count > 0)
                RecentFiles.Add(new RecentFileItem(_localizer.T("Clear list"), ClearRecentFilesCommand, isClear: true));
            OnPropertyChanged(nameof(AnyRecentFiles));
            ClearRecentFilesCommand.NotifyCanExecuteChanged();
            OpenRecentCommand.NotifyCanExecuteChanged();
        }

        private void RememberRecent(String path)
        {
            _preferences.AddRecentFile(Path.GetFullPath(path));
            LoadRecentFiles();
        }

        [RelayCommand(CanExecute = nameof(CanStartJob))]
        private void OpenRecent(String? path)
        {
            if (path is null)
                return;
            if (!File.Exists(path))
            {
                _dialogs.ShowWarning(_localizer.F("'{0}' was not found.", path));
                _preferences.RemoveRecentFile(path);
                LoadRecentFiles();
                return;
            }
            OpenPath(path);
        }

        [RelayCommand(CanExecute = nameof(CanClearRecentFiles))]
        private void ClearRecentFiles()
        {
            _preferences.ClearRecentFiles();
            LoadRecentFiles();
        }

        // ------------------------------------------------------------------ file actions

        [RelayCommand(CanExecute = nameof(CanStartJob))]
        private void New()
        {
            if (!ConfirmDiscardChanges())
                return;
            Service.NewProject();
            Session.Refresh();
        }

        [RelayCommand(CanExecute = nameof(CanStartJob))]
        private void Open()
        {
            String[] files = _dialogs.OpenFiles("Open Project", FrmFilter, false);
            if (files.Length > 0)
                OpenPath(files[0]);
        }

        /// <summary>
        /// Opens a project or an FRM (the dialog, a recent file, a file dropped on the window, the command-line argument). Returns
        /// <c>false</c> when the user kept the current project or the file could not be opened.
        /// </summary>
        public Boolean OpenPath(String path)
        {
            if (!ConfirmDiscardChanges())
                return false;
            Boolean opened = Service.OpenFile(path);
            Session.Refresh();
            if (opened)
                RememberRecent(path);
            return opened;
        }

        /// <summary>
        /// Files dropped on the window: a project or FRM is opened, images are added as frames of the current direction (in name order),
        /// a palette is loaded.
        /// </summary>
        public void DropFiles(IReadOnlyList<String> files)
        {
            if (!Session.CanStartJob || files.Count == 0)
                return;

            String? project = files.FirstOrDefault(IsProjectFile);
            if (project is not null)
            {
                OpenPath(project);
                return;
            }

            List<String> images = files.Where(IsImageFile).ToList();
            if (images.Count > 0)
            {
                images.Sort(CompareNames);
                FrameList.AddFrameFiles(images);
                return;
            }

            String? palette = files.FirstOrDefault(IsPaletteFile);
            if (palette is not null)
            {
                Service.LoadPalette(palette);
                Session.Refresh();
                return;
            }

            _dialogs.ShowWarning(_localizer.F("Unknown file type '{0}'!", Path.GetExtension(files[0])));
        }

        /// <summary>Whether files being dragged over the window would be accepted.</summary>
        public Boolean CanDrop(IReadOnlyList<String> files)
            => Session.CanStartJob && files.Any(file => IsProjectFile(file) || IsImageFile(file) || IsPaletteFile(file));

        private static Boolean IsProjectFile(String file)
        {
            String extension = Path.GetExtension(file).ToUpperInvariant();
            return extension == ".FPR" || extension == ".FRM" || (extension.Length == 4 && extension.StartsWith(".FR", StringComparison.Ordinal) && Char.IsAsciiDigit(extension[3]));
        }

        private static Boolean IsImageFile(String file) => Path.GetExtension(file).ToUpperInvariant() is ".BMP" or ".GIF" or ".PNG";

        private static Boolean IsPaletteFile(String file) => Path.GetExtension(file).ToUpperInvariant() is ".ACT" or ".PAL";

        /// <summary>Name order where numbers count by value (<c>frame2</c> before <c>frame10</c>), as Explorer sorts.</summary>
        private static Int32 CompareNames(String a, String b)
            => StringComparer.Create(System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.NumericOrdering | System.Globalization.CompareOptions.IgnoreCase)
                .Compare(Path.GetFileName(a), Path.GetFileName(b));

        /// <summary>
        /// Runs a script on the project on screen. The script runs on another thread so that the window stays alive; it can be stopped
        /// (<see cref="CancelCommand"/>) between two lines.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartJob))]
        private async Task ExecuteScriptAsync()
        {
            String[] files = _dialogs.OpenFiles("Open Script", "Action Script (*.FAS)|*.FAS", false);
            if (files.Length == 0)
                return;
            Project.Undo.Clear();
            using (Session.BeginBusy())
            {
                try
                {
                    await Task.Run(() => new ScriptRunner(Service).Run(files[0]));
                }
                catch (Exception e)
                {
                    _dialogs.ShowWarning(e.Message);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void Save() => SaveCore();

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void SaveAs() => SaveAsCore();

        /// <summary>Saves to the project file, asking for a name first when there is none. Returns <c>false</c> when nothing was saved.</summary>
        private Boolean SaveCore()
        {
            if (!String.Equals(PathHelpers.ExtractFileExt(Project.ProjectFile), ".FPR", StringComparison.OrdinalIgnoreCase))
                return SaveAsCore();
            Boolean saved = Service.SaveProject(Project.ProjectFile);
            Session.Refresh();
            if (saved)
                RememberRecent(Project.ProjectFile);
            return saved;
        }

        private Boolean SaveAsCore()
        {
            String suggested = String.IsNullOrEmpty(Project.ProjectFile) ? "Untitled.fpr" : PathHelpers.ChangeFileExt(Project.ProjectFile, ".FPR");
            String? path = _dialogs.SaveFile("Save Project As", "Project (*.FPR)|*.FPR", 1, suggested, "FPR");
            if (path is null)
                return false;
            Boolean saved = Service.SaveProject(path);
            Session.Refresh();
            if (saved)
                RememberRecent(path);
            return saved;
        }

        [RelayCommand(CanExecute = nameof(CanCreateFrm))]
        private void CreateFrm()
        {
            String? path = _dialogs.SaveFile("Create FRM", "FRM (*.FRM)|*.FRM|FR (*.FR0 - *.FR5)|*.FR0", 1, Project.FrmFile, "FRM");
            if (path is null)
                return;
            Service.CreateFrm(path);
            Session.Refresh();
        }

        /// <summary>
        /// Batch processing. The batch works on a project of its own: the one on screen is neither read nor replaced (the original
        /// loaded every source into it), so nothing is lost and the window stays usable while the batch runs.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartJob))]
        private async Task AutomateAsync()
        {
            AutomationOptions? options = _dialogs.ShowAutomate(Project.PaletteFile);
            if (options is null)
                return;

            ProjectService batch = new(new Project(), Session.Interaction, Session.AppDirectory)
            {
                ConfirmPaletteOnOpen = false,
                SaveRelativePaths = _preferences.SaveRelativePaths,
            };
            using (Session.BeginBatch())
            {
                try
                {
                    await Task.Run(() => new AutomationRunner(batch).Run(options));
                }
                catch (Exception e)
                {
                    _dialogs.ShowWarning(e.Message);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanCancel))]
        private void Cancel()
        {
            if (CanCancel() && _dialogs.AskYesNo("Stop automated routine?"))
                Session.Interaction.CancelRequested = true;
        }

        [RelayCommand(CanExecute = nameof(CanStartJob))]
        private void Exit()
        {
            if (!ConfirmClose())
                return;
            _closeConfirmed = true; // closing the windows must not ask again
            Application.Current.Shutdown();
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void About() => _dialogs.ShowAbout();

        /// <summary>Opens (or closes) the window that plays the current direction.</summary>
        [RelayCommand]
        private void TogglePreview() => _dialogs.TogglePreview(Preview);

        // ------------------------------------------------------------------ edit actions

        [RelayCommand(CanExecute = nameof(CanUndo))]
        private void Undo()
        {
            Int32 dir = Editor.Undo();
            if (dir >= 0)
                Project.CurrentDirection = dir;
            Session.Refresh();
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void LoadPalette()
        {
            String[] files = _dialogs.OpenFiles("Load Palette", PaletteFilter, false);
            if (files.Length > 0)
                Service.LoadPalette(files[0]);
            Session.Refresh();
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void Options()
        {
            if (_dialogs.ShowOptions(Project.Settings, Project.Directions[0].Count))
                Session.Refresh();
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void LoadBackground()
        {
            String[] files = _dialogs.OpenFiles("Load Background", "Background (*.BMP)|*.BMP", false);
            if (files.Length > 0)
                Service.LoadBackground(files[0]);
            Session.Refresh();
        }
    }
}
