namespace FrameAnimator.App.Views
{
    using System;
    using System.Globalization;
    using System.Windows;
    using FrameAnimator.Core.Model;
	using Localization;

	public partial class OptionsDialog : Window
    {
        private readonly ProjectSettings _settings;

        public OptionsDialog(ProjectSettings settings, Int32 frameCount)
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);
            _settings = settings;

            AutoRadio.IsChecked = settings.TransparentMode == TransparentMode.Auto;
            PaletteRadio.IsChecked = settings.TransparentMode == TransparentMode.Palette;
            SampleRadio.IsChecked = settings.TransparentMode == TransparentMode.Sample;
            AutoCuttingBox.IsChecked = settings.AutoCutting;
            LockBox.IsChecked = settings.LockOffsets;
            LegacyBox.IsChecked = settings.LegacyColorMatching;
            GhostBox.Text = settings.GhostIncrement.ToString(CultureInfo.InvariantCulture);
            FpsBox.Text = settings.Fps.ToString(CultureInfo.InvariantCulture);

            DitherBox.ItemsSource = new[] { Localizer.T("Default"), Localizer.T("Advanced"), "Fallout" };
            DitherBox.SelectedIndex = (Int32)settings.DitherMethod;

            // "0..n-1" (n = number of frames of direction 0); a single empty item if n <= 0
            var actions = new System.Collections.Generic.List<String>();
            if (frameCount <= 0)
                actions.Add("");
            else
                for (var i = 0; i < frameCount; i++)
                    actions.Add(i.ToString(CultureInfo.InvariantCulture));
            ActionBox.ItemsSource = actions;
            ActionBox.SelectedIndex = frameCount <= 0 ? 0 : Math.Min(settings.ActionFrame, frameCount - 1);
            if (frameCount > 0 && settings.ActionFrame >= frameCount)
                ActionBox.Text = settings.ActionFrame.ToString(CultureInfo.InvariantCulture);
        }

        private void OnOk(Object sender, RoutedEventArgs e)
        {
            if (!Int16.TryParse(GhostBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ghost)
                || !UInt16.TryParse(FpsBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fps))
            {
                MessageDialog.Show(this, Localizer.T("You enter is not a valid integer value!"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UInt16 action = 0;
            if (ActionBox.SelectedIndex >= 0)
                action = (UInt16)ActionBox.SelectedIndex;
            else if (UInt16.TryParse(ActionBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var typed))
                action = typed;
            if (String.IsNullOrEmpty(ActionBox.Text))
                action = _settings.ActionFrame;

            _settings.TransparentMode = PaletteRadio.IsChecked == true ? TransparentMode.Palette
                : SampleRadio.IsChecked == true ? TransparentMode.Sample
                : TransparentMode.Auto;
            _settings.AutoCutting = AutoCuttingBox.IsChecked == true;
            _settings.LockOffsets = LockBox.IsChecked == true;
            _settings.LegacyColorMatching = LegacyBox.IsChecked == true;
            _settings.GhostIncrement = ghost;
            _settings.Fps = fps;
            _settings.ActionFrame = action;
            _settings.DitherMethod = (DitherMethod)Math.Max(0, DitherBox.SelectedIndex);
            DialogResult = true;
        }
    }
}