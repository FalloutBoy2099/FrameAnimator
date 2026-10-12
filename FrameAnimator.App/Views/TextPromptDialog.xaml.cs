namespace FrameAnimator.App.Views
{
    using System.Windows;

    /// <summary>Small dialog that asks for a line of text (for example the name of a new folder).</summary>
    public partial class TextPromptDialog : Window
    {
        public TextPromptDialog()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                Input.Focus();
                Input.SelectAll();
            };
        }
    }
}