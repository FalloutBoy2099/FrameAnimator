namespace FrameAnimator.App.Views
{
    using System.Windows;

    public partial class OffsetDialog : Window
    {
        public OffsetDialog()
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);
            Loaded += (_, _) =>
            {
                XBox.Focus();
                XBox.SelectAll();
            };
        }
    }
}