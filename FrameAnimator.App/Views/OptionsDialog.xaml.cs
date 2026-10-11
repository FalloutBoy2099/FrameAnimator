namespace FrameAnimator.App.Views
{
    using System.Windows;

    public partial class OptionsDialog : Window
    {
        public OptionsDialog()
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);
        }
    }
}