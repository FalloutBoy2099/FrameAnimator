namespace FrameAnimator.App.Views
{
    using System.Windows;

    public partial class OffsetDialog : Window
    {
        public OffsetDialog()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                XBox.Focus();
                XBox.SelectAll();
            };
        }
    }
}