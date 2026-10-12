namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;
    using FrameAnimator.Core.Model;

    /// <summary>View model of the "Options" dialog: edits the project settings and writes them back on OK.</summary>
    public sealed partial class OptionsDialogViewModel : DialogViewModelBase
    {
        private readonly ProjectSettings _settings;
        private readonly Int32 _frameCount;
        private readonly IMessageService _messages;

        public OptionsDialogViewModel(ProjectSettings settings, Int32 frameCount, IMessageService messages, ILocalizer localizer)
        {
            _settings = settings;
            _frameCount = frameCount;
            _messages = messages;

            TransparentMode = settings.TransparentMode;
            AutoCutting = settings.AutoCutting;
            LockOffsets = settings.LockOffsets;
            LegacyColorMatching = settings.LegacyColorMatching;
            GhostText = settings.GhostIncrement.ToString(CultureInfo.InvariantCulture);
            FpsText = settings.Fps.ToString(CultureInfo.InvariantCulture);

            DitherMethods = [localizer.T("Default"), localizer.T("Advanced"), "Fallout"];
            DitherIndex = (Int32)settings.DitherMethod;

            // "0..n-1" (n = number of frames of direction 0); a single empty item if n <= 0
            List<String> actions = [];
            if (frameCount <= 0)
                actions.Add("");
            else
                for (Int32 i = 0; i < frameCount; i++)
                    actions.Add(i.ToString(CultureInfo.InvariantCulture));
            ActionFrames = actions;
            ActionIndex = frameCount <= 0 ? 0 : Math.Min(settings.ActionFrame, frameCount - 1);
        }

        public IReadOnlyList<String> DitherMethods { get; }

        public IReadOnlyList<String> ActionFrames { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAutoTransparent))]
        [NotifyPropertyChangedFor(nameof(IsPaletteTransparent))]
        [NotifyPropertyChangedFor(nameof(IsSampleTransparent))]
        public partial TransparentMode TransparentMode { get; set; }

        /// <summary>Radio button "Auto": checking selects the mode; unchecking is only the consequence of checking another one.</summary>
        public Boolean IsAutoTransparent
        {
            get => TransparentMode == TransparentMode.Auto;
            set
            {
                if (value)
                    TransparentMode = TransparentMode.Auto;
            }
        }

        public Boolean IsPaletteTransparent
        {
            get => TransparentMode == TransparentMode.Palette;
            set
            {
                if (value)
                    TransparentMode = TransparentMode.Palette;
            }
        }

        public Boolean IsSampleTransparent
        {
            get => TransparentMode == TransparentMode.Sample;
            set
            {
                if (value)
                    TransparentMode = TransparentMode.Sample;
            }
        }

        [ObservableProperty]
        public partial Boolean AutoCutting { get; set; }

        [ObservableProperty]
        public partial Boolean LockOffsets { get; set; }

        [ObservableProperty]
        public partial Boolean LegacyColorMatching { get; set; }

        [ObservableProperty]
        public partial String GhostText { get; set; }

        [ObservableProperty]
        public partial String FpsText { get; set; }

        [ObservableProperty]
        public partial Int32 DitherIndex { get; set; }

        [ObservableProperty]
        public partial Int32 ActionIndex { get; set; }

        [RelayCommand]
        private void Ok()
        {
            if (!Int16.TryParse(GhostText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out Int16 ghost)
                || !UInt16.TryParse(FpsText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out UInt16 fps))
            {
                _messages.ShowWarning("You enter is not a valid integer value!");
                return;
            }

            // with no frame (single empty item) the current action frame is kept
            UInt16 action = ActionIndex < 0 || _frameCount <= 0 ? _settings.ActionFrame : (UInt16)ActionIndex;

            _settings.TransparentMode = TransparentMode == TransparentMode.Palette ? TransparentMode.Palette
                : TransparentMode == TransparentMode.Sample ? TransparentMode.Sample
                : TransparentMode.Auto;
            _settings.AutoCutting = AutoCutting;
            _settings.LockOffsets = LockOffsets;
            _settings.LegacyColorMatching = LegacyColorMatching;
            _settings.GhostIncrement = ghost;
            _settings.Fps = fps;
            _settings.ActionFrame = action;
            _settings.DitherMethod = (DitherMethod)Math.Max(0, DitherIndex);
            RequestClose(true);
        }
    }
}
