namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Threading;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using CommunityToolkit.Mvvm.Messaging;
    using FrameAnimator.Core.Formats;
    using FrameAnimator.Core.Geometry;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;
    using FrameAnimator.Core.Services;
    using Infrastructure;
    using Localization;
    using Views;

    /// <summary>Active tool of the drawing area (byte <c>+0x88e</c> of the original).</summary>
    public enum EditTool
    {
        None = 0,
        Move = 1,
        Pick = 2,
    }

    /// <summary>
    /// View model of the main window: reproduces the actions, state and enabling rules of
    /// <c>TfrmFrameAnimator</c> (specification §6.1 to §6.4), relying on <see cref="ProjectService"/> and <see cref="ProjectEditor"/>.
    /// Built on CommunityToolkit.Mvvm: observable properties and commands are generated from attributes, and the language and
    /// theme changes arrive as messages.
    /// </summary>
    public sealed partial class MainViewModel : ObservableObject, IRecipient<LanguageChangedMessage>, IRecipient<ThemeChangedMessage>
    {
        private const String FrmFilter = "All (*.FPR,*.FRM,*.FR0-*.FR5)|*.FPR;*.FRM;*.FR0;*.FR1;*.FR2;*.FR3;*.FR4;*.FR5|Project (*.FPR)|*.FPR|FRM (*.FRM,*.FR0-*.FR5)|*.FRM;*.FR0;*.FR1;*.FR2;*.FR3;*.FR4;*.FR5";
        private const String ImageFilter = "All (*.BMP, *.GIF, *.PNG)|*.BMP;*.GIF;*.PNG|BMP (*.BMP)|*.BMP|CompuServe GIF (*.GIF)|*.GIF|PNG (*.PNG)|*.PNG";
        private const String PaletteFilter = "All (*.ACT, *.PAL)|*.ACT;*.PAL|Palette (*.ACT)|*.ACT|Fallout Palette (*.PAL)|*.PAL";

        private readonly Dispatcher _dispatcher;
        private readonly IDialogService _dialogs;
        private Boolean _refreshPending;
        private Boolean _rebuilding;

        private Int32 _selectedFrameIndex = -1;
        private IReadOnlyList<Int32> _selectedIndices = [];
        private Int32 _zoom = 2;

        // dragging (Move tool)
        private Boolean _dragging;
        private Int32 _anchorX, _anchorY, _lastX, _lastY;

        public MainViewModel(Dispatcher dispatcher, IDialogService dialogs, String appDirectory)
        {
            _dispatcher = dispatcher;
            _dialogs = dialogs;
            Interaction = new WpfInteraction(dispatcher, dialogs);
            Interaction.ProgressChanged += OnProgress;
            Project = new Project();
            Service = new ProjectService(Project, Interaction, appDirectory) { ConfirmPaletteOnOpen = false };
            Editor = new ProjectEditor(Project);
            Service.Changed += (_, _) => ScheduleRefresh();
            Service.NewProject();

            ThemeChoices = Enum.GetValues<ThemePreference>()
                .Select(preference => new MenuChoice(Localizer.T(preference.ToString()), ChangeThemeCommand, preference))
                .ToArray();
            LanguageChoices = Localizer.Languages
                .Select(language => new MenuChoice(language.NativeName, ChangeLanguageCommand, language.Code))
                .ToArray();
            UpdateChoices();

            WeakReferenceMessenger.Default.RegisterAll(this);

            RefreshAll();
        }

        // ------------------------------------------------------------------ state

        public WpfInteraction Interaction { get; }
        public Project Project { get; }
        public ProjectService Service { get; }
        public ProjectEditor Editor { get; }

        public ObservableCollection<String> Frames { get; } = [];

        /// <summary>Raised to redraw the drawing area.</summary>
        public event EventHandler? RenderRequested;

        /// <summary>Entries of the theme menu (Automatic / Light / Dark).</summary>
        public IReadOnlyList<MenuChoice> ThemeChoices { get; }

        /// <summary>Entries of the language menu.</summary>
        public IReadOnlyList<MenuChoice> LanguageChoices { get; }

        public IReadOnlyList<LocalizedText> DirectionNames { get; } = Project.DirectionNames.Select(name => Localizer.Instance.Observe(name)).ToArray();

        public Direction CurrentDir => Project.Directions[Project.CurrentDirection];
        public Int32 CurrentFrameCount => CurrentDir.Count;
        public Boolean HasFrames => CurrentFrameCount > 0;

        public Int32 CurrentDirection
        {
            get => Project.CurrentDirection;
            set
            {
                if (value < 0 || value >= Project.DirectionCount || value == Project.CurrentDirection)
                    return;
                Project.CurrentDirection = value;
                OnPropertyChanged();
                _dragging = false;
                _selectedFrameIndex = -1;
                RefreshAll();
            }
        }

        /// <summary>The current frame (focused item of the list); ignored while the list is being rebuilt.</summary>
        public Int32 SelectedFrameIndex
        {
            get => _selectedFrameIndex;
            set
            {
                if (_rebuilding)
                    return;
                if (SetProperty(ref _selectedFrameIndex, value))
                    OnSelectionChanged();
            }
        }

        /// <summary>Indices of all selected items (multi-select list).</summary>
        public void SetSelectedIndices(IReadOnlyList<Int32> indices)
        {
            if (_rebuilding)
                return;
            _selectedIndices = indices;
            UpdateCommands();
        }

        [ObservableProperty]
        public partial Boolean ShowFullNames { get; set; }

        [ObservableProperty]
        public partial Boolean ShowBackground { get; set; } = true;

        [ObservableProperty]
        public partial Boolean ShowHexes { get; set; }

        [ObservableProperty]
        public partial Boolean ShowGhost { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsMoveTool))]
        [NotifyPropertyChangedFor(nameof(IsPickTool))]
        public partial EditTool Tool { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsIdle))]
        public partial Boolean IsBusy { get; private set; }

        [ObservableProperty]
        public partial Int32 ProgressValue { get; private set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProgressVisible))]
        public partial Int32 ProgressMaximum { get; private set; }

        partial void OnShowFullNamesChanged(Boolean value) => RebuildFrameList();

        partial void OnShowBackgroundChanged(Boolean value) => Render();

        partial void OnShowHexesChanged(Boolean value) => Render();

        partial void OnShowGhostChanged(Boolean value) => Render();

        partial void OnToolChanged(EditTool value) => _dragging = false;

        partial void OnIsBusyChanged(Boolean value) => UpdateCommands();

        /// <summary>Windows "legacy" dialogs (files, messages) instead of the application's own.</summary>
        public Boolean UseSystemDialogs
        {
            get => DialogPreferences.UseSystemDialogs;
            set
            {
                if (DialogPreferences.UseSystemDialogs == value)
                    return;
                DialogPreferences.UseSystemDialogs = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Remember the position of all windows.</summary>
        public Boolean SaveWindowPosition
        {
            get => WindowMemory.SavePosition;
            set
            {
                if (WindowMemory.SavePosition == value)
                    return;
                WindowMemory.SavePosition = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Remember the size of all windows.</summary>
        public Boolean SaveWindowSize
        {
            get => WindowMemory.SaveSize;
            set
            {
                if (WindowMemory.SaveSize == value)
                    return;
                WindowMemory.SaveSize = value;
                OnPropertyChanged();
            }
        }

        public Boolean IsMoveTool => Tool == EditTool.Move;
        public Boolean IsPickTool => Tool == EditTool.Pick;

        public Boolean IsIdle => !IsBusy;

        public Int32 Zoom
        {
            get => _zoom;
            set
            {
                if (SetProperty(ref _zoom, Math.Clamp(value, 1, 8)))
                {
                    OnPropertyChanged(nameof(ZoomText));
                    ZoomInCommand.NotifyCanExecuteChanged();
                    ZoomOutCommand.NotifyCanExecuteChanged();
                    Render();
                }
            }
        }

        public String ZoomText => $"{_zoom * 100} %";

        public String Title => $"{Project.Title} — Frame Animator";

        // status bar (panels of the original)
        public String StatusFrame => HasFrames ? Localizer.F("Frame {0}/{1}", SelectedFrameIndex + 1, CurrentFrameCount) : "";

        public String StatusTransparent => Localizer.T(Project.Settings.TransparentMode switch
        {
            TransparentMode.Auto => "Transp: Auto",
            TransparentMode.Palette => "Transp: Palette",
            _ => "Transp: Sample",
        });

        public String StatusPalette => Localizer.F("Pal: {0}", String.IsNullOrEmpty(Project.PaletteFile) ? "Default.act" : PathHelpers.ExtractFileName(Project.PaletteFile));
        public String StatusAutoCutting => Project.Settings.AutoCutting ? Localizer.T("Autocutting") : "";
        public String StatusLock => Project.Settings.LockOffsets ? Localizer.T("Lock Offsets") : "";

        public Boolean ProgressVisible => ProgressMaximum > 0;

        public Brush SampleBrush => new SolidColorBrush(ImageConversion.ToColor(Project.Settings.SampleColor));

        public String SampleText => Localizer.T("Sample");

        public String SampleToolTip => Localizer.F("Sample color #{0} (click to change)", (Project.Settings.SampleColor & 0xFFFFFF).ToString("X6"));

        // ------------------------------------------------------------------ messages

        /// <summary>The language changed: every text computed by this view model is refreshed.</summary>
        public void Receive(LanguageChangedMessage message)
        {
            UpdateChoices();
            RefreshAll();
        }

        /// <summary>The theme changed: the drawing area uses theme colors.</summary>
        public void Receive(ThemeChangedMessage message)
        {
            UpdateChoices();
            Render();
        }

        /// <summary>Texts and check marks of the theme and language menus follow the current choices.</summary>
        private void UpdateChoices()
        {
            foreach (MenuChoice choice in ThemeChoices)
            {
                ThemePreference preference = (ThemePreference)choice.Parameter!;
                choice.Header = Localizer.T(preference.ToString());
                choice.IsCurrent = preference == ThemeManager.Preference;
            }
            foreach (MenuChoice choice in LanguageChoices)
                choice.IsCurrent = (String)choice.Parameter! == Localizer.Instance.Language;
        }

        // ------------------------------------------------------------------ enabling rules

        private Boolean CanCreateFrm() => IsIdle && FrmFormat.IsValidForFrm(Project);

        private Boolean CanUndo() => IsIdle && Editor.CanUndo;

        private Boolean CanUseFrames() => IsIdle && HasFrames;

        private Boolean CanMoveFrameDown() => CanUseFrames() && SelectedFrameIndex >= 0 && SelectedFrameIndex < CurrentFrameCount - 1;

        private Boolean CanMoveFrameUp() => CanUseFrames() && SelectedFrameIndex > 0;

        private Boolean CanEditFrameOffset()
            => CanUseFrames() && SelectedFrameIndex >= 0 && (SelectedFrameIndex == 0 || !Project.Settings.LockOffsets);

        private Boolean CanRemoveFrames() => IsIdle && _selectedIndices.Count > 0;

        private Boolean CanZoomIn() => Zoom < 8;

        private Boolean CanZoomOut() => Zoom > 1;

        /// <summary>
        /// Commands do not poll WPF: after any change that can alter an enabling rule, this re-evaluates them all
        /// (busy state, frames, selection, undo stack, locked offsets, zoom).
        /// </summary>
        private void UpdateCommands()
        {
            NewCommand.NotifyCanExecuteChanged();
            OpenCommand.NotifyCanExecuteChanged();
            ExecuteScriptCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            SaveAsCommand.NotifyCanExecuteChanged();
            CreateFrmCommand.NotifyCanExecuteChanged();
            AutomateCommand.NotifyCanExecuteChanged();
            ExitCommand.NotifyCanExecuteChanged();
            UndoCommand.NotifyCanExecuteChanged();
            FramesOffsetCommand.NotifyCanExecuteChanged();
            AddFramesCommand.NotifyCanExecuteChanged();
            FrameDownCommand.NotifyCanExecuteChanged();
            FrameUpCommand.NotifyCanExecuteChanged();
            FrameOffsetCommand.NotifyCanExecuteChanged();
            RemoveFramesCommand.NotifyCanExecuteChanged();
            LoadPaletteCommand.NotifyCanExecuteChanged();
            OptionsCommand.NotifyCanExecuteChanged();
            LoadBackgroundCommand.NotifyCanExecuteChanged();
            AboutCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            ToggleMoveCommand.NotifyCanExecuteChanged();
            TogglePickCommand.NotifyCanExecuteChanged();
            PickSampleColorCommand.NotifyCanExecuteChanged();
            ZoomInCommand.NotifyCanExecuteChanged();
            ZoomOutCommand.NotifyCanExecuteChanged();
        }

        // ------------------------------------------------------------------ refresh

        private void ScheduleRefresh()
        {
            if (_refreshPending || IsBusy)
                return;
            _refreshPending = true;
            _dispatcher.BeginInvoke(() =>
            {
                _refreshPending = false;
                RefreshAll();
            }, DispatcherPriority.Background);
        }

        /// <summary>Equivalent of <c>0x4025bc</c>: list, status bar, action enabling and drawing.</summary>
        public void RefreshAll()
        {
            RebuildFrameList();
            OnPropertyChanged(nameof(CurrentDirection));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(StatusTransparent));
            OnPropertyChanged(nameof(StatusPalette));
            OnPropertyChanged(nameof(StatusAutoCutting));
            OnPropertyChanged(nameof(StatusLock));
            OnPropertyChanged(nameof(SampleBrush));
            OnPropertyChanged(nameof(SampleText));
            OnPropertyChanged(nameof(SampleToolTip));
            UpdateCommands();
        }

        private String FrameText(Frame f)
        {
            var name = ShowFullNames ? f.FileName : PathHelpers.ExtractFileName(f.FileName);
            return f.HasSourceIndex ? name + "[" + f.SourceIndex + "]" : name;
        }

        private void RebuildFrameList()
        {
            var keep = _selectedFrameIndex;
            _rebuilding = true;
            try
            {
                Frames.Clear();
                foreach (var f in CurrentDir.Frames)
                    Frames.Add(FrameText(f));
            }
            finally
            {
                _rebuilding = false;
            }

            if (Frames.Count == 0)
                keep = -1;
            else
                keep = Math.Clamp(keep, 0, Frames.Count - 1);
            _selectedFrameIndex = keep;
            _selectedIndices = keep >= 0 ? [keep] : [];
            OnPropertyChanged(nameof(SelectedFrameIndex));
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            OnPropertyChanged(nameof(StatusFrame));
            UpdateCommands();
            Render();
        }

        public void Render() => RenderRequested?.Invoke(this, EventArgs.Empty);

        private void OnProgress(Int32 done, Int32 total)
        {
            ProgressValue = done;
            ProgressMaximum = total;
        }

        // ------------------------------------------------------------------ file actions

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void New()
        {
            Service.NewProject();
            RefreshAll();
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void Open()
        {
            var files = _dialogs.OpenFiles("Open Project", FrmFilter, false);
            if (files.Length > 0)
                OpenPath(files[0]);
        }

        /// <summary>Opens a project or an FRM (also used for the command-line argument).</summary>
        public void OpenPath(String path)
        {
            Service.OpenFile(path);
            RefreshAll();
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void ExecuteScript()
        {
            var files = _dialogs.OpenFiles("Open Script", "Action Script (*.FAS)|*.FAS", false);
            if (files.Length == 0)
                return;
            Project.Undo.Clear();
            IsBusy = true;
            try
            {
                new ScriptRunner(Service).Run(files[0]);
            }
            finally
            {
                IsBusy = false;
                RefreshAll();
            }
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void Save()
        {
            if (!String.Equals(PathHelpers.ExtractFileExt(Project.ProjectFile), ".FPR", StringComparison.OrdinalIgnoreCase))
            {
                SaveAs();
                return;
            }
            Service.SaveProject(Project.ProjectFile);
            RefreshAll();
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void SaveAs()
        {
            var suggested = String.IsNullOrEmpty(Project.ProjectFile) ? "Untitled.fpr" : PathHelpers.ChangeFileExt(Project.ProjectFile, ".FPR");
            var path = _dialogs.SaveFile("Save Project As", "Project (*.FPR)|*.FPR", 1, suggested, "FPR");
            if (path is null)
                return;
            Service.SaveProject(path);
            RefreshAll();
        }

        [RelayCommand(CanExecute = nameof(CanCreateFrm))]
        private void CreateFrm()
        {
            var path = _dialogs.SaveFile("Create FRM", "FRM (*.FRM)|*.FRM|FR (*.FR0 - *.FR5)|*.FR0", 1, Project.FrmFile, "FRM");
            if (path is null)
                return;
            Service.CreateFrm(path);
            RefreshAll();
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private async Task AutomateAsync()
        {
            var options = _dialogs.ShowAutomate(Project.PaletteFile);
            if (options is null)
                return;

            IsBusy = true;
            Interaction.CancelRequested = false;
            try
            {
                var runner = new AutomationRunner(Service);
                await Task.Run(() => runner.Run(options));
            }
            catch (Exception e)
            {
                _dialogs.ShowWarning(e.Message);
            }
            finally
            {
                IsBusy = false;
                RefreshAll();
            }
        }

        [RelayCommand(CanExecute = nameof(IsBusy))]
        private void Cancel()
        {
            if (IsBusy && _dialogs.AskYesNo("Stop automated routine?"))
                Interaction.CancelRequested = true;
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void Exit() => Application.Current.Shutdown();

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void About() => _dialogs.ShowAbout();

        // ------------------------------------------------------------------ edit actions

        [RelayCommand(CanExecute = nameof(CanUndo))]
        private void Undo()
        {
            var dir = Editor.Undo();
            if (dir >= 0)
                Project.CurrentDirection = dir;
            RefreshAll();
        }

        [RelayCommand(CanExecute = nameof(CanUseFrames))]
        private void FramesOffset()
        {
            var dir = Project.CurrentDirection;
            var (x, y) = Editor.GetFramesOffset(dir);
            var result = _dialogs.ShowOffset(new OffsetDialogRequest { Title = "Frames Offset", X = x, Y = y });
            if (result is { } r)
                Editor.SetFramesOffset(dir, r.X, r.Y);
            RefreshAll();
        }

        [RelayCommand(CanExecute = nameof(CanEditFrameOffset))]
        private void FrameOffset()
        {
            var dir = Project.CurrentDirection;
            var index = SelectedFrameIndex;
            var (x, y) = Editor.GetFrameOffset(dir, index);

            OffsetDialogRequest request = index > 0
                ? new OffsetDialogRequest
                {
                    Title = "Frame Offset",
                    X = x,
                    Y = y,
                    ShowInherit = true,
                    ShowWizard = true,
                    WizardEnabled = false, // the wizard is only enabled for the first frame
                }
                : new OffsetDialogRequest
                {
                    Title = "First Frame Offset",
                    X = x,
                    Y = y,
                    ShowInherit = true,
                    InheritEnabled = false,
                    InheritChecked = true,
                    ShowWizard = true,
                    WizardEnabled = true,
                    Wizard = px => Editor.OffsetWizard(dir, px),
                };

            var result = _dialogs.ShowOffset(request);
            if (result is { } r)
                Editor.SetFrameOffset(dir, index, r.X, r.Y, index > 0 && r.Inherit);
            RefreshAll();
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void AddFrames()
        {
            var files = _dialogs.OpenFiles("Add Frames", ImageFilter, true);
            if (files.Length == 0)
                return;

            var dir = CurrentDir;
            for (var i = 0; i < files.Length; i++)
            {
                if (Service.LoadFrameFile(dir, files[i], -1) is null && i < files.Length - 1
                    && !_dialogs.AskYesNo("Continue loading frames?"))
                    break;
            }
            Project.Undo.Clear();
            RefreshAll();
        }

        [RelayCommand(CanExecute = nameof(CanMoveFrameDown))]
        private void FrameDown() => MoveFrame(1);

        [RelayCommand(CanExecute = nameof(CanMoveFrameUp))]
        private void FrameUp() => MoveFrame(-1);

        private void MoveFrame(Int32 delta)
        {
            var index = SelectedFrameIndex;
            Editor.MoveFrame(Project.CurrentDirection, index, delta);
            _selectedFrameIndex = index + delta;
            RefreshAll();
        }

        [RelayCommand(CanExecute = nameof(CanRemoveFrames))]
        private void RemoveFrames()
        {
            Editor.RemoveFrames(Project.CurrentDirection, _selectedIndices);
            RefreshAll();
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void LoadPalette()
        {
            var files = _dialogs.OpenFiles("Load Palette", PaletteFilter, false);
            if (files.Length > 0)
                Service.LoadPalette(files[0]);
            RefreshAll();
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void Options()
        {
            if (_dialogs.ShowOptions(Project.Settings, Project.Directions[0].Count))
                RefreshAll();
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        private void LoadBackground()
        {
            var files = _dialogs.OpenFiles("Load Background", "Background (*.BMP)|*.BMP", false);
            if (files.Length > 0)
                Service.LoadBackground(files[0]);
            RefreshAll();
        }

        // ------------------------------------------------------------------ tools and view

        [RelayCommand(CanExecute = nameof(CanUseFrames))]
        private void ToggleMove() => Tool = Tool == EditTool.Move ? EditTool.None : EditTool.Move;

        [RelayCommand(CanExecute = nameof(CanUseFrames))]
        private void TogglePick() => Tool = Tool == EditTool.Pick ? EditTool.None : EditTool.Pick;

        [RelayCommand(CanExecute = nameof(CanUseFrames))]
        private void PickSampleColor()
        {
            var color = _dialogs.PickColor(Project.Settings.SampleColor);
            if (color is { } c)
                SetSampleColor(c);
        }

        private void SetSampleColor(Int32 rgb)
        {
            Project.Settings.SampleColor = rgb & 0xFFFFFF;
            OnPropertyChanged(nameof(SampleBrush));
            OnPropertyChanged(nameof(SampleText));
            OnPropertyChanged(nameof(SampleToolTip));
            Render();
        }

        [RelayCommand]
        private void ChangeTheme(ThemePreference preference) => ThemeManager.SetPreference(preference);

        [RelayCommand]
        private void ChangeLanguage(String code) => Localizer.Instance.SetLanguage(code);

        [RelayCommand]
        private void ToggleBackground() => ShowBackground = !ShowBackground;

        [RelayCommand]
        private void ToggleHexes() => ShowHexes = !ShowHexes;

        [RelayCommand]
        private void ToggleGhost() => ShowGhost = !ShowGhost;

        [RelayCommand(CanExecute = nameof(CanZoomIn))]
        private void ZoomIn() => Zoom++;

        [RelayCommand(CanExecute = nameof(CanZoomOut))]
        private void ZoomOut() => Zoom--;

        [RelayCommand]
        private void ZoomReset() => Zoom = 1;

        // ------------------------------------------------------------------ mouse (coordinates relative to the center)

        public void CanvasMouseDown(Int32 x, Int32 y)
        {
            if (IsBusy || !HasFrames || SelectedFrameIndex < 0)
                return;

            switch (Tool)
            {
                case EditTool.Move:
                    _dragging = true;
                    _anchorX = _lastX = x;
                    _anchorY = _lastY = y;
                    Editor.BeginDrag();
                    break;

                case EditTool.Pick:
                {
                    var (fx, fy) = FrameMath.DisplayPosition(CurrentDir, SelectedFrameIndex, 0, 0);
                    var bmp = CurrentDir.Frames[SelectedFrameIndex].Bitmap;
                    var px = x - fx;
                    var py = y - fy;
                    if (px >= 0 && py >= 0 && px < bmp.Width && py < bmp.Height)
                        SetSampleColor(bmp[px, py]);
                    break;
                }
            }
        }

        public void CanvasMouseMove(Int32 x, Int32 y, Boolean shift, Boolean ctrl)
        {
            if (!_dragging || IsBusy || !HasFrames)
                return;

            var ny = shift ? ProjectEditor.ConstrainY(Project.CurrentDirection, _anchorX, _anchorY, x) : y;
            Editor.DragMove(Project.CurrentDirection, SelectedFrameIndex, x - _lastX, ny - _lastY, ctrl);
            _lastX = x;
            _lastY = ny;
            Render();
            UndoCommand.NotifyCanExecuteChanged();
        }

        public void CanvasMouseUp() => _dragging = false;
    }
}
