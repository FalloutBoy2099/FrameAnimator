namespace FrameAnimator.App.Views
{
    using System.Windows;

    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);
        }
    }
}