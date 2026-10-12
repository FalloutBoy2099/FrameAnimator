namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using CommunityToolkit.Mvvm.Messaging;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;
    using FrameAnimator.App.Services;

    /// <summary>
    /// The user's preferences about the application itself (not about the project): theme, language, and the options of dialogs and
    /// windows. Each one is stored by its service; this view model only exposes them to the menus.
    /// </summary>
    public sealed partial class AppearanceViewModel : ObservableObject, IRecipient<LanguageChangedMessage>, IRecipient<ThemeChangedMessage>
    {
        private readonly ILocalizer _localizer;
        private readonly IThemeService _theme;
        private readonly IDialogPreferences _dialogPreferences;
        private readonly IWindowService _windows;

        public AppearanceViewModel(ILocalizer localizer, IThemeService theme, IDialogPreferences dialogPreferences, IWindowService windows)
        {
            _localizer = localizer;
            _theme = theme;
            _dialogPreferences = dialogPreferences;
            _windows = windows;

            ThemeChoices = Enum.GetValues<ThemePreference>()
                .Select(preference => new MenuChoice(localizer.T(preference.ToString()), ChangeThemeCommand, preference))
                .ToArray();
            LanguageChoices = localizer.Languages
                .Select(language => new MenuChoice(language.NativeName, ChangeLanguageCommand, language.Code))
                .ToArray();
            UpdateChoices();

            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        /// <summary>Entries of the theme menu (Automatic / Light / Dark).</summary>
        public IReadOnlyList<MenuChoice> ThemeChoices { get; }

        /// <summary>Entries of the language menu.</summary>
        public IReadOnlyList<MenuChoice> LanguageChoices { get; }

        /// <summary>Windows "legacy" dialogs (files, messages) instead of the application's own.</summary>
        public Boolean UseSystemDialogs
        {
            get => _dialogPreferences.UseSystemDialogs;
            set
            {
                if (_dialogPreferences.UseSystemDialogs == value)
                    return;
                _dialogPreferences.UseSystemDialogs = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Remember the position of all windows.</summary>
        public Boolean SaveWindowPosition
        {
            get => _windows.SavePosition;
            set
            {
                if (_windows.SavePosition == value)
                    return;
                _windows.SavePosition = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Remember the size of all windows.</summary>
        public Boolean SaveWindowSize
        {
            get => _windows.SaveSize;
            set
            {
                if (_windows.SaveSize == value)
                    return;
                _windows.SaveSize = value;
                OnPropertyChanged();
            }
        }

        public void Receive(LanguageChangedMessage message) => UpdateChoices();

        public void Receive(ThemeChangedMessage message) => UpdateChoices();

        /// <summary>Texts and check marks of the theme and language menus follow the current choices.</summary>
        private void UpdateChoices()
        {
            foreach (MenuChoice choice in ThemeChoices)
            {
                ThemePreference preference = (ThemePreference)choice.Parameter!;
                choice.Header = _localizer.T(preference.ToString());
                choice.IsCurrent = preference == _theme.Preference;
            }
            foreach (MenuChoice choice in LanguageChoices)
                choice.IsCurrent = (String)choice.Parameter! == _localizer.Language;
        }

        [RelayCommand]
        private void ChangeTheme(ThemePreference preference) => _theme.SetPreference(preference);

        [RelayCommand]
        private void ChangeLanguage(String code) => _localizer.SetLanguage(code);
    }
}
