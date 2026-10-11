namespace FrameAnimator.App.Views
{
    using System;
    using System.Linq;
    using System.Windows;
	using System.Windows.Controls;
	using FrameAnimator.Core.Scripting;
	using Localization;

    public partial class AutomateDialog : Window
    {
        private readonly AutomationOptions _options = new();

        public AutomateDialog(String currentPalette)
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);
            // le masque doit être renseigné avant le choix du format : celui-ci en remplace l'extension
            MaskBox.Text = _options.DestinationMask;
            PaletteBox.Text = currentPalette;
            FormatBox.ItemsSource = new[] { "FPR-Project", "FRM", "FR0-FR5" };
            FramesBox.ItemsSource = new[] { "BMP", "PNG", "GIF" };
            FormatBox.SelectedIndex = 0;
            FramesBox.SelectedIndex = 0;
        }

        public AutomationOptions Options => _options;

        private void OnAddSources(Object sender, RoutedEventArgs e)
        {
            String[] files = FileDialogs.ShowOpen(this, "Select Sources",
                DialogService.TranslateFilter("All (*.FPR,*.FRM,*.FR0-*.FR5)|*.FPR;*.FRM;*.FR0;*.FR1;*.FR2;*.FR3;*.FR4;*.FR5|Project (*.FPR)|*.FPR|FRM (*.FRM,*.FR0-*.FR5)|*.FRM;*.FR0;*.FR1;*.FR2;*.FR3;*.FR4;*.FR5"),
                multiselect: true);
            foreach (String file in files)
                SourceList.Items.Add(file);
        }

        private void OnRemoveSources(Object sender, RoutedEventArgs e)
        {
            foreach (var item in SourceList.SelectedItems.Cast<Object>().ToList())
                SourceList.Items.Remove(item);
        }

        private void OnBrowsePalette(Object sender, RoutedEventArgs e)
        {
            String[] files = FileDialogs.ShowOpen(this, "Load Palette",
                DialogService.TranslateFilter("All (*.ACT, *.PAL)|*.ACT;*.PAL|Palette (*.ACT)|*.ACT|Fallout Palette (*.PAL)|*.PAL"), multiselect: false);
            if (files.Length > 0)
                PaletteBox.Text = files[0];
        }

        private void OnBrowseScript(Object sender, RoutedEventArgs e)
        {
            String[] files = FileDialogs.ShowOpen(this, "Open Script", DialogService.TranslateFilter("Action Script (*.FAS)|*.FAS"), multiselect: false);
            if (files.Length > 0)
                ScriptBox.Text = files[0];
        }

        private void OnBrowseMask(Object sender, RoutedEventArgs e)
        {
            String? file = FileDialogs.ShowSave(this, "Destination", Localizer.T("All") + " (*.*)|*.*", 1, MaskBox.Text, "", overwritePrompt: false);
            if (file is not null)
                MaskBox.Text = file;
        }

        private void OnFormatChanged(Object sender, SelectionChangedEventArgs e)
        {
            if (MaskBox is null)
                return;
            _options.Format = (AutomationFormat)Math.Max(0, FormatBox.SelectedIndex);
            _options.DestinationMask = MaskBox.Text;
            _options.ApplyFormatToMask();
            MaskBox.Text = _options.DestinationMask;
        }

        private void OnOk(Object sender, RoutedEventArgs e)
        {
            if (SourceList.Items.Count == 0)
            {
                MessageDialog.Show(this, Localizer.T("Select at least one source file."), Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _options.Sources.Clear();
            foreach (String s in SourceList.Items)
                _options.Sources.Add(s);
            _options.UsePalette = UsePaletteBox.IsChecked == true;
            _options.PalettePath = PaletteBox.Text;
            _options.UseScript = UseScriptBox.IsChecked == true;
            _options.ScriptPath = ScriptBox.Text;
            _options.Format = (AutomationFormat)Math.Max(0, FormatBox.SelectedIndex);
            _options.DestinationMask = MaskBox.Text;
            _options.FrameExtension = (String)FramesBox.SelectedItem;
            DialogResult = true;
        }
    }
}