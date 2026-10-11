namespace FrameAnimator.App.Views
{
    using System;
    using System.Globalization;
    using System.Windows;
	using Infrastructure;
	using Localization;

    public partial class OffsetDialog : Window
    {
        private readonly OffsetDialogRequest _request;

        public OffsetDialog(OffsetDialogRequest request)
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);
            _request = request;
            Title = Localizer.T(request.Title);
            XBox.Text = request.X.ToString(CultureInfo.InvariantCulture);
            YBox.Text = request.Y.ToString(CultureInfo.InvariantCulture);
            InheritBox.Visibility = request.ShowInherit ? Visibility.Visible : Visibility.Collapsed;
            InheritBox.IsEnabled = request.InheritEnabled;
            InheritBox.IsChecked = request.InheritChecked;
            WizardButton.Visibility = request.ShowWizard ? Visibility.Visible : Visibility.Collapsed;
            WizardButton.IsEnabled = request.WizardEnabled && request.Wizard is not null;
            Loaded += (_, _) =>
            {
                XBox.Focus();
                XBox.SelectAll();
            };
        }

        public OffsetDialogResult Result { get; private set; }

        private static Boolean TryParse(String text, out Int32 value)
            => Int32.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        private void OnWizard(Object sender, RoutedEventArgs e)
        {
            if (!TryParse(XBox.Text, out var x))
            {
                MessageDialog.Show(this, Localizer.F("'{0}' is not a valid integer value!", XBox.Text), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            x = (Int16)x;
            XBox.Text = x.ToString(CultureInfo.InvariantCulture);
            YBox.Text = _request.Wizard!(x).ToString(CultureInfo.InvariantCulture);
        }

        private void OnOk(Object sender, RoutedEventArgs e)
        {
            if (!TryParse(XBox.Text, out var x) || !TryParse(YBox.Text, out var y))
            {
                MessageDialog.Show(this, Localizer.T("You enter is not a valid integer value!"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Result = new OffsetDialogResult(x, y, InheritBox.IsChecked == true);
            DialogResult = true;
        }
    }
}