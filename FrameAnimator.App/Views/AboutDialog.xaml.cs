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

            // version de l'assemblage : 2026.mois.jour.build
            VersionText.Text = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "";
        }
    }
}