namespace FrameAnimator.App.Views
{
    using System;
    using System.Windows;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;

    /// <summary>
    /// Shows messages and questions over the window the user is working in (the active one, so that a message raised by a
    /// dialog appears over that dialog), falling back to <see cref="Owner"/>.
    /// </summary>
    public sealed class MessageService : IMessageService
    {
        private const String Caption = "Frame Animator";

        /// <summary>Owner used when no window is active.</summary>
        public Window? Owner { get; set; }

        private Window? Target => DialogHost.ActiveWindow ?? Owner;

        public void ShowWarning(String message)
            => MessageDialog.Show(Target, Localizer.T(message), Caption, MessageBoxButton.OK, MessageBoxImage.Warning);

        public Boolean AskYesNo(String message)
            => MessageDialog.Show(Target, Localizer.T(message), Caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}