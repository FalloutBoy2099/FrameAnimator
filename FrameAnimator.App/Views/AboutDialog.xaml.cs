namespace FrameAnimator.App.Views
{
    using System.Reflection;
    using System.Windows;

    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);

            // assembly version: 2026.month.day.build
            VersionText.Text = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "";
        }
    }
}