namespace FrameAnimator.App.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using FrameAnimator.App.Infrastructure;

    /// <summary>Une langue de l'interface.</summary>
    public sealed record LanguageInfo(String Code, String NativeName);

    /// <summary>Texte qui suit la langue de l'interface (utilisé par l'extension de balisage XAML).</summary>
    public sealed class LocalizedText : ObservableObject
    {
        private String _value;

        public LocalizedText(String value)
        {
            _value = value;
        }

        public String Value
        {
            get => _value;
            set => SetField(ref _value, value);
        }
    }

    /// <summary>
    /// Traduit les textes de l'interface. Le texte anglais sert de clé : un texte sans traduction est affiché tel quel,
    /// l'anglais fonctionne donc sans dictionnaire et une traduction manquante n'est jamais une erreur.
    /// </summary>
    public sealed class Localizer
    {
        private const String LanguageKey = "language";
        private const String DefaultLanguage = "en";

        private readonly Dictionary<String, LocalizedText> _observed = [];
        private Dictionary<String, String> _translations = [];

        private Localizer()
        {
        }

        public static Localizer Instance { get; } = new Localizer();

        /// <summary>Langues proposées ; l'anglais n'a pas de dictionnaire.</summary>
        public static IReadOnlyList<LanguageInfo> Languages { get; } =
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

        public String Language { get; private set; } = DefaultLanguage;

        /// <summary>Déclenché quand la langue change.</summary>
        public event EventHandler? LanguageChanged;

        /// <summary>Traduit un texte dans la langue courante.</summary>
        public static String T(String text)
            => Instance._translations.TryGetValue(text, out String? translated) ? translated : text;

        /// <summary>Traduit un format puis le remplit.</summary>
        public static String F(String format, params Object[] arguments)
            => String.Format(CultureInfo.CurrentCulture, T(format), arguments);

        /// <summary>Langue du système si elle fait partie des nôtres, anglais sinon.</summary>
        public static String DetectSystemLanguage()
            => Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

        /// <summary>Démarre dans la langue choisie auparavant, ou celle du système.</summary>
        public void Initialize()
        {
            String? remembered = UserSettings.Get(LanguageKey);
            Apply(remembered is not null && Languages.Any(language => language.Code == remembered) ? remembered : DetectSystemLanguage());
        }

        /// <summary>Change la langue et la mémorise.</summary>
        public void SetLanguage(String code)
        {
            Apply(Normalize(code));
            UserSettings.Set(LanguageKey, Language);
        }

        /// <summary>Texte qui se met à jour lors d'un changement de langue.</summary>
        public LocalizedText Observe(String text)
        {
            if (!_observed.TryGetValue(text, out LocalizedText? observed))
            {
                observed = new LocalizedText(T(text));
                _observed[text] = observed;
            }
            return observed;
        }

        internal void Apply(String code)
        {
            Language = Normalize(code);
            _translations = Language == DefaultLanguage ? [] : LoadDictionary(Language);
            foreach ((String text, LocalizedText observed) in _observed)
                observed.Value = T(text);
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        private static String Normalize(String code)
        {
            String lower = code.ToLowerInvariant();
            return Languages.Any(language => language.Code == lower) ? lower : DefaultLanguage;
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
