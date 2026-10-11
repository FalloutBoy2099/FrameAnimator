namespace FrameAnimator.App.Views
{
    using System;
    using System.Windows;
    using Infrastructure;
    using ViewModels;

    /// <summary>Message box using the application's theme (system message boxes do not follow the dark theme).</summary>
    public partial class MessageDialog : Window
    {
        private MessageDialog()
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);
        }

        /// <summary>Shows the message and returns the chosen button.</summary>
        public static MessageBoxResult Show(Window? owner, String message, String caption, MessageBoxButton buttons, MessageBoxImage image)
        {
            if (DialogPreferences.UseSystemDialogs)
            {
                return owner is not null
                    ? MessageBox.Show(owner, message, caption, buttons, image)
                    : MessageBox.Show(message, caption, buttons, image);
            }

            MessageDialogViewModel viewModel = new(message, caption, buttons, image);
            DialogHost.ShowModal(new MessageDialog(), viewModel, owner);
            return viewModel.Result;
        }
    }
}