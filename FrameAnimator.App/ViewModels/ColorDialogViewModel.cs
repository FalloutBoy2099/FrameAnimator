namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.Globalization;
    using System.Windows.Media;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using FrameAnimator.App.Infrastructure;

    /// <summary>View model of the color picker: R, G, B (0-255) and the hexadecimal text stay in sync.</summary>
    public sealed partial class ColorDialogViewModel : DialogViewModelBase
    {
        private Boolean _updating;
        private Brush _preview = Brushes.Transparent;

        public ColorDialogViewModel(Int32 initialRgb)
        {
            SetColor(initialRgb & 0xFFFFFF);
        }

        [ObservableProperty]
        public partial Int32 R { get; set; }

        [ObservableProperty]
        public partial Int32 G { get; set; }

        [ObservableProperty]
        public partial Int32 B { get; set; }

        /// <summary>Hexadecimal text (RRGGBB); an invalid text is kept as typed and ignored.</summary>
        [ObservableProperty]
        public partial String Hex { get; set; } = "";

        /// <summary>Brush of the preview swatch.</summary>
        public Brush Preview
        {
            get => _preview;
            private set => SetProperty(ref _preview, value);
        }

        /// <summary>The current color, 0xRRGGBB.</summary>
        public Int32 Current => (R << 16) | (G << 8) | B;

        /// <summary>Chosen color, 0xRRGGBB (set by the OK command).</summary>
        public Int32 Result { get; private set; }

        partial void OnRChanged(Int32 value)
        {
            if (value is < 0 or > 255)
                R = Math.Clamp(value, 0, 255);
            else
                OnComponentChanged();
        }

        partial void OnGChanged(Int32 value)
        {
            if (value is < 0 or > 255)
                G = Math.Clamp(value, 0, 255);
            else
                OnComponentChanged();
        }

        partial void OnBChanged(Int32 value)
        {
            if (value is < 0 or > 255)
                B = Math.Clamp(value, 0, 255);
            else
                OnComponentChanged();
        }

        private void OnComponentChanged()
        {
            if (!_updating)
                Hex = Current.ToString("X6", CultureInfo.InvariantCulture);
            RefreshPreview();
        }

        partial void OnHexChanged(String value)
        {
            if (_updating)
                return;
            if (Int32.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out Int32 rgb))
            {
                _updating = true;
                R = (rgb >> 16) & 0xFF;
                G = (rgb >> 8) & 0xFF;
                B = rgb & 0xFF;
                _updating = false;
                RefreshPreview();
            }
        }

        private void SetColor(Int32 rgb)
        {
            _updating = true;
            R = (rgb >> 16) & 0xFF;
            G = (rgb >> 8) & 0xFF;
            B = rgb & 0xFF;
            Hex = rgb.ToString("X6", CultureInfo.InvariantCulture);
            _updating = false;
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            SolidColorBrush brush = new(ImageConversion.ToColor(Current));
            brush.Freeze();
            Preview = brush;
        }

        [RelayCommand]
        private void Ok()
        {
            Result = Current;
            RequestClose(true);
        }
    }
}
