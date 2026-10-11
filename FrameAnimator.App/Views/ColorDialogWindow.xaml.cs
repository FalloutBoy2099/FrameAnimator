namespace FrameAnimator.App.Views
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Media;
    using Infrastructure;

    public partial class ColorDialogWindow : Window
    {
        private Boolean _updating;

        public ColorDialogWindow(Int32 initial)
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);
            SetColor(initial & 0xFFFFFF);
        }

        /// <summary>Chosen color, 0xRRGGBB.</summary>
        public Int32 Result { get; private set; }

        private void SetColor(Int32 rgb)
        {
            _updating = true;
            RSlider.Value = (rgb >> 16) & 0xFF;
            GSlider.Value = (rgb >> 8) & 0xFF;
            BSlider.Value = rgb & 0xFF;
            HexBox.Text = rgb.ToString("X6", CultureInfo.InvariantCulture);
            _updating = false;
            Refresh();
        }

        private Int32 Current => ((Int32)RSlider.Value << 16) | ((Int32)GSlider.Value << 8) | (Int32)BSlider.Value;

        private void Refresh()
        {
            RText.Text = ((Int32)RSlider.Value).ToString(CultureInfo.InvariantCulture);
            GText.Text = ((Int32)GSlider.Value).ToString(CultureInfo.InvariantCulture);
            BText.Text = ((Int32)BSlider.Value).ToString(CultureInfo.InvariantCulture);
            Preview.Background = new SolidColorBrush(ImageConversion.ToColor(Current));
        }

        private void OnSliderChanged(Object sender, RoutedPropertyChangedEventArgs<Double> e)
        {
            if (_updating || !IsLoaded && HexBox is null)
                return;
            _updating = true;
            HexBox.Text = Current.ToString("X6", CultureInfo.InvariantCulture);
            _updating = false;
            Refresh();
        }

        private void OnHexChanged(Object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_updating)
                return;
            if (Int32.TryParse(HexBox.Text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
            {
                _updating = true;
                RSlider.Value = (rgb >> 16) & 0xFF;
                GSlider.Value = (rgb >> 8) & 0xFF;
                BSlider.Value = rgb & 0xFF;
                _updating = false;
                Refresh();
            }
        }

        private void OnOk(Object sender, RoutedEventArgs e)
        {
            Result = Current;
            DialogResult = true;
        }
    }
}