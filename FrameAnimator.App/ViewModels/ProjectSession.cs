namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.Windows.Threading;
    using CommunityToolkit.Mvvm.ComponentModel;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;
    using FrameAnimator.Core.Services;

    /// <summary>
    /// The project being edited and what works on it, shared by the view models of the main window: the project, its service and
    /// editor, the user interaction, and the "busy" state (a script or a batch is running). It tells the others when the content
    /// changed (<see cref="Refreshed"/>) or when an enabling rule may have changed (<see cref="StateChanged"/>).
    /// </summary>
    public sealed partial class ProjectSession : ObservableObject
    {
        private readonly Dispatcher _dispatcher;
        private Boolean _refreshPending;

        public ProjectSession(Dispatcher dispatcher, IDialogService dialogs, ILocalizer localizer, String appDirectory)
        {
            _dispatcher = dispatcher;
            Interaction = new WpfInteraction(dispatcher, dialogs, localizer);
            Interaction.ProgressChanged += OnProgress;
            Project = new Project();
            Service = new ProjectService(Project, Interaction, appDirectory) { ConfirmPaletteOnOpen = false };
            Editor = new ProjectEditor(Project);
            Service.Changed += (_, _) => ScheduleRefresh();
            Service.NewProject();
        }

        public WpfInteraction Interaction { get; }

        public Project Project { get; }

        public ProjectService Service { get; }

        public ProjectEditor Editor { get; }

        /// <summary>Directory of the application: where a batch looks for its default palette.</summary>
        public String AppDirectory => Service.AppDirectory;

        /// <summary>The project content changed (new project, frames added, offsets…): lists, texts and drawing are rebuilt.</summary>
        public event EventHandler? Refreshed;

        /// <summary>Something that decides whether a command is available changed (busy state).</summary>
        public event EventHandler? StateChanged;

        /// <summary>The project was edited without being rebuilt (a frame is being dragged, a color was picked): the undo command and the title may change.</summary>
        public event EventHandler? Edited;

        public Direction CurrentDir => Project.Directions[Project.CurrentDirection];

        public Int32 CurrentFrameCount => CurrentDir.Count;

        public Boolean HasFrames => CurrentFrameCount > 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsIdle))]
        [NotifyPropertyChangedFor(nameof(CanStartJob))]
        public partial Boolean IsBusy { get; private set; }

        [ObservableProperty]
        public partial Int32 ProgressValue { get; private set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ProgressVisible))]
        public partial Int32 ProgressMaximum { get; private set; }

        /// <summary>A batch is running on a project of its own (the one on screen is not touched).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanStartJob))]
        public partial Boolean IsBatchRunning { get; private set; }

        /// <summary>No script or batch works on the project (it can be edited).</summary>
        public Boolean IsIdle => !IsBusy;

        /// <summary>No script or batch is running at all: a new one may start, the project may be replaced.</summary>
        public Boolean CanStartJob => !IsBusy && !IsBatchRunning;

        public Boolean ProgressVisible => ProgressMaximum > 0;

        partial void OnIsBusyChanged(Boolean value) => NotifyStateChanged();

        partial void OnIsBatchRunningChanged(Boolean value) => NotifyStateChanged();

        /// <summary>Announces that a command may have become available or unavailable.</summary>
        public void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        /// <summary>Announces an edit that did not go through <see cref="Refresh"/>.</summary>
        public void NotifyEdited() => Edited?.Invoke(this, EventArgs.Empty);

        /// <summary>Rebuilds everything that shows the project (equivalent of <c>0x4025bc</c> of the original).</summary>
        public void Refresh() => Refreshed?.Invoke(this, EventArgs.Empty);

        /// <summary>The service reports changes from any thread: the refresh is done later on the UI thread, once for a burst of changes.</summary>
        private void ScheduleRefresh()
        {
            if (_refreshPending || IsBusy)
                return;
            _refreshPending = true;
            _dispatcher.BeginInvoke(() =>
            {
                _refreshPending = false;
                Refresh();
            }, DispatcherPriority.Background);
        }

        private void OnProgress(Int32 done, Int32 total)
        {
            ProgressValue = done;
            ProgressMaximum = total;
        }

        /// <summary>Marks the session busy until the returned scope is disposed; the cancel request is cleared and the project redrawn at the end.</summary>
        public IDisposable BeginBusy()
        {
            IsBusy = true;
            Interaction.CancelRequested = false;
            return new BusyScope(this);
        }

        /// <summary>
        /// Marks a batch as running until the returned scope is disposed. The batch works on a project of its own, so the project on screen
        /// stays editable; only the start of another script or batch is refused meanwhile.
        /// </summary>
        public IDisposable BeginBatch()
        {
            IsBatchRunning = true;
            Interaction.CancelRequested = false;
            return new BatchScope(this);
        }

        private sealed class BusyScope(ProjectSession session) : IDisposable
        {
            public void Dispose()
            {
                session.IsBusy = false;
                session.Refresh();
            }
        }

        private sealed class BatchScope(ProjectSession session) : IDisposable
        {
            public void Dispose() => session.IsBatchRunning = false;
        }
    }
}
