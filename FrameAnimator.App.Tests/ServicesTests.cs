namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Windows.Threading;
    using CommunityToolkit.Mvvm.Messaging;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;
    using FrameAnimator.App.Services;
    using FrameAnimator.App.ViewModels;
    using FrameAnimator.App.Views;
    using FrameAnimator.Core.Services;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The injectable services that replaced the static singletons: settings, preferences, folders memory, theme, language.</summary>
    public class ServicesTests
    {
        // ------------------------------------------------------------------ settings file

        [Fact]
        public void TheFileStoreKeepsValuesAndListsInOrder()
        {
            using TempDir dir = new();
            FileSettingsStore store = new(dir.Path);

            store.Set("theme", "dark");
            store.SetAll("favorite", ["a", "b"]);
            store.Set("language", "fr");
            store.SetAll("favorite", ["a", "b", "c"]);

            Assert.Equal("dark", store.Get("THEME"));
            Assert.Equal(["a", "b", "c"], store.GetAll("favorite"));
            Assert.Equal(["theme=dark", "favorite=a", "favorite=b", "favorite=c", "language=fr"], File.ReadAllLines(dir.File("settings.txt")));
        }

        [Fact]
        public void TwoStoresOnTheSameFolderShareTheirContent()
        {
            using TempDir dir = new();
            new FileSettingsStore(dir.Path).Set("language", "de");

            Assert.Equal("de", new FileSettingsStore(dir.Path).Get("language"));
        }

        [Fact]
        public void AMissingOrUnreadableFileIsEquivalentToNoPreferences()
        {
            using TempDir dir = new();
            FileSettingsStore store = new(dir.File("never created"));

            Assert.Null(store.Get("theme"));
            Assert.Empty(store.GetAll("favorite"));
        }

        [Fact]
        public void LinesWithoutAKeyOrAValueAreIgnored()
        {
            using TempDir dir = new();
            File.WriteAllLines(dir.File("settings.txt"), ["=x", "novalue=", "nothing", "ok = 1 "]);
            FileSettingsStore store = new(dir.Path);

            Assert.Equal("1", store.Get("ok"));
            Assert.Null(store.Get("novalue"));
        }

        // ------------------------------------------------------------------ preferences

        [Fact]
        public void DialogPreferencesAreStoredAndPreviewIsNot()
        {
            MemorySettingsStore store = new();
            DialogPreferences preferences = new(store);
            Assert.False(preferences.UseSystemDialogs);

            preferences.UseSystemDialogs = true;
            Assert.True(new DialogPreferences(store).UseSystemDialogs);

            preferences.Preview(false);
            Assert.False(preferences.UseSystemDialogs);
            Assert.Equal("1", store.Get("systemDialogs"));
        }

        [Fact]
        public void TheFileDialogMemoryForgetsFoldersThatNoLongerExist()
        {
            using TempDir dir = new();
            FileDialogMemory memory = new(new MemorySettingsStore());

            memory.SetLastDirectory(save: false, dir.Path);
            memory.SetLastDirectory(save: true, dir.File("gone"));

            Assert.Equal(dir.Path, memory.GetLastDirectory(save: false));
            Assert.Null(memory.GetLastDirectory(save: true));
        }

        [Fact]
        public void FavoritesIgnoreCaseAndTrailingSeparators()
        {
            using TempDir dir = new();
            FileDialogMemory memory = new(new MemorySettingsStore());

            memory.AddFavorite(dir.Path);
            memory.AddFavorite(dir.Path.ToUpperInvariant() + "\\");

            Assert.Single(memory.GetFavorites());
            Assert.True(memory.IsFavorite(dir.Path + "/"));
            memory.RemoveFavorite(dir.Path.ToLowerInvariant());
            Assert.Empty(memory.GetFavorites());
        }

        // ------------------------------------------------------------------ theme

        [Theory]
        [InlineData("dark", ThemePreference.Dark)]
        [InlineData("LIGHT", ThemePreference.Light)]
        [InlineData("automatic", ThemePreference.Automatic)]
        [InlineData("garbage", ThemePreference.Automatic)]
        [InlineData("99", ThemePreference.Automatic)]
        [InlineData(null, ThemePreference.Automatic)]
        public void AThemePreferenceIsParsedLeniently(String? text, ThemePreference expected)
            => Assert.Equal(expected, ThemeService.Parse(text));

        [Fact]
        public void SettingTheThemeStoresItAnnouncesItAndResolvesIsDark()
        {
            MemorySettingsStore store = new();
            ThemeService theme = new(store);
            List<ThemePreference> announced = [];
            Object recipient = new();
            WeakReferenceMessenger.Default.Register<Object, ThemeChangedMessage>(recipient, (_, message) => announced.Add(message.Preference));
            try
            {
                theme.SetPreference(ThemePreference.Dark);
                Assert.True(theme.IsDark);
                theme.SetPreference(ThemePreference.Light);
                Assert.False(theme.IsDark);
            }
            finally
            {
                WeakReferenceMessenger.Default.Unregister<ThemeChangedMessage>(recipient);
            }

            Assert.Contains(ThemePreference.Dark, announced); // the messenger is shared: other tests may announce too
            Assert.Contains(ThemePreference.Light, announced);
            Assert.Equal("light", store.Get("theme"));
        }

        [Fact]
        public void TheThemeStartsFromTheStoredPreference()
        {
            MemorySettingsStore store = new();
            store.Set("theme", "dark");
            ThemeService theme = new(store);

            theme.Initialize();

            Assert.Equal(ThemePreference.Dark, theme.Preference);
        }

        // ------------------------------------------------------------------ language

        [Fact]
        public void EnglishTextsAreReturnedAsTheyAre()
        {
            Localizer localizer = TestServices.Localizer();

            Assert.Equal("Open Project", localizer.T("Open Project"));
            Assert.Equal("Frame 2/5", localizer.F("Frame {0}/{1}", 2, 5));
        }

        [Fact]
        public void ChangingTheLanguageTranslatesStoresAndNotifies()
        {
            MemorySettingsStore store = new();
            Localizer localizer = new(store);
            LocalizedText observed = localizer.Observe("Open Project");
            List<String> announced = [];
            Object recipient = new();
            WeakReferenceMessenger.Default.Register<Object, LanguageChangedMessage>(recipient, (_, message) => announced.Add(message.Language));
            try
            {
                localizer.SetLanguage("fr");
            }
            finally
            {
                WeakReferenceMessenger.Default.Unregister<LanguageChangedMessage>(recipient);
            }

            Assert.Equal("fr", localizer.Language);
            Assert.Equal("fr", store.Get("language"));
            Assert.Contains("fr", announced); // the messenger is shared: other tests may announce too
            Assert.NotEqual("Open Project", localizer.T("Open Project"));
            Assert.Equal(localizer.T("Open Project"), observed.Value);
        }

        [Fact]
        public void AnUnknownLanguageFallsBackToEnglish()
        {
            Localizer localizer = TestServices.Localizer();
            localizer.SetLanguage("fr");

            localizer.SetLanguage("xx");

            Assert.Equal("en", localizer.Language);
            Assert.Equal("Open Project", localizer.T("Open Project"));
        }

        [Fact]
        public void TheLanguageStartsFromTheStoredOne()
        {
            MemorySettingsStore store = new();
            store.Set("language", "de");
            Localizer localizer = new(store);

            localizer.Initialize();

            Assert.Equal("de", localizer.Language);
        }

        [Fact]
        public void MessagesOfTheCoreAreTranslatedByTheSameService()
        {
            Localizer localizer = TestServices.Localizer();
            localizer.Apply("fr");
            UserMessage message = new(CoreMessages.UnableToLoadPalette, "x.act");

            String text = message.Render(localizer.T);

            Assert.Equal("Impossible de charger la palette 'x.act' !", text);
        }

        [Fact]
        public void EveryLanguageHasACodeAndANativeName()
        {
            Localizer localizer = TestServices.Localizer();

            Assert.Equal(11, localizer.Languages.Count);
            Assert.All(localizer.Languages, language =>
            {
                Assert.Equal(2, language.Code.Length);
                Assert.NotEmpty(language.NativeName);
            });
        }

        // ------------------------------------------------------------------ composition

        [Fact]
        public void TheCompositionRootResolvesEveryServiceOnce()
        {
            using TempDir dir = new();
            ServiceCollection services = new();
            AppServices.Register(services, dir.Path, Dispatcher.CurrentDispatcher, "");
            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.Same(provider.GetRequiredService<Localizer>(), provider.GetRequiredService<ILocalizer>());
            Assert.Same(provider.GetRequiredService<ThemeService>(), provider.GetRequiredService<IThemeService>());
            Assert.Same(provider.GetRequiredService<WindowService>(), provider.GetRequiredService<IWindowService>());
            Assert.Same(provider.GetRequiredService<DialogPreferences>(), provider.GetRequiredService<IDialogPreferences>());
            Assert.Same(provider.GetRequiredService<MessageService>(), provider.GetRequiredService<IMessageService>());
            Assert.Same(provider.GetRequiredService<DialogService>(), provider.GetRequiredService<IDialogService>());
            Assert.IsType<FileSettingsStore>(provider.GetRequiredService<ISettingsStore>());
            Assert.NotNull(provider.GetRequiredService<IFileDialogMemory>());
            Assert.NotNull(provider.GetRequiredService<FileDialogs>());
        }

        [Fact]
        public void ThePreferencesOfTheContainerLiveInTheGivenFolder()
        {
            using TempDir dir = new();
            ServiceCollection services = new();
            AppServices.Register(services, dir.Path, Dispatcher.CurrentDispatcher, "");
            using ServiceProvider provider = services.BuildServiceProvider();

            provider.GetRequiredService<IDialogPreferences>().UseSystemDialogs = true;
            provider.GetRequiredService<IWindowService>().SavePosition = true;

            String[] lines = File.ReadAllLines(dir.File("settings.txt"));
            Assert.Contains("systemDialogs=1", lines);
            Assert.Contains("savePosition=1", lines);
        }
    }
}
