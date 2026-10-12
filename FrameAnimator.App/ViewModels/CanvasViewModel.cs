namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.Windows.Media;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using CommunityToolkit.Mvvm.Messaging;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;
    using FrameAnimator.Core.Geometry;
    using FrameAnimator.Core.Imaging;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;
    using FrameAnimator.Core.Services;

    /// <summary>Active tool of the drawing area (byte <c>+0x88e</c> of the original).</summary>
    public enum EditTool
    {
        None = 0,
        Move = 1,
        Pick = 2,
    }

    /// <summary>
    /// The drawing area: zoom, what is shown (background, hexes, ghost frame), the active tool (move a frame, pick a color), the
    /// sample color, and the mouse actions. The control draws from this view model and asks it to act on mouse events.
    /// </summary>
    public sealed partial class CanvasViewModel : ObservableObject, IRecipient<LanguageChangedMessage>, IRecipient<ThemeChangedMessage>
    {
        private readonly ProjectSession _session;
        private readonly FrameListViewModel _frameList;
        private readonly IDialogService _dialogs;
        private readonly ILocalizer _localizer;
        private Int32 _zoom = 2;

        // dragging (Move tool)
        private Boolean _dragging;
        private Int32 _anchorX, _anchorY, _lastX, _lastY;

        public CanvasViewModel(ProjectSession session, FrameListViewModel frameList, IDialogService dialogs, ILocalizer localizer)
        {
            _session = session;
            _frameList = frameList;
            _dialogs = dialogs;
            _localizer = localizer;

            session.Refreshed += (_, _) => NotifySampleChanged();
            session.StateChanged += (_, _) => UpdateCommands();
            frameList.SelectionChanged += (_, _) =>
            {
                UpdateCommands();
                Render();
            };
            frameList.DirectionChanged += (_, _) => _dragging = false;
            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        /// <summary>Raised to redraw the drawing area.</summary>
        public event EventHandler? RenderRequested;

        // what the control draws
        public Project Project => _session.Project;

        public Direction CurrentDir => _session.CurrentDir;

        public Int32 SelectedFrameIndex => _frameList.SelectedFrameIndex;

        /// <summary>The project is modified by another thread (script, batch): the control must not read it.</summary>
        public Boolean IsBusy => _session.IsBusy;

        public Boolean HasFrames => _session.HasFrames;

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

        partial void OnShowBackgroundChanged(Boolean value) => Render();

        partial void OnShowHexesChanged(Boolean value) => Render();

        partial void OnShowGhostChanged(Boolean value) => Render();

        partial void OnToolChanged(EditTool value) => _dragging = false;

        public Boolean IsMoveTool => Tool == EditTool.Move;

        public Boolean IsPickTool => Tool == EditTool.Pick;

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

        // ------------------------------------------------------------------ sample color

        public Brush SampleBrush => new SolidColorBrush(ImageConversion.ToColor(Project.Settings.SampleColor));

        public String SampleText => _localizer.T("Sample");

        public String SampleToolTip => _localizer.F("Sample color #{0} (click to change)", (Project.Settings.SampleColor & 0xFFFFFF).ToString("X6"));

        private void NotifySampleChanged()
        {
            OnPropertyChanged(nameof(SampleBrush));
            OnPropertyChanged(nameof(SampleText));
            OnPropertyChanged(nameof(SampleToolTip));
        }

        private void SetSampleColor(Int32 rgb)
        {
            Project.Settings.SampleColor = rgb & 0xFFFFFF;
            NotifySampleChanged();
            Render();
            _session.NotifyEdited();
        }

        // ------------------------------------------------------------------ messages

        /// <summary>The language changed: the texts computed here are refreshed.</summary>
        public void Receive(LanguageChangedMessage message) => NotifySampleChanged();

        /// <summary>The theme changed: the drawing area uses theme colors.</summary>
        public void Receive(ThemeChangedMessage message) => Render();

        // ------------------------------------------------------------------ commands

        private Boolean CanUseFrames() => _session.IsIdle && _session.HasFrames;

        private Boolean CanZoomIn() => Zoom < 8;

        private Boolean CanZoomOut() => Zoom > 1;

        private void UpdateCommands()
        {
            ToggleMoveCommand.NotifyCanExecuteChanged();
            TogglePickCommand.NotifyCanExecuteChanged();
            PickSampleColorCommand.NotifyCanExecuteChanged();
        }

        public void Render() => RenderRequested?.Invoke(this, EventArgs.Empty);

        [RelayCommand(CanExecute = nameof(CanUseFrames))]
        private void ToggleMove() => Tool = Tool == EditTool.Move ? EditTool.None : EditTool.Move;

        [RelayCommand(CanExecute = nameof(CanUseFrames))]
        private void TogglePick() => Tool = Tool == EditTool.Pick ? EditTool.None : EditTool.Pick;

        [RelayCommand(CanExecute = nameof(CanUseFrames))]
        private void PickSampleColor()
        {
            Int32? color = _dialogs.PickColor(Project.Settings.SampleColor);
            if (color is { } c)
                SetSampleColor(c);
        }

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

        // ------------------------------------------------------------------ keyboard

        /// <summary>
        /// Arrow keys with the Move tool: moves like a one-step drag does (the direction for the first frame, the frame otherwise, unless
        /// offsets are locked), each key press being its own undo step. <paramref name="propagate"/> moves the following frames too
        /// (what Ctrl does while dragging). Returns whether the key was used.
        /// </summary>
        public Boolean Nudge(Int32 dx, Int32 dy, Boolean propagate)
        {
            if (Tool != EditTool.Move || IsBusy || !HasFrames || SelectedFrameIndex < 0)
                return false;
            _session.Editor.BeginDrag();
            _session.Editor.DragMove(Project.CurrentDirection, SelectedFrameIndex, dx, dy, propagate);
            Render();
            _session.NotifyEdited();
            return true;
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
                    _session.Editor.BeginDrag();
                    break;

                case EditTool.Pick:
                {
                    (Int32 fx, Int32 fy) = FrameMath.DisplayPosition(CurrentDir, SelectedFrameIndex, 0, 0);
                    Bitmap32 bmp = CurrentDir.Frames[SelectedFrameIndex].Bitmap;
                    Int32 px = x - fx;
                    Int32 py = y - fy;
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

            Int32 ny = shift ? ProjectEditor.ConstrainY(Project.CurrentDirection, _anchorX, _anchorY, x) : y;
            _session.Editor.DragMove(Project.CurrentDirection, SelectedFrameIndex, x - _lastX, ny - _lastY, ctrl);
            _lastX = x;
            _lastY = ny;
            Render();
            _session.NotifyEdited();
        }

        public void CanvasMouseUp() => _dragging = false;
    }
}
