namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CommunityToolkit.Mvvm.Messaging;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Services;
    using FrameAnimator.App.ViewModels;
    using FrameAnimator.Core.Model;

    /// <summary>The status bar texts, and the menus of theme, language and window options.</summary>
    public class StatusAndAppearanceTests
    {
        // ------------------------------------------------------------------ status bar

        [Fact]
        public void TheStatusBarDescribesTheProject()
        {
            Shell shell = Shell.Create();

            Assert.Equal("", shell.Status.StatusFrame);
            Assert.Equal("Pal: Default.act", shell.Status.StatusPalette);
            Assert.Equal("Transp: Auto", shell.Status.StatusTransparent);
            Assert.Equal("Lock Offsets", shell.Status.StatusLock);
            Assert.Equal("Autocutting", shell.Status.StatusAutoCutting);
        }

        [Fact]
        public void TheStatusTextsFollowTheSettings()
        {
            Shell shell = Shell.Create();
            List<String?> changed = [];
            shell.Status.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            shell.Session.Project.Settings.TransparentMode = TransparentMode.Palette;
            shell.Session.Project.Settings.LockOffsets = false;
            shell.Session.Refresh();

            Assert.Equal("Transp: Palette", shell.Status.StatusTransparent);
            Assert.Equal("", shell.Status.StatusLock);
            Assert.Contains(nameof(StatusBarViewModel.StatusTransparent), changed);
            Assert.Contains(nameof(StatusBarViewModel.StatusLock), changed);
        }

        [Fact]
        public void TheFramePanelFollowsTheSelection()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            Assert.Equal("Frame 1/2", shell.Status.StatusFrame);

            shell.FrameList.SelectedFrameIndex = 1;

            Assert.Equal("Frame 2/2", shell.Status.StatusFrame);
        }

        [Fact]
        public void TheLanguageMessageRefreshesTheStatusTexts()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            List<String?> changed = [];
            shell.Status.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            WeakReferenceMessenger.Default.Send(new LanguageChangedMessage("en"));

            Assert.Contains(nameof(StatusBarViewModel.StatusFrame), changed);
            Assert.Contains(nameof(StatusBarViewModel.StatusPalette), changed);
        }

        [Fact]
        public void TheProgressOfASessionIsMirroredByTheStatusBar()
        {
            Shell shell = Shell.Create();
            List<String?> changed = [];
            shell.Status.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            shell.Session.Interaction.Progress(3, 10);
            // the interaction marshals progress to the dispatcher of the session: from this thread it is immediate

            Assert.Equal(3, shell.Status.ProgressValue);
            Assert.Equal(10, shell.Status.ProgressMaximum);
            Assert.True(shell.Status.ProgressVisible);
            Assert.Contains(nameof(StatusBarViewModel.ProgressMaximum), changed);

            shell.Session.Interaction.Progress(0, 0);
            Assert.False(shell.Status.ProgressVisible);
        }

        // ------------------------------------------------------------------ appearance

        [Fact]
        public void TheThemeMenuHasOneEntryPerPreferenceAndMarksTheCurrentOne()
        {
            Shell shell = Shell.Create();

            Assert.Equal(["Automatic", "Light", "Dark"], shell.Appearance.ThemeChoices.Select(choice => choice.Header));
            Assert.True(shell.Appearance.ThemeChoices[0].IsCurrent);

            shell.Appearance.ThemeChoices[2].Command.Execute(shell.Appearance.ThemeChoices[2].Parameter);

            Assert.Equal(ThemePreference.Dark, shell.Theme.Preference);
            Assert.True(shell.Appearance.ThemeChoices[2].IsCurrent);
            Assert.False(shell.Appearance.ThemeChoices[0].IsCurrent);
        }

        [Fact]
        public void TheLanguageMenuListsEveryLanguageByItsNativeName()
        {
            Shell shell = Shell.Create();

            Assert.Equal(11, shell.Appearance.LanguageChoices.Count);
            Assert.Contains("Français", shell.Appearance.LanguageChoices.Select(choice => choice.Header));
            Assert.True(shell.Appearance.LanguageChoices.Single(choice => (String)choice.Parameter! == "en").IsCurrent);
        }

        [Fact]
        public void ChoosingALanguageChangesItAndTheCheckMarkFollows()
        {
            Shell shell = Shell.Create();
            MenuChoice german = shell.Appearance.LanguageChoices.Single(choice => (String)choice.Parameter! == "de");

            german.Command.Execute(german.Parameter);

            Assert.Equal("de", shell.Localizer.Language);
            Assert.True(german.IsCurrent);
            Assert.False(shell.Appearance.LanguageChoices.Single(choice => (String)choice.Parameter! == "en").IsCurrent);
        }

        [Fact]
        public void TheWindowAndDialogOptionsAreStoredByTheirServices()
        {
            Shell shell = Shell.Create();
            List<String?> changed = [];
            shell.Appearance.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            shell.Appearance.UseSystemDialogs = true;
            shell.Appearance.SaveWindowPosition = true;
            shell.Appearance.SaveWindowSize = true;
            shell.Appearance.SaveWindowSize = true; // unchanged: no notification

            Assert.True(shell.DialogPreferences.UseSystemDialogs);
            Assert.True(shell.Windows.SavePosition);
            Assert.True(shell.Windows.SaveSize);
            Assert.Equal(3, changed.Count);
        }
    }
}
