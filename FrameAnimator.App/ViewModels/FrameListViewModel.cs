namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;
    using FrameAnimator.Core.Formats;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Services;

    /// <summary>
    /// The direction and its frames: the direction list, the frame list with its (multiple) selection, and the commands that act on
    /// the frames (add, remove, move, offsets).
    /// </summary>
    public sealed partial class FrameListViewModel : ObservableObject
    {
        private const String ImageFilter = "All (*.BMP, *.GIF, *.PNG)|*.BMP;*.GIF;*.PNG|BMP (*.BMP)|*.BMP|CompuServe GIF (*.GIF)|*.GIF|PNG (*.PNG)|*.PNG";

        private readonly ProjectSession _session;
        private readonly IDialogService _dialogs;
        private Boolean _rebuilding;
        private Int32 _selectedFrameIndex = -1;
        private IReadOnlyList<Int32> _selectedIndices = [];

        public FrameListViewModel(ProjectSession session, IDialogService dialogs, ILocalizer localizer)
        {
            _session = session;
            _dialogs = dialogs;
            DirectionNames = Project.DirectionNames.Select(name => localizer.Observe(name)).ToArray();
            session.Refreshed += (_, _) => RebuildFrameList();
            session.StateChanged += (_, _) => UpdateCommands();
        }

        private Project Project => _session.Project;

        private ProjectEditor Editor => _session.Editor;

        /// <summary>Names of the six directions of the direction list.</summary>
        public IReadOnlyList<LocalizedText> DirectionNames { get; }

        /// <summary>Texts of the frames of the current direction.</summary>
        public ObservableCollection<String> Frames { get; } = [];

        /// <summary>Raised when the current frame changed (or the list was rebuilt).</summary>
        public event EventHandler? SelectionChanged;

        /// <summary>Raised when another direction became the current one.</summary>
        public event EventHandler? DirectionChanged;

        public Int32 CurrentDirection
        {
            get => Project.CurrentDirection;
            set
            {
                if (value < 0 || value >= Project.DirectionCount || value == Project.CurrentDirection)
                    return;
                Project.CurrentDirection = value;
                OnPropertyChanged();
                DirectionChanged?.Invoke(this, EventArgs.Empty);
                _selectedFrameIndex = -1;
                _session.Refresh();
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

        partial void OnShowFullNamesChanged(Boolean value) => RebuildFrameList();

        // ------------------------------------------------------------------ enabling rules

        private Boolean CanUseFrames() => _session.IsIdle && _session.HasFrames;

        private Boolean CanAddFrames() => _session.IsIdle;

        private Boolean CanMoveFrameDown() => CanUseFrames() && SelectedFrameIndex >= 0 && SelectedFrameIndex < _session.CurrentFrameCount - 1;

        private Boolean CanMoveFrameUp() => CanUseFrames() && SelectedFrameIndex > 0;

        private Boolean CanEditFrameOffset()
            => CanUseFrames() && SelectedFrameIndex >= 0 && (SelectedFrameIndex == 0 || !Project.Settings.LockOffsets);

        private Boolean CanRemoveFrames() => _session.IsIdle && _selectedIndices.Count > 0;

        private void UpdateCommands()
        {
            FramesOffsetCommand.NotifyCanExecuteChanged();
            AddFramesCommand.NotifyCanExecuteChanged();
            FrameDownCommand.NotifyCanExecuteChanged();
            FrameUpCommand.NotifyCanExecuteChanged();
            FrameOffsetCommand.NotifyCanExecuteChanged();
            RemoveFramesCommand.NotifyCanExecuteChanged();
        }

        // ------------------------------------------------------------------ list

        private String FrameText(Frame frame)
        {
            String name = ShowFullNames ? frame.FileName : PathHelpers.ExtractFileName(frame.FileName);
            return frame.HasSourceIndex ? name + "[" + frame.SourceIndex + "]" : name;
        }

        private void RebuildFrameList()
        {
            Int32 keep = _selectedFrameIndex;
            _rebuilding = true;
            try
            {
                Frames.Clear();
                foreach (Frame frame in _session.CurrentDir.Frames)
                    Frames.Add(FrameText(frame));
            }
            finally
            {
                _rebuilding = false;
            }

            keep = Frames.Count == 0 ? -1 : Math.Clamp(keep, 0, Frames.Count - 1);
            _selectedFrameIndex = keep;
            _selectedIndices = keep >= 0 ? [keep] : [];
            OnPropertyChanged(nameof(CurrentDirection));
            OnPropertyChanged(nameof(SelectedFrameIndex));
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            UpdateCommands();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        // ------------------------------------------------------------------ frame actions

        [RelayCommand(CanExecute = nameof(CanUseFrames))]
        private void FramesOffset()
        {
            Int32 dir = Project.CurrentDirection;
            (Int32 x, Int32 y) = Editor.GetFramesOffset(dir);
            OffsetDialogResult? result = _dialogs.ShowOffset(new OffsetDialogRequest { Title = "Frames Offset", X = x, Y = y });
            if (result is { } r)
                Editor.SetFramesOffset(dir, r.X, r.Y);
            _session.Refresh();
        }

        [RelayCommand(CanExecute = nameof(CanEditFrameOffset))]
        private void FrameOffset()
        {
            Int32 dir = Project.CurrentDirection;
            Int32 index = SelectedFrameIndex;
            (Int32 x, Int32 y) = Editor.GetFrameOffset(dir, index);

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

            OffsetDialogResult? result = _dialogs.ShowOffset(request);
            if (result is { } r)
                Editor.SetFrameOffset(dir, index, r.X, r.Y, index > 0 && r.Inherit);
            _session.Refresh();
        }

        [RelayCommand(CanExecute = nameof(CanAddFrames))]
        private void AddFrames()
        {
            String[] files = _dialogs.OpenFiles("Add Frames", ImageFilter, true);
            if (files.Length == 0)
                return;
            AddFrameFiles(files);
        }

        /// <summary>Adds image files at the end of the current direction (the dialog, or files dropped on the window).</summary>
        public void AddFrameFiles(IReadOnlyList<String> files)
        {
            Direction dir = _session.CurrentDir;
            for (Int32 i = 0; i < files.Count; i++)
            {
                if (_session.Service.LoadFrameFile(dir, files[i], -1) is null && i < files.Count - 1
                    && !_dialogs.AskYesNo("Continue loading frames?"))
                    break;
            }
            Project.Undo.Clear();
            _session.Refresh();
        }

        [RelayCommand(CanExecute = nameof(CanMoveFrameDown))]
        private void FrameDown() => MoveFrame(1);

        [RelayCommand(CanExecute = nameof(CanMoveFrameUp))]
        private void FrameUp() => MoveFrame(-1);

        private void MoveFrame(Int32 delta)
        {
            Int32 index = SelectedFrameIndex;
            Editor.MoveFrame(Project.CurrentDirection, index, delta);
            _selectedFrameIndex = index + delta;
            _session.Refresh();
        }

        [RelayCommand(CanExecute = nameof(CanRemoveFrames))]
        private void RemoveFrames()
        {
            Editor.RemoveFrames(Project.CurrentDirection, _selectedIndices);
            _session.Refresh();
        }
    }
}
