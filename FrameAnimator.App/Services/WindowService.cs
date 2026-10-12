namespace FrameAnimator.App.Services
{
    using System;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;
    using CommunityToolkit.Mvvm.Messaging;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Views;

    /// <summary>What every window of the application gets: the themed title bar and, on request, its remembered position and size.</summary>
    public interface IWindowService
    {
        /// <summary>Remember the position of all windows.</summary>
        Boolean SavePosition { get; set; }

        /// <summary>Remember the size of all windows (and the "maximized" state).</summary>
        Boolean SaveSize { get; set; }

        /// <summary>
        /// Registers a window before it is shown: its title bar follows the theme, and its position and size are restored and
        /// saved according to the two options (each window type has its own entry).
        /// </summary>
        void Attach(Window window);
    }

    /// <summary>
    /// Implements <see cref="IWindowService"/>: the title bar follows the application theme (DWM dark mode), and the position and
    /// size of windows are stored in the settings, in the format "left,top,width,height,maximized".
    /// </summary>
    public sealed partial class WindowService : IWindowService, IRecipient<ThemeChangedMessage>
    {
        private const String PositionKey = "savePosition";
        private const String SizeKey = "saveSize";

        // DWMWA_USE_IMMERSIVE_DARK_MODE: 20 is the documented value, 19 is the one used by the first Windows 10 versions that had it
        private const Int32 ImmersiveDarkMode = 20;
        private const Int32 ImmersiveDarkModeBeforeBuild18985 = 19;

        private readonly ISettingsStore _settings;
        private readonly IThemeService _theme;
        private Boolean? _savePosition;
        private Boolean? _saveSize;

        public WindowService(ISettingsStore settings, IThemeService theme)
        {
            _settings = settings;
            _theme = theme;
        }

        public Boolean SavePosition
        {
            get => _savePosition ??= _settings.Get(PositionKey) == "1";
            set
            {
                _savePosition = value;
                _settings.Set(PositionKey, value ? "1" : "0");
            }
        }

        public Boolean SaveSize
        {
            get => _saveSize ??= _settings.Get(SizeKey) == "1";
            set
            {
                _saveSize = value;
                _settings.Set(SizeKey, value ? "1" : "0");
            }
        }

        /// <summary>
        /// Starts following the theme: the windows already open are updated when it changes, and any window that was not registered
        /// with <see cref="Attach"/> still gets its title bar when it loads (the bar may then stay white for a moment).
        /// </summary>
        public void Initialize()
        {
            WeakReferenceMessenger.Default.Register<ThemeChangedMessage>(this);
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
        }

        public void Receive(ThemeChangedMessage message)
        {
            if (Application.Current is null)
                return;
            foreach (Window window in Application.Current.Windows)
                ApplyTitleBar(window);
        }

        public void Attach(Window window)
        {
            // the theme is applied as soon as the handle is created, before the window is shown: no flash of a white title bar
            window.SourceInitialized += (_, _) => ApplyTitleBar(window);

            // message boxes are transient: they stay centered on the window that calls them
            if (window is MessageDialog)
                return;

            String key = "window." + window.GetType().Name;
            Restore(window, key);
            window.Closing += (_, _) => Save(window, key);
        }

        // ------------------------------------------------------------------ title bar

        private void OnWindowLoaded(Object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
                ApplyTitleBar(window);
        }

        private void ApplyTitleBar(Window window)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
                return;
            Int32 dark = _theme.IsDark ? 1 : 0;
            if (SetWindowAttribute(handle, ImmersiveDarkMode, ref dark, sizeof(Int32)) != 0)
                SetWindowAttribute(handle, ImmersiveDarkModeBeforeBuild18985, ref dark, sizeof(Int32));
        }

        [LibraryImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
        private static partial Int32 SetWindowAttribute(IntPtr window, Int32 attribute, ref Int32 value, Int32 size);

        // ------------------------------------------------------------------ position and size

        private static Boolean CanResize(Window window)
            => window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip && window.SizeToContent == SizeToContent.Manual;

        private String[]? Read(String key)
        {
            String? text = _settings.Get(key);
            String[]? parts = text?.Split(',');
            return parts is { Length: 5 } ? parts : null;
        }

        private static Boolean TryParse(String text, out Double value)
            => Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Double.IsFinite(value);

        private void Restore(Window window, String key)
        {
            String[]? parts = Read(key);
            if (parts is null)
                return;

            if (SaveSize && CanResize(window) && TryParse(parts[2], out Double width) && TryParse(parts[3], out Double height))
            {
                window.Width = Math.Clamp(width, Math.Max(window.MinWidth, 100), Math.Max(window.MinWidth, SystemParameters.VirtualScreenWidth));
                window.Height = Math.Clamp(height, Math.Max(window.MinHeight, 100), Math.Max(window.MinHeight, SystemParameters.VirtualScreenHeight));
                if (parts[4] == "1")
                    window.WindowState = WindowState.Maximized;
            }

            if (SavePosition && TryParse(parts[0], out Double left) && TryParse(parts[1], out Double top) && IsReachable(left, top))
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = left;
                window.Top = top;
            }
        }

        /// <summary>The remembered position must still leave the title bar within reach (a monitor may have been unplugged).</summary>
        private static Boolean IsReachable(Double left, Double top)
            => left > SystemParameters.VirtualScreenLeft - 200
               && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 60
               && top >= SystemParameters.VirtualScreenTop
               && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60;

        private void Save(Window window, String key)
        {
            if (!SavePosition && !SaveSize)
                return;

            Rect bounds = window.WindowState == WindowState.Normal
                ? new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight)
                : window.RestoreBounds;
            if (!Double.IsFinite(bounds.Left) || !Double.IsFinite(bounds.Top) || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            // only the values whose option is enabled are updated; the others keep their previous value
            String[] parts = Read(key) ?? ["", "", "", "", "0"];
            if (SavePosition)
            {
                parts[0] = bounds.Left.ToString("0.##", CultureInfo.InvariantCulture);
                parts[1] = bounds.Top.ToString("0.##", CultureInfo.InvariantCulture);
            }
            if (SaveSize && CanResize(window))
            {
                parts[2] = bounds.Width.ToString("0.##", CultureInfo.InvariantCulture);
                parts[3] = bounds.Height.ToString("0.##", CultureInfo.InvariantCulture);
                parts[4] = window.WindowState == WindowState.Maximized ? "1" : "0";
            }
            _settings.Set(key, String.Join(',', parts));
        }
    }
}
