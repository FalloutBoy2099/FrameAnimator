namespace FrameAnimator.App.Views
{
    using System;
    using System.Windows;
    using ViewModels;

    /// <summary>Small dialog that asks for a line of text (for example the name of a new folder).</summary>
    public partial class TextPromptDialog : Window
    {
        private TextPromptDialog()
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);
            Loaded += (_, _) =>
            {
                Input.Focus();
                Input.SelectAll();
            };
        }

        /// <summary>Returns the entered text, or <c>null</c> if the dialog is canceled or the text is empty.</summary>
        public static String? Show(Window? owner, String title, String prompt, String initial)
        {
            TextPromptDialogViewModel viewModel = new(title, prompt, initial);
            return DialogHost.ShowModal(new TextPromptDialog(), viewModel, owner) ? viewModel.Result : null;
        }
    }
}