namespace FrameAnimator.App.Services
{
    using System;
    using System.Windows;
    using CommunityToolkit.Mvvm.Messaging;
    using FrameAnimator.App.Infrastructure;
    using Microsoft.Win32;

    /// <summary>User's theme choice: follow Windows, always light or always dark.</summary>
    public enum ThemePreference
    {
        Automatic,
        Light,
        Dark,
    }

    /// <summary>The application's theme: the user's preference and what it resolves to.</summary>
    public interface IThemeService
    {
        ThemePreference Preference { get; }

        /// <summary>Indicates whether the application is drawn with the dark theme (preference first, then the Windows setting).</summary>
        Boolean IsDark { get; }

        /// <summary>Applies and remembers the preference, then announces it with a <see cref="ThemeChangedMessage"/>.</summary>
        void SetPreference(ThemePreference preference);
    }

    /// <summary>Applies the theme preference to the application and remembers it.</summary>
    public sealed class ThemeService(ISettingsStore settings) : IThemeService
    {
        private const String ThemeKey = "theme";

        public ThemePreference Preference { get; private set; } = ThemePreference.Automatic;

        public Boolean IsDark => Preference switch
        {
            ThemePreference.Light => false,
            ThemePreference.Dark => true,
            _ => IsWindowsDark(),
        };

        /// <summary>Reads the stored preference and applies it (start of the application).</summary>
        public void Initialize()
        {
            Preference = Parse(settings.Get(ThemeKey));
            ApplyToApplication();
        }

        public void SetPreference(ThemePreference preference)
        {
            Preference = preference;
            settings.Set(ThemeKey, preference.ToString().ToLowerInvariant());
            ApplyToApplication();
            WeakReferenceMessenger.Default.Send(new ThemeChangedMessage(preference));
        }

        /// <summary>Applies a theme without remembering it (development switches).</summary>
        public void Preview(ThemePreference preference)
        {
            Preference = preference;
            ApplyToApplication();
        }

        internal static ThemePreference Parse(String? text)
            => Enum.TryParse(text, true, out ThemePreference preference) && Enum.IsDefined(preference) ? preference : ThemePreference.Automatic;

        private static Boolean IsWindowsDark()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is Int32 light && light == 0;
        }

        private void ApplyToApplication()
        {
            if (Application.Current is null)
                return;
            Application.Current.ThemeMode = Preference switch
            {
                ThemePreference.Light => ThemeMode.Light,
                ThemePreference.Dark => ThemeMode.Dark,
                _ => ThemeMode.System,
            };
        }
    }
}
