namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.ComponentModel;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Messaging;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;
    using FrameAnimator.Core.Formats;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Services;

    /// <summary>The texts of the status bar (panels of the original window) and the progress of a running script or batch.</summary>
    public sealed class StatusBarViewModel : ObservableObject, IRecipient<LanguageChangedMessage>
    {
        private readonly ProjectSession _session;
        private readonly FrameListViewModel _frameList;
        private readonly CanvasViewModel _canvas;
        private readonly ILocalizer _localizer;

        public StatusBarViewModel(ProjectSession session, FrameListViewModel frameList, CanvasViewModel canvas, ILocalizer localizer)
        {
            _session = session;
            _frameList = frameList;
            _canvas = canvas;
            _localizer = localizer;

            session.Refreshed += (_, _) => NotifyProjectTexts();
            session.PropertyChanged += OnSessionPropertyChanged;
            frameList.SelectionChanged += (_, _) => OnPropertyChanged(nameof(StatusFrame));
            canvas.PropertyChanged += OnCanvasPropertyChanged;
            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        private ProjectSettings Settings => _session.Project.Settings;

        public String StatusFrame
            => _session.HasFrames ? _localizer.F("Frame {0}/{1}", _frameList.SelectedFrameIndex + 1, _session.CurrentFrameCount) : "";

        public String StatusTransparent => _localizer.T(Settings.TransparentMode switch
        {
            TransparentMode.Auto => "Transp: Auto",
            TransparentMode.Palette => "Transp: Palette",
            _ => "Transp: Sample",
        });

        public String StatusPalette
            => _localizer.F("Pal: {0}", String.IsNullOrEmpty(_session.Project.PaletteFile) ? "Default.act" : PathHelpers.ExtractFileName(_session.Project.PaletteFile));

        public String StatusAutoCutting => Settings.AutoCutting ? _localizer.T("Autocutting") : "";

        public String StatusLock => Settings.LockOffsets ? _localizer.T("Lock Offsets") : "";

        public String ZoomText => _canvas.ZoomText;

        /// <summary>What the active tool does (empty without a tool, or while a script or batch shows its progress instead).</summary>
        public String StatusHint
        {
            get
            {
                if (_session.ProgressVisible)
                    return "";
                return _canvas.Tool switch
                {
                    EditTool.Move => _localizer.T("Drag the frame, or use the arrow keys (Shift: 10 pixels, Ctrl: following frames too)"),
                    EditTool.Pick => _localizer.T("Click a pixel to use its color as the sample"),
                    _ => "",
                };
            }
        }

        public Int32 ProgressValue => _session.ProgressValue;

        public Int32 ProgressMaximum => _session.ProgressMaximum;

        public Boolean ProgressVisible => _session.ProgressVisible;

        public void Receive(LanguageChangedMessage message)
        {
            NotifyProjectTexts();
            OnPropertyChanged(nameof(StatusHint));
        }

        private void NotifyProjectTexts()
        {
            OnPropertyChanged(nameof(StatusFrame));
            OnPropertyChanged(nameof(StatusTransparent));
            OnPropertyChanged(nameof(StatusPalette));
            OnPropertyChanged(nameof(StatusAutoCutting));
            OnPropertyChanged(nameof(StatusLock));
        }

        private void OnSessionPropertyChanged(Object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ProjectSession.ProgressValue) or nameof(ProjectSession.ProgressMaximum) or nameof(ProjectSession.ProgressVisible))
                OnPropertyChanged(e.PropertyName);
            if (e.PropertyName == nameof(ProjectSession.ProgressVisible))
                OnPropertyChanged(nameof(StatusHint));
        }

        private void OnCanvasPropertyChanged(Object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CanvasViewModel.ZoomText))
                OnPropertyChanged(nameof(ZoomText));
            if (e.PropertyName == nameof(CanvasViewModel.Tool))
                OnPropertyChanged(nameof(StatusHint));
        }
    }
}
