namespace FrameAnimator.App.Views
{
    using System.Windows;

    public partial class AutomateDialog : Window
    {
        public AutomateDialog()
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);
        }
    }
}