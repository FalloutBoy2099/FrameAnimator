namespace FrameAnimator.App.ViewModels
{
    using System;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using CommunityToolkit.Mvvm.Messaging;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;
    using FrameAnimator.Core.Model;

    /// <summary>
    /// Animated preview of the current direction: its frames are shown one after the other, in a loop, at the frame rate of the project
    /// (the "FPS" option; an FRM without a rate plays at <see cref="DefaultFps"/>, like the game does). The preview follows the project: another
    /// direction, added or removed frames, another rate are taken into account as they happen.
    /// </summary>
    public sealed partial class AnimationPreviewViewModel : ObservableObject, IRecipient<LanguageChangedMessage>
    {
        /// <summary>Frame rate used when the project has none.</summary>
        public const Int32 DefaultFps = 10;

        /// <summary>The preview is never faster than this (the timer could not follow, and nobody would see it).</summary>
        public const Int32 MaxFps = 60;

        private readonly ProjectSession _session;
        private readonly ITicker _ticker;
        private readonly ILocalizer _localizer;
        private Boolean _open;

        public AnimationPreviewViewModel(ProjectSession session, ITicker ticker, ILocalizer localizer)
        {
            _session = session;
            _ticker = ticker;
            _localizer = localizer;
            ticker.Tick += (_, _) => Advance();
            session.Refreshed += (_, _) => OnProjectChanged();
            session.StateChanged += (_, _) => UpdateTimer();
            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        /// <summary>Raised to redraw the preview.</summary>
        public event EventHandler? RenderRequested;

        public Project Project => _session.Project;

        /// <summary>The project is modified by another thread (script): the control must not read it.</summary>
        public Boolean IsBusy => _session.IsBusy;

        /// <summary>The frame being shown, in the current direction.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Caption))]
        public partial Int32 FrameIndex { get; private set; }

        /// <summary>The animation runs (otherwise the frame stays where it is).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayPauseGlyph))]
        [NotifyPropertyChangedFor(nameof(PlayPauseToolTip))]
        public partial Boolean IsPlaying { get; private set; } = true;

        [ObservableProperty]
        public partial Int32 Zoom { get; set; } = 2;

        partial void OnZoomChanged(Int32 value)
        {
            if (value is < 1 or > 8)
                Zoom = Math.Clamp(value, 1, 8);
            else
                Render();
        }

        /// <summary>Frames per second actually used.</summary>
        public Int32 Fps
        {
            get
            {
                Int32 fps = Project.Settings.Fps == 0 ? DefaultFps : Project.Settings.Fps;
                return Math.Clamp(fps, 1, MaxFps);
            }
        }

        public Int32 FrameCount => _session.CurrentFrameCount;

        /// <summary>The preview has something to animate (at least two frames).</summary>
        public Boolean CanAnimate => FrameCount > 1;

        public String Caption => FrameCount == 0
            ? _localizer.T("No frames")
            : _localizer.F("Frame {0}/{1} - {2} fps", FrameIndex + 1, FrameCount, Fps);

        public String PlayPauseGlyph => IsPlaying ? "" : "";

        public String PlayPauseToolTip => _localizer.T(IsPlaying ? "Pause" : "Play");

        // ------------------------------------------------------------------ window life

        /// <summary>The preview window opened: the animation starts from the beginning.</summary>
        public void Open()
        {
            _open = true;
            FrameIndex = 0;
            IsPlaying = true;
            OnProjectChanged();
        }

        /// <summary>The preview window closed: the timer stops.</summary>
        public void Close()
        {
            _open = false;
            UpdateTimer();
        }

        // ------------------------------------------------------------------ playback

        private void Advance()
        {
            if (_session.IsBusy || FrameCount == 0)
                return;
            FrameIndex = (FrameIndex + 1) % FrameCount;
            Render();
        }

        private void OnProjectChanged()
        {
            Int32 last = Math.Max(FrameCount - 1, 0);
            if (FrameIndex > last)
                FrameIndex = last;
            OnPropertyChanged(nameof(Caption));
            OnPropertyChanged(nameof(CanAnimate));
            StepForwardCommand.NotifyCanExecuteChanged();
            StepBackCommand.NotifyCanExecuteChanged();
            PlayPauseCommand.NotifyCanExecuteChanged();
            UpdateTimer();
            Render();
        }

        /// <summary>The timer runs only while the window is open, the animation is playing, there is something to animate and no script is working.</summary>
        private void UpdateTimer()
        {
            if (_open && IsPlaying && CanAnimate && !_session.IsBusy)
            {
                TimeSpan interval = TimeSpan.FromMilliseconds(1000.0 / Fps);
                if (_ticker.Interval != interval)
                    _ticker.Interval = interval;
                if (!_ticker.IsRunning)
                    _ticker.Start();
            }
            else if (_ticker.IsRunning)
            {
                _ticker.Stop();
            }
        }

        partial void OnIsPlayingChanged(Boolean value) => UpdateTimer();

        private void Render() => RenderRequested?.Invoke(this, EventArgs.Empty);

        public void Receive(LanguageChangedMessage message)
        {
            OnPropertyChanged(nameof(Caption));
            OnPropertyChanged(nameof(PlayPauseToolTip));
        }

        [RelayCommand(CanExecute = nameof(CanAnimate))]
        private void PlayPause() => IsPlaying = !IsPlaying;

        /// <summary>Pauses and shows the next frame.</summary>
        [RelayCommand(CanExecute = nameof(CanAnimate))]
        private void StepForward() => Step(1);

        /// <summary>Pauses and shows the previous frame.</summary>
        [RelayCommand(CanExecute = nameof(CanAnimate))]
        private void StepBack() => Step(-1);

        private void Step(Int32 delta)
        {
            IsPlaying = false;
            FrameIndex = (FrameIndex + delta + FrameCount) % FrameCount;
            Render();
        }
    }
}
