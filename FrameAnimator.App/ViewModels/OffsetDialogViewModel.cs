namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.Globalization;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;

    /// <summary>View model of the "Offset" dialog (Frames Offset / Frame Offset / First Frame Offset).</summary>
    public sealed partial class OffsetDialogViewModel : DialogViewModelBase
    {
        private readonly OffsetDialogRequest _request;
        private readonly IMessageService _messages;
        private readonly ILocalizer _localizer;

        public OffsetDialogViewModel(OffsetDialogRequest request, IMessageService messages, ILocalizer localizer)
        {
            _request = request;
            _messages = messages;
            _localizer = localizer;
            Title = localizer.T(request.Title);
            XText = request.X.ToString(CultureInfo.InvariantCulture);
            YText = request.Y.ToString(CultureInfo.InvariantCulture);
            Inherit = request.InheritChecked;
            ShowInherit = request.ShowInherit;
            InheritEnabled = request.InheritEnabled;
            ShowWizard = request.ShowWizard;
            WizardEnabled = request.WizardEnabled && request.Wizard is not null;
        }

        public String Title { get; }

        /// <summary>The "Inherit shift down" checkbox is shown.</summary>
        public Boolean ShowInherit { get; }

        public Boolean InheritEnabled { get; }

        /// <summary>The wizard button is shown.</summary>
        public Boolean ShowWizard { get; }

        public Boolean WizardEnabled { get; }

        [ObservableProperty]
        public partial String XText { get; set; }

        [ObservableProperty]
        public partial String YText { get; set; }

        [ObservableProperty]
        public partial Boolean Inherit { get; set; }

        /// <summary>The answer, set when the dialog was accepted.</summary>
        public OffsetDialogResult? Result { get; private set; }

        private static Boolean TryParse(String text, out Int32 value)
            => Int32.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        /// <summary>Computes Y from X (truncated to 16 bits).</summary>
        [RelayCommand(CanExecute = nameof(WizardEnabled))]
        private void Wizard()
        {
            if (!TryParse(XText, out Int32 x))
            {
                _messages.ShowWarning(_localizer.F("'{0}' is not a valid integer value!", XText));
                return;
            }
            x = (Int16)x;
            XText = x.ToString(CultureInfo.InvariantCulture);
            YText = _request.Wizard!(x).ToString(CultureInfo.InvariantCulture);
        }

        [RelayCommand]
        private void Ok()
        {
            if (!TryParse(XText, out Int32 x) || !TryParse(YText, out Int32 y))
            {
                _messages.ShowWarning("You enter is not a valid integer value!");
                return;
            }
            Result = new OffsetDialogResult(x, y, Inherit);
            RequestClose(true);
        }
    }
}
