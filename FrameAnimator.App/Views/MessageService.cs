namespace FrameAnimator.App.Views
{
    using System;
    using System.Windows;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;
    using FrameAnimator.App.Services;
    using FrameAnimator.App.ViewModels;

    /// <summary>
    /// Shows messages and questions over the window the user is working in (the active one, so that a message raised by a
    /// dialog appears over that dialog), falling back to the main window. Uses the application's themed message box or, when the
    /// user prefers them, the Windows ones (which do not follow the dark theme).
    /// </summary>
    public sealed class MessageService(DialogHost host, IDialogPreferences preferences, ILocalizer localizer) : IMessageService
    {
        private const String Caption = "Frame Animator";

        public void ShowWarning(String message)
            => Show(host.Target, localizer.T(message), Caption, MessageBoxButton.OK, MessageBoxImage.Warning);

        public Boolean AskYesNo(String message)
            => Show(host.Target, localizer.T(message), Caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        public MessageBoxResult AskYesNoCancel(String message)
        {
            MessageBoxResult answer = Show(host.Target, localizer.T(message), Caption, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            return answer is MessageBoxResult.Yes or MessageBoxResult.No ? answer : MessageBoxResult.Cancel; // closing the box is a cancel
        }

        /// <summary>Shows an error as is (an exception message is not translated).</summary>
        public void ShowError(String message)
            => Show(host.Target, message, Caption, MessageBoxButton.OK, MessageBoxImage.Error);

        /// <summary>Shows the message and returns the chosen button.</summary>
        public MessageBoxResult Show(Window? owner, String message, String caption, MessageBoxButton buttons, MessageBoxImage image)
        {
            if (preferences.UseSystemDialogs)
            {
                return owner is not null
                    ? MessageBox.Show(owner, message, caption, buttons, image)
                    : MessageBox.Show(message, caption, buttons, image);
            }

            MessageDialogViewModel viewModel = new(localizer, message, caption, buttons, image);
            host.ShowModal(new MessageDialog(), viewModel, owner);
            return viewModel.Result;
        }
    }
}
