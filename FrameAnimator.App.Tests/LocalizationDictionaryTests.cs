namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using FrameAnimator.App.Localization;

    /// <summary>The interface dictionaries: what each translation must keep of its English text.</summary>
    public partial class LocalizationDictionaryTests
    {
        public static IEnumerable<Object[]> Languages
            => new Localizer(new MemorySettingsStore()).Languages.Where(language => language.Code != "en").Select(language => new Object[] { language.Code });

        private static Dictionary<String, String> Load(String code)
        {
            using Stream stream = typeof(Localizer).Assembly.GetManifestResourceStream("FrameAnimator.App.Localization." + code + ".json")!;
            return JsonSerializer.Deserialize<Dictionary<String, String>>(stream)!;
        }

        [GeneratedRegex(@"\{\d+\}")]
        private static partial Regex Placeholder();

        [Theory]
        [MemberData(nameof(Languages))]
        public void EveryLanguageHasADictionary(String code) => Assert.NotEmpty(Load(code));

        [Theory]
        [MemberData(nameof(Languages))]
        public void ATranslationKeepsThePlaceholdersOfItsEnglishText(String code)
        {
            foreach ((String english, String translated) in Load(code))
            {
                Assert.True(
                    Placeholder().Matches(english).Select(match => match.Value).Order().SequenceEqual(Placeholder().Matches(translated).Select(match => match.Value).Order()),
                    $"[{code}] '{english}' -> '{translated}'");
            }
        }

        [Theory]
        [MemberData(nameof(Languages))]
        public void ATranslationOfAMenuTextKeepsItsAccessKey(String code)
        {
            foreach ((String english, String translated) in Load(code))
            {
                Boolean hasAccessKey = english.Contains('_', StringComparison.Ordinal);
                Assert.True(hasAccessKey == translated.Contains('_', StringComparison.Ordinal), $"[{code}] '{english}' -> '{translated}'");
            }
        }

        [Theory]
        [MemberData(nameof(Languages))]
        public void NoTranslationIsEmpty(String code)
            => Assert.All(Load(code), pair => Assert.False(String.IsNullOrWhiteSpace(pair.Value), $"[{code}] '{pair.Key}'"));

        [Theory]
        [MemberData(nameof(Languages))]
        public void TheNewTextsOfTheEditingComfortAreTranslatedEverywhere(String code)
        {
            Dictionary<String, String> dictionary = Load(code);
            String[] keys =
            [
                "Relative pa_ths",
                "Animation pre_view",
                "Animation preview",
                "Previous frame",
                "Next frame",
                "Play",
                "Pause",
                "No frames",
                "Frame {0}/{1} - {2} fps",
                "Save changes to '{0}'?",
                "Recent _projects",
                "Clear list",
            ];
            Assert.All(keys, key => Assert.Contains(key, dictionary.Keys));
        }
    }
}
