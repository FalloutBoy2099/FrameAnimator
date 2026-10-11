namespace FrameAnimator.App.Views
{
    using System;
    using System.Windows;
    using Localization;

    /// <summary>Small dialog that asks for a line of text (for example the name of a new folder).</summary>
    public partial class TextPromptDialog : Window
    {
        private TextPromptDialog(String title, String prompt, String initial)
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);
            Title = Localizer.T(title);
            Prompt.Text = Localizer.T(prompt);
            Input.Text = initial;
            Loaded += (_, _) =>
            {
                Input.Focus();
                Input.SelectAll();
            };
        }

        private String? Result { get; set; }

        /// <summary>Returns the entered text, or <c>null</c> if the dialog is canceled or the text is empty.</summary>
        public static String? Show(Window? owner, String title, String prompt, String initial)
        {
            TextPromptDialog dialog = new(title, prompt, initial);
            if (owner is { IsLoaded: true })
                dialog.Owner = owner;
            dialog.ShowDialog();
            return dialog.Result;
        }

        private void OnOk(Object sender, RoutedEventArgs e)
        {
            String text = Input.Text.Trim();
            if (text.Length == 0)
                return;
            Result = text;
            DialogResult = true;
        }
    }
}