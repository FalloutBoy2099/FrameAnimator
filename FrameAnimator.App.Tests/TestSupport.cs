namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Windows;
    using System.Windows.Threading;
    using CommunityToolkit.Mvvm.Messaging;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;
    using FrameAnimator.App.Services;
    using FrameAnimator.App.ViewModels;
    using FrameAnimator.Core.Imaging;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;

    /// <summary>Temporary folder deleted at the end of the test.</summary>
    internal sealed class TempDir : IDisposable
    {
        public String Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fa_app_tests_" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public String File(String name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // best effort
            }
        }
    }

    /// <summary>
    /// Dialog service for tests: answers come from public fields, every call is recorded.
    /// </summary>
    internal sealed class FakeDialogService : IDialogService
    {
        public Func<String, String, Boolean, String[]> OpenFilesAnswer { get; set; } = (_, _, _) => [];
        public Func<String, String, Int32, String?, String, String?> SaveFileAnswer { get; set; } = (_, _, _, _, _) => null;
        public Func<OffsetDialogRequest, OffsetDialogResult?> OffsetAnswer { get; set; } = _ => null;
        public Func<ProjectSettings, Int32, Boolean> OptionsAnswer { get; set; } = (_, _) => false;
        public Func<String, AutomationOptions?> AutomateAnswer { get; set; } = _ => null;
        public Func<Int32, Int32?> ColorAnswer { get; set; } = _ => null;
        public Boolean YesNoAnswer { get; set; }
        public MessageBoxResult YesNoCancelAnswer { get; set; } = MessageBoxResult.Cancel;

        public List<String> Warnings { get; } = [];
        public List<String> Questions { get; } = [];
        public List<OffsetDialogRequest> OffsetRequests { get; } = [];
        public Int32 AboutCount { get; private set; }
        public Int32 PreviewToggles { get; private set; }

        public String[] OpenFiles(String title, String filter, Boolean multiSelect) => OpenFilesAnswer(title, filter, multiSelect);

        /// <summary>The <c>overwritePrompt</c> argument of the last SaveFile call.</summary>
        public Boolean? LastOverwritePrompt { get; private set; }

        public String? SaveFile(String title, String filter, Int32 filterIndex, String? fileName, String defaultExt, Boolean overwritePrompt = true)
        {
            LastOverwritePrompt = overwritePrompt;
            return SaveFileAnswer(title, filter, filterIndex, fileName, defaultExt);
        }

        public OffsetDialogResult? ShowOffset(OffsetDialogRequest request)
        {
            OffsetRequests.Add(request);
            return OffsetAnswer(request);
        }

        public Boolean ShowOptions(ProjectSettings settings, Int32 frameCount) => OptionsAnswer(settings, frameCount);

        public AutomationOptions? ShowAutomate(String currentPalette) => AutomateAnswer(currentPalette);

        public void ShowAbout() => AboutCount++;

        public void TogglePreview(AnimationPreviewViewModel viewModel) => PreviewToggles++;

        public Int32? PickColor(Int32 initial) => ColorAnswer(initial);

        public void ShowWarning(String message) => Warnings.Add(message);

        public Boolean AskYesNo(String message)
        {
            Questions.Add(message);
            return YesNoAnswer;
        }

        public MessageBoxResult AskYesNoCancel(String message)
        {
            Questions.Add(message);
            return YesNoCancelAnswer;
        }
    }

    /// <summary>Preferences kept in memory: nothing is written to the disk.</summary>
    internal sealed class MemorySettingsStore : ISettingsStore
    {
        private readonly List<(String Key, String Value)> _entries = [];

        public String? Get(String key) => GetAll(key) is { Count: > 0 } values ? values[0] : null;

        public IReadOnlyList<String> GetAll(String key)
        {
            List<String> values = [];
            foreach ((String entryKey, String value) in _entries)
            {
                if (entryKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                    values.Add(value);
            }
            return values;
        }

        public void Set(String key, String value) => SetAll(key, [value]);

        public void SetAll(String key, IEnumerable<String> values)
        {
            Int32 position = _entries.FindIndex(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            _entries.RemoveAll(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            List<(String, String)> added = [];
            foreach (String value in values)
                added.Add((key, value));
            _entries.InsertRange(position < 0 ? _entries.Count : position, added);
        }
    }

    /// <summary>Theme service that only remembers the preference (no application, no windows) and announces the change.</summary>
    internal sealed class FakeThemeService : IThemeService
    {
        public ThemePreference Preference { get; private set; }

        public Boolean IsDark => Preference == ThemePreference.Dark;

        public void SetPreference(ThemePreference preference)
        {
            Preference = preference;
            WeakReferenceMessenger.Default.Send(new ThemeChangedMessage(preference));
        }
    }

    internal sealed class FakeWindowService : IWindowService
    {
        public Boolean SavePosition { get; set; }

        public Boolean SaveSize { get; set; }

        public void Attach(Window window)
        {
        }
    }

    internal sealed class FakeDialogPreferences : IDialogPreferences
    {
        public Boolean UseSystemDialogs { get; set; }
    }

    /// <summary>The services a view model needs, in memory, with the interface in English.</summary>
    internal static class TestServices
    {
        public static Localizer Localizer() => new(new MemorySettingsStore());
    }

    /// <summary>A ticker the test advances by hand.</summary>
    internal sealed class ManualTicker : ITicker
    {
        public event EventHandler? Tick;

        public TimeSpan Interval { get; set; } = TimeSpan.FromMilliseconds(100);

        public Boolean IsRunning { get; private set; }

        public Int32 Starts { get; private set; }

        public void Start()
        {
            IsRunning = true;
            Starts++;
        }

        public void Stop() => IsRunning = false;

        /// <summary>One interval elapsed (nothing happens when the ticker is stopped).</summary>
        public void Fire()
        {
            if (IsRunning)
                Tick?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The view models of the main window, assembled like the application does, over fakes and in-memory preferences.</summary>
    internal sealed class Shell
    {
        private Shell(FakeDialogService dialogs)
        {
            Dialogs = dialogs;
            Localizer = TestServices.Localizer();
            Theme = new FakeThemeService();
            Windows = new FakeWindowService();
            DialogPreferences = new FakeDialogPreferences();
            Preferences = new ProjectPreferences(new MemorySettingsStore());
            Session = new ProjectSession(Dispatcher.CurrentDispatcher, dialogs, Localizer, "");
            FrameList = new FrameListViewModel(Session, dialogs, Localizer);
            Canvas = new CanvasViewModel(Session, FrameList, dialogs, Localizer);
            Status = new StatusBarViewModel(Session, FrameList, Canvas, Localizer);
            Appearance = new AppearanceViewModel(Localizer, Theme, DialogPreferences, Windows);
            Ticker = new ManualTicker();
            Preview = new AnimationPreviewViewModel(Session, Ticker, Localizer);
            Main = new MainViewModel(Session, FrameList, Canvas, Status, Appearance, Preview, dialogs, Preferences, Localizer);
        }

        public FakeDialogService Dialogs { get; }
        public Localizer Localizer { get; }
        public FakeThemeService Theme { get; }
        public FakeWindowService Windows { get; }
        public FakeDialogPreferences DialogPreferences { get; }
        public ProjectPreferences Preferences { get; }
        public ProjectSession Session { get; }
        public FrameListViewModel FrameList { get; }
        public CanvasViewModel Canvas { get; }
        public StatusBarViewModel Status { get; }
        public AppearanceViewModel Appearance { get; }
        public ManualTicker Ticker { get; }
        public AnimationPreviewViewModel Preview { get; }
        public MainViewModel Main { get; }

        public static Shell Create(FakeDialogService? dialogs = null) => new(dialogs ?? new FakeDialogService());

        /// <summary>A shell that already holds two frames in the current direction.</summary>
        public static Shell WithTwoFrames(TempDir dir, FakeDialogService? dialogs = null)
        {
            Shell shell = Create(dialogs);
            shell.Dialogs.OpenFilesAnswer = (_, _, _) => [Images.WriteBmp(dir, "a.bmp"), Images.WriteBmp(dir, "b.bmp")];
            shell.FrameList.AddFramesCommand.Execute(null);
            return shell;
        }
    }

    internal static class Images
    {
        /// <summary>Writes a small BMP and returns its path.</summary>
        public static String WriteBmp(TempDir dir, String name, Int32 width = 12, Int32 height = 9)
        {
            Bitmap32 bitmap = new(width, height);
            Array.Fill(bitmap.Pixels, 0x102030);
            for (Int32 y = 2; y < height - 2; y++)
                for (Int32 x = 2; x < width - 2; x++)
                    bitmap[x, y] = 0xE0A040;
            String path = dir.File(name);
            BmpCodec.WriteFile(bitmap, path);
            return path;
        }
    }
}
