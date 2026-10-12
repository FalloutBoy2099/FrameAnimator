namespace FrameAnimator.App.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Messaging;
    using FrameAnimator.Core.Services;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Services;

    /// <summary>A language of the interface.</summary>
    public sealed record LanguageInfo(String Code, String NativeName);

    /// <summary>Text that follows the interface language (used by the XAML markup extension).</summary>
    public sealed partial class LocalizedText(String value) : ObservableObject
    {
        [ObservableProperty]
        public partial String Value { get; set; } = value;
    }

    /// <summary>Translates the interface texts into the language chosen by the user.</summary>
    public interface ILocalizer
    {
        /// <summary>Code of the current language.</summary>
        String Language { get; }

        /// <summary>Available languages; English has no dictionary.</summary>
        IReadOnlyList<LanguageInfo> Languages { get; }

        /// <summary>Translates a text into the current language.</summary>
        String T(String text);

        /// <summary>Translates a format string, then fills it in.</summary>
        String F(String format, params Object[] arguments);

        /// <summary>Text that updates when the language changes.</summary>
        LocalizedText Observe(String text);

        /// <summary>Changes the language and stores it.</summary>
        void SetLanguage(String code);
    }

    /// <summary>
    /// Translates the interface texts. The English text is the key: a text without a translation is displayed as is,
    /// so English works without a dictionary and a missing translation is never an error.
    /// </summary>
    public sealed class Localizer(ISettingsStore settings) : ILocalizer
    {
        private const String LanguageKey = "language";
        private const String DefaultLanguage = "en";

        private readonly Dictionary<String, LocalizedText> _observed = [];
        private Dictionary<String, String> _translations = [];

        /// <summary>Available languages; English has no dictionary.</summary>
        public static IReadOnlyList<LanguageInfo> Supported { get; } =
        [
            new LanguageInfo("en", "English"),
            new LanguageInfo("cs", "Čeština"),
            new LanguageInfo("de", "Deutsch"),
            new LanguageInfo("es", "Español"),
            new LanguageInfo("fr", "Français"),
            new LanguageInfo("it", "Italiano"),
            new LanguageInfo("pl", "Polski"),
            new LanguageInfo("pt", "Português"),
            new LanguageInfo("ru", "Русский"),
            new LanguageInfo("sv", "Svenska"),
            new LanguageInfo("zh", "繁體中文"),
        ];

        public IReadOnlyList<LanguageInfo> Languages => Supported;

        public String Language { get; private set; } = DefaultLanguage;

        public String T(String text)
            => _translations.TryGetValue(text, out String? translated)
                ? translated
                : MessageCatalog.Translate(text, Language); // messages of the core

        public String F(String format, params Object[] arguments)
            => String.Format(CultureInfo.CurrentCulture, T(format), arguments);

        /// <summary>The system language if it is one of ours, English otherwise.</summary>
        public static String DetectSystemLanguage()
            => Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

        /// <summary>Starts in the previously chosen language, or the system one.</summary>
        public void Initialize()
        {
            String? remembered = settings.Get(LanguageKey);
            Apply(remembered is not null && Supported.Any(language => language.Code == remembered) ? remembered : DetectSystemLanguage());
        }

        public void SetLanguage(String code)
        {
            Apply(Normalize(code));
            settings.Set(LanguageKey, Language);
        }

        public LocalizedText Observe(String text)
        {
            if (!_observed.TryGetValue(text, out LocalizedText? observed))
            {
                observed = new LocalizedText(T(text));
                _observed[text] = observed;
            }
            return observed;
        }

        /// <summary>Switches to a language without remembering it (start-up and development switches).</summary>
        public void Apply(String code)
        {
            Language = Normalize(code);
            _translations = Language == DefaultLanguage ? [] : LoadDictionary(Language);
            foreach ((String text, LocalizedText observed) in _observed)
                observed.Value = T(text);
            WeakReferenceMessenger.Default.Send(new LanguageChangedMessage(Language));
        }

        private static String Normalize(String code)
        {
            String lower = code.ToLowerInvariant();
            return Supported.Any(language => language.Code == lower) ? lower : DefaultLanguage;
        }

        private static Dictionary<String, String> LoadDictionary(String code)
        {
            String resource = "FrameAnimator.App.Localization." + code + ".json";
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
            if (stream is null)
                return [];
            return JsonSerializer.Deserialize<Dictionary<String, String>>(stream) ?? [];
        }
    }
}
