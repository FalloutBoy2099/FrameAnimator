namespace FrameAnimator.App.Views
{
    using System;
    using System.Windows;
    using Microsoft.Win32;
	using Infrastructure;

	/// <summary>Choix de thème de l'utilisateur : suivre Windows, toujours clair ou toujours sombre.</summary>
	public enum ThemePreference
    {
        Automatic,
        Light,
        Dark,
    }

    /// <summary>Applique la préférence de thème à l'application et la mémorise.</summary>
    public static class ThemeManager
    {
        private const String ThemeKey = "theme";

        public static ThemePreference Preference { get; private set; } = ThemePreference.Automatic;

        /// <summary>Déclenché après un changement de thème.</summary>
        public static event EventHandler? Changed;

        public static void Initialize()
        {
            Preference = Parse(UserSettings.Get(ThemeKey));
            ApplyToApplication();
        }

        public static void SetPreference(ThemePreference preference)
        {
            Preference = preference;
            UserSettings.Set(ThemeKey, preference.ToString().ToLowerInvariant());
            ApplyToApplication();
            foreach (Window window in Application.Current.Windows)
                TitleBarTheme.Apply(window);
            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Applique un thème sans le mémoriser (commutateurs de développement).</summary>
        internal static void Preview(ThemePreference preference)
        {
            Preference = preference;
            ApplyToApplication();
        }

        /// <summary>Indique si l'application est dessinée en thème sombre (préférence, puis réglage de Windows).</summary>
        public static Boolean IsDark() => Preference switch
        {
            ThemePreference.Light => false,
            ThemePreference.Dark => true,
            _ => IsWindowsDark(),
        };

        internal static ThemePreference Parse(String? text)
            => Enum.TryParse(text, true, out ThemePreference preference) && Enum.IsDefined(preference) ? preference : ThemePreference.Automatic;

        private static Boolean IsWindowsDark()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is Int32 light && light == 0;
        }

        private static void ApplyToApplication()
        {
            Application.Current.ThemeMode = Preference switch
            {
                ThemePreference.Light => ThemeMode.Light,
                ThemePreference.Dark => ThemeMode.Dark,
                _ => ThemeMode.System,
            };
        }
    }
}