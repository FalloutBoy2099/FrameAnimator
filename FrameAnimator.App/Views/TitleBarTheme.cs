namespace FrameAnimator.App.Views
{
    using System;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;

    /// <summary>Gives a window's title bar the light or dark look of the application theme.</summary>
    public static partial class TitleBarTheme
    {
        // DWMWA_USE_IMMERSIVE_DARK_MODE: 20 is the documented value, 19 is the one used by the first Windows 10 versions that had it
        private const Int32 ImmersiveDarkMode = 20;
        private const Int32 ImmersiveDarkModeBeforeBuild18985 = 19;

        /// <summary>
        /// Safety net: applies the theme to any window that did not register with <see cref="Attach"/>
        /// (the bar may then stay white for a moment when the window appears).
        /// </summary>
        public static void ApplyToAllWindows()
        {
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
        }

        /// <summary>
        /// Call from a window's constructor: the theme is applied as soon as the handle is created, before the window is shown,
        /// which avoids a flash of a white title bar in the dark theme.
        /// </summary>
        public static void Attach(Window window)
        {
            window.SourceInitialized += (_, _) => Apply(window);
        }

        public static void Apply(Window window)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
                Apply(handle);
        }

        private static void OnWindowLoaded(Object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
                Apply(window);
        }

        private static void Apply(IntPtr handle)
        {
            Int32 dark = ThemeManager.IsDark() ? 1 : 0;
            if (SetWindowAttribute(handle, ImmersiveDarkMode, ref dark, sizeof(Int32)) != 0)
                SetWindowAttribute(handle, ImmersiveDarkModeBeforeBuild18985, ref dark, sizeof(Int32));
        }

        [LibraryImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
        private static partial Int32 SetWindowAttribute(IntPtr window, Int32 attribute, ref Int32 value, Int32 size);
    }
}