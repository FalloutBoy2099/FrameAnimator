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
    /// </summary>
    public sealed class MainViewModel : ObservableObject
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
        private Boolean _showFullNames;
        private Boolean _showBackground = true;
        private Boolean _showHexes;
        private Boolean _showGhost;
        private EditTool _tool;
        private Boolean _isBusy;
        private Int32 _zoom = 2;
        private Int32 _progressValue;
        private Int32 _progressMax;

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

            NewCommand = new RelayCommand(DoNew, NotBusy);
            OpenCommand = new RelayCommand(DoOpen, NotBusy);
            ExecuteScriptCommand = new RelayCommand(DoExecuteScript, NotBusy);
            SaveCommand = new RelayCommand(DoSave, NotBusy);
            SaveAsCommand = new RelayCommand(DoSaveAs, NotBusy);
            CreateFrmCommand = new RelayCommand(DoCreateFrm, () => !IsBusy && FrmFormat.IsValidForFrm(Project));
            AutomateCommand = new RelayCommand(DoAutomate, NotBusy);
            ExitCommand = new RelayCommand(() => Application.Current.Shutdown(), NotBusy);
            UndoCommand = new RelayCommand(DoUndo, () => !IsBusy && Editor.CanUndo);
            FramesOffsetCommand = new RelayCommand(DoFramesOffset, () => !IsBusy && HasFrames);
            AddFramesCommand = new RelayCommand(DoAddFrames, NotBusy);
            FrameDownCommand = new RelayCommand(() => DoMoveFrame(1), () => !IsBusy && HasFrames && SelectedFrameIndex >= 0 && SelectedFrameIndex < CurrentFrameCount - 1);
            FrameUpCommand = new RelayCommand(() => DoMoveFrame(-1), () => !IsBusy && HasFrames && SelectedFrameIndex > 0);
            FrameOffsetCommand = new RelayCommand(DoFrameOffset, () => !IsBusy && HasFrames && SelectedFrameIndex >= 0 && (SelectedFrameIndex == 0 || !Project.Settings.LockOffsets));
            RemoveFramesCommand = new RelayCommand(DoRemoveFrames, () => !IsBusy && _selectedIndices.Count > 0);
            LoadPaletteCommand = new RelayCommand(DoLoadPalette, NotBusy);
            OptionsCommand = new RelayCommand(DoOptions, NotBusy);
            LoadBackgroundCommand = new RelayCommand(DoLoadBackground, NotBusy);
            AboutCommand = new RelayCommand(() => _dialogs.ShowAbout(), NotBusy);
            CancelCommand = new RelayCommand(DoCancel, () => IsBusy);
            ToggleMoveCommand = new RelayCommand(() => Tool = Tool == EditTool.Move ? EditTool.None : EditTool.Move, () => !IsBusy && HasFrames);
            TogglePickCommand = new RelayCommand(() => Tool = Tool == EditTool.Pick ? EditTool.None : EditTool.Pick, () => !IsBusy && HasFrames);
            PickSampleColorCommand = new RelayCommand(DoPickSampleColor, () => !IsBusy && HasFrames);
            ToggleBackgroundCommand = new RelayCommand(() => ShowBackground = !ShowBackground);
            ToggleHexesCommand = new RelayCommand(() => ShowHexes = !ShowHexes);
            ToggleGhostCommand = new RelayCommand(() => ShowGhost = !ShowGhost);
            ZoomInCommand = new RelayCommand(() => Zoom++, () => Zoom < 8);
            ZoomOutCommand = new RelayCommand(() => Zoom--, () => Zoom > 1);
            ZoomResetCommand = new RelayCommand(() => Zoom = 1);

            Localizer.Instance.LanguageChanged += (_, _) => RefreshAll();
            ThemeManager.Changed += (_, _) => Render();

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

        public Int32 SelectedFrameIndex
        {
            get => _selectedFrameIndex;
            set
            {
                if (_rebuilding)
                    return;
                if (SetField(ref _selectedFrameIndex, value))
                    OnSelectionChanged();
            }
        }

        /// <summary>Indices of all selected items (multi-select list).</summary>
        public void SetSelectedIndices(IReadOnlyList<Int32> indices)
        {
            if (_rebuilding)
                return;
            _selectedIndices = indices;
            RelayCommand.RaiseCanExecuteChanged();
        }

        public Boolean ShowFullNames
        {
            get => _showFullNames;
            set
            {
                if (SetField(ref _showFullNames, value))
                    RebuildFrameList();
            }
        }

        public Boolean ShowBackground
        {
            get => _showBackground;
            set
            {
                if (SetField(ref _showBackground, value))
                    Render();
            }
        }

        public Boolean ShowHexes
        {
            get => _showHexes;
            set
            {
                if (SetField(ref _showHexes, value))
                    Render();
            }
        }

        public Boolean ShowGhost
        {
            get => _showGhost;
            set
            {
                if (SetField(ref _showGhost, value))
                    Render();
            }
        }

        public EditTool Tool
        {
            get => _tool;
            set
            {
                if (!SetField(ref _tool, value))
                    return;
                OnPropertyChanged(nameof(IsMoveTool));
                OnPropertyChanged(nameof(IsPickTool));
                _dragging = false;
            }
        }

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

        public Boolean IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetField(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsIdle));
                    RelayCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public Boolean IsIdle => !IsBusy;

        public Int32 Zoom
        {
            get => _zoom;
            set
            {
                if (SetField(ref _zoom, Math.Clamp(value, 1, 8)))
                {
                    OnPropertyChanged(nameof(ZoomText));
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

        public Int32 ProgressValue
        {
            get => _progressValue;
            private set => SetField(ref _progressValue, value);
        }

        public Int32 ProgressMaximum
        {
            get => _progressMax;
            private set => SetField(ref _progressMax, value);
        }

        public Boolean ProgressVisible => _progressMax > 0;

        public Brush SampleBrush => new SolidColorBrush(ImageConversion.ToColor(Project.Settings.SampleColor));

        public String SampleText => Localizer.T("Sample");

        public String SampleToolTip => Localizer.F("Sample color #{0} (click to change)", (Project.Settings.SampleColor & 0xFFFFFF).ToString("X6"));

        // ------------------------------------------------------------------ commands

        public RelayCommand NewCommand { get; }
        public RelayCommand OpenCommand { get; }
        public RelayCommand ExecuteScriptCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand SaveAsCommand { get; }
        public RelayCommand CreateFrmCommand { get; }
        public RelayCommand AutomateCommand { get; }
        public RelayCommand ExitCommand { get; }
        public RelayCommand UndoCommand { get; }
        public RelayCommand FramesOffsetCommand { get; }
        public RelayCommand AddFramesCommand { get; }
        public RelayCommand FrameDownCommand { get; }
        public RelayCommand FrameUpCommand { get; }
        public RelayCommand FrameOffsetCommand { get; }
        public RelayCommand RemoveFramesCommand { get; }
        public RelayCommand LoadPaletteCommand { get; }
        public RelayCommand OptionsCommand { get; }
        public RelayCommand LoadBackgroundCommand { get; }
        public RelayCommand AboutCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand ToggleMoveCommand { get; }
        public RelayCommand TogglePickCommand { get; }
        public RelayCommand PickSampleColorCommand { get; }
        public RelayCommand ToggleBackgroundCommand { get; }
        public RelayCommand ToggleHexesCommand { get; }
        public RelayCommand ToggleGhostCommand { get; }
        public RelayCommand ZoomInCommand { get; }
        public RelayCommand ZoomOutCommand { get; }
        public RelayCommand ZoomResetCommand { get; }

        private Boolean NotBusy() => !IsBusy;

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
            RelayCommand.RaiseCanExecuteChanged();
            Render();
        }

        public void Render() => RenderRequested?.Invoke(this, EventArgs.Empty);

        private void OnProgress(Int32 done, Int32 total)
        {
            ProgressValue = done;
            ProgressMaximum = total;
            OnPropertyChanged(nameof(ProgressVisible));
        }

        // ------------------------------------------------------------------ file actions

        private void DoNew()
        {
            Service.NewProject();
            RefreshAll();
        }

        private void DoOpen()
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

        private void DoExecuteScript()
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

        private void DoSave()
        {
            if (!String.Equals(PathHelpers.ExtractFileExt(Project.ProjectFile), ".FPR", StringComparison.OrdinalIgnoreCase))
            {
                DoSaveAs();
                return;
            }
            Service.SaveProject(Project.ProjectFile);
            RefreshAll();
        }

        private void DoSaveAs()
        {
            var suggested = String.IsNullOrEmpty(Project.ProjectFile) ? "Untitled.fpr" : PathHelpers.ChangeFileExt(Project.ProjectFile, ".FPR");
            var path = _dialogs.SaveFile("Save Project As", "Project (*.FPR)|*.FPR", 1, suggested, "FPR");
            if (path is null)
                return;
            Service.SaveProject(path);
            RefreshAll();
        }

        private void DoCreateFrm()
        {
            var path = _dialogs.SaveFile("Create FRM", "FRM (*.FRM)|*.FRM|FR (*.FR0 - *.FR5)|*.FR0", 1, Project.FrmFile, "FRM");
            if (path is null)
                return;
            Service.CreateFrm(path);
            RefreshAll();
        }

        private async void DoAutomate()
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

        private void DoCancel()
        {
            if (IsBusy && _dialogs.AskYesNo("Stop automated routine?"))
                Interaction.CancelRequested = true;
        }

        // ------------------------------------------------------------------ edit actions

        private void DoUndo()
        {
            var dir = Editor.Undo();
            if (dir >= 0)
                Project.CurrentDirection = dir;
            RefreshAll();
        }

        private void DoFramesOffset()
        {
            var dir = Project.CurrentDirection;
            var (x, y) = Editor.GetFramesOffset(dir);
            var result = _dialogs.ShowOffset(new OffsetDialogRequest { Title = "Frames Offset", X = x, Y = y });
            if (result is { } r)
                Editor.SetFramesOffset(dir, r.X, r.Y);
            RefreshAll();
        }

        private void DoFrameOffset()
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

        private void DoAddFrames()
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

        private void DoMoveFrame(Int32 delta)
        {
            var index = SelectedFrameIndex;
            Editor.MoveFrame(Project.CurrentDirection, index, delta);
            _selectedFrameIndex = index + delta;
            RefreshAll();
        }

        private void DoRemoveFrames()
        {
            Editor.RemoveFrames(Project.CurrentDirection, _selectedIndices);
            RefreshAll();
        }

        private void DoLoadPalette()
        {
            var files = _dialogs.OpenFiles("Load Palette", PaletteFilter, false);
            if (files.Length > 0)
                Service.LoadPalette(files[0]);
            RefreshAll();
        }

        private void DoOptions()
        {
            if (_dialogs.ShowOptions(Project.Settings, Project.Directions[0].Count))
                RefreshAll();
        }

        private void DoLoadBackground()
        {
            var files = _dialogs.OpenFiles("Load Background", "Background (*.BMP)|*.BMP", false);
            if (files.Length > 0)
                Service.LoadBackground(files[0]);
            RefreshAll();
        }

        private void DoPickSampleColor()
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
            RelayCommand.RaiseCanExecuteChanged();
        }

        public void CanvasMouseUp() => _dragging = false;
    }
}
