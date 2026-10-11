namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using FrameAnimator.App.Infrastructure;
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

        public List<String> Warnings { get; } = [];
        public List<String> Questions { get; } = [];
        public List<OffsetDialogRequest> OffsetRequests { get; } = [];
        public Int32 AboutCount { get; private set; }

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

        public Int32? PickColor(Int32 initial) => ColorAnswer(initial);

        public void ShowWarning(String message) => Warnings.Add(message);

        public Boolean AskYesNo(String message)
        {
            Questions.Add(message);
            return YesNoAnswer;
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
