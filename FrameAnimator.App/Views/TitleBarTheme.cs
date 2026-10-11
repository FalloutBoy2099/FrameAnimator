namespace FrameAnimator.App.Views
{
    using System;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;

    /// <summary>Donne à la barre de titre d'une fenêtre l'aspect clair ou sombre du thème de l'application.</summary>
    public static partial class TitleBarTheme
    {
        // DWMWA_USE_IMMERSIVE_DARK_MODE : 20 est la valeur documentée, 19 celle des premières versions de Windows 10 qui l'avaient
        private const Int32 ImmersiveDarkMode = 20;
        private const Int32 ImmersiveDarkModeBeforeBuild18985 = 19;

        /// <summary>
        /// Filet de sécurité : applique le thème à toute fenêtre qui ne s'est pas annoncée avec <see cref="Attach"/>
        /// (la barre peut alors rester blanche un instant à l'apparition).
        /// </summary>
        public static void ApplyToAllWindows()
        {
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
        }

        /// <summary>
        /// À appeler dans le constructeur d'une fenêtre : le thème est appliqué dès la création du handle, avant l'affichage,
        /// ce qui évite le flash d'une barre de titre blanche en thème sombre.
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