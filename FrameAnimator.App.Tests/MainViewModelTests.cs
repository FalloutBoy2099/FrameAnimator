namespace FrameAnimator.App.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;

    /// <summary>The file and project actions of the main window, and how its parts are wired together.</summary>
    public class MainViewModelTests
    {
        [Fact]
        public void StartsWithAnEmptyProject()
        {
            Shell shell = Shell.Create();

            Assert.Equal("Untitled.fpr — Frame Animator", shell.Main.Title);
            Assert.True(shell.Main.IsIdle);
            Assert.False(shell.Main.IsBatchRunning);
        }

        [Fact]
        public void TheWindowIsMadeOfItsParts()
        {
            Shell shell = Shell.Create();

            Assert.Same(shell.FrameList, shell.Main.FrameList);
            Assert.Same(shell.Canvas, shell.Main.Canvas);
            Assert.Same(shell.Status, shell.Main.Status);
            Assert.Same(shell.Appearance, shell.Main.Appearance);
            Assert.Same(shell.Session.Project, shell.Main.Project);
        }

        [Fact]
        public void EnablingRulesFollowTheContent()
        {
            Shell shell = Shell.Create();

            Assert.True(shell.Main.NewCommand.CanExecute(null));
            Assert.True(shell.Main.OpenCommand.CanExecute(null));
            Assert.True(shell.Main.SaveCommand.CanExecute(null));
            Assert.True(shell.Main.AutomateCommand.CanExecute(null));
            Assert.False(shell.Main.CreateFrmCommand.CanExecute(null));
            Assert.False(shell.Main.UndoCommand.CanExecute(null));
            Assert.False(shell.Main.CancelCommand.CanExecute(null));
        }

        [Fact]
        public void ANewProjectEmptiesTheList()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Dialogs.YesNoCancelAnswer = MessageBoxResult.No; // discard the unsaved frames

            shell.Main.NewCommand.Execute(null);

            Assert.Empty(shell.FrameList.Frames);
            Assert.False(shell.Session.HasFrames);
        }

        [Fact]
        public void AFileOpenedFromTheDialogReplacesTheProject()
        {
            using TempDir dir = new();
            Shell source = Shell.WithTwoFrames(dir);
            String project = dir.File("p.fpr");
            Assert.True(source.Main.Service.SaveProject(project, dir.File("p.BMP")));

            Shell shell = Shell.Create();
            shell.Dialogs.OpenFilesAnswer = (_, _, _) => [project];
            shell.Main.OpenCommand.Execute(null);

            Assert.Equal(2, shell.FrameList.Frames.Count);
            Assert.Equal("p.fpr — Frame Animator", shell.Main.Title);
        }

        [Fact]
        public void SaveAsAsksForAnFprNameAndWritesTheProject()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            String target = dir.File("saved.fpr");
            String? suggested = null;
            shell.Dialogs.SaveFileAnswer = (_, _, _, name, _) =>
            {
                suggested = name;
                return target;
            };

            shell.Main.SaveAsCommand.Execute(null);

            Assert.Equal("Untitled.fpr", suggested);
            Assert.True(File.Exists(target));
            Assert.Equal("saved.fpr — Frame Animator", shell.Main.Title);
        }

        [Fact]
        public void CancellingSaveAsWritesNothing()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);

            shell.Main.SaveAsCommand.Execute(null);

            Assert.Equal("*Untitled.fpr — Frame Animator", shell.Main.Title); // still unsaved
        }

        [Fact]
        public void SaveWithoutAProjectFileFallsBackToSaveAs()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            Int32 asked = 0;
            shell.Dialogs.SaveFileAnswer = (_, _, _, _, _) =>
            {
                asked++;
                return null;
            };

            shell.Main.SaveCommand.Execute(null);

            Assert.Equal(1, asked);
        }

        [Fact]
        public void OptionsCommandPassesTheSettingsAndTheFrameCount()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            ProjectSettings? seen = null;
            Int32 count = -1;
            shell.Dialogs.OptionsAnswer = (settings, frames) =>
            {
                seen = settings;
                count = frames;
                settings.AutoCutting = false;
                return true;
            };

            shell.Main.OptionsCommand.Execute(null);

            Assert.Same(shell.Session.Project.Settings, seen);
            Assert.Equal(2, count);
            Assert.Equal("", shell.Status.StatusAutoCutting);
        }

        // ------------------------------------------------------------------ relative paths

        [Fact]
        public void RelativePathsAreOffByDefaultAndFollowTheChoice()
        {
            Shell shell = Shell.Create();
            Assert.False(shell.Main.SaveRelativePaths);
            Assert.False(shell.Main.Service.SaveRelativePaths);

            shell.Main.SaveRelativePaths = true;

            Assert.True(shell.Main.Service.SaveRelativePaths);
            Assert.True(shell.Preferences.SaveRelativePaths);
        }

        [Fact]
        public void AProjectSavedWithRelativePathsHasNoAbsolutePathInIt()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Main.SaveRelativePaths = true;
            String target = dir.File("rel.fpr");
            shell.Dialogs.SaveFileAnswer = (_, _, _, _, _) => target;

            shell.Main.SaveAsCommand.Execute(null);

            String text = File.ReadAllText(target);
            Assert.DoesNotContain(dir.Path, text);
            Assert.Contains("a.bmp", text);
        }

        // ------------------------------------------------------------------ batch

        [Fact]
        public async Task ABatchWorksOnAProjectOfItsOwnAndLeavesTheOneOnScreenAlone()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            String source = dir.File("src.fpr");
            Assert.True(shell.Main.Service.SaveProject(source, dir.File("src.BMP")));
            String frame = shell.FrameList.Frames[0];
            Directory.CreateDirectory(dir.File("out"));
            AutomationOptions options = new() { DestinationMask = Path.Combine(dir.File("out"), "*.FPR") };
            options.Sources.Add(source);
            shell.Dialogs.AutomateAnswer = _ => options;
            Int32 busyNotifications = 0;
            shell.Main.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(shell.Main.IsBatchRunning))
                    busyNotifications++;
            };

            await shell.Main.AutomateCommand.ExecuteAsync(null);

            Assert.True(File.Exists(Path.Combine(dir.File("out"), "src.FPR")), String.Join("|", shell.Dialogs.Warnings));
            Assert.Equal(2, shell.FrameList.Frames.Count);
            Assert.Equal(frame, shell.FrameList.Frames[0]);
            Assert.False(shell.Main.IsBatchRunning);
            Assert.True(shell.Main.IsIdle);
            Assert.Equal(2, busyNotifications); // started, finished
            Assert.True(shell.Main.AutomateCommand.CanExecute(null));
        }

        [Fact]
        public async Task CancellingTheAutomateDialogStartsNothing()
        {
            Shell shell = Shell.Create();
            shell.Dialogs.AutomateAnswer = _ => null;

            await shell.Main.AutomateCommand.ExecuteAsync(null);

            Assert.False(shell.Main.IsBatchRunning);
        }

        [Fact]
        public void WhileABatchRunsAnotherJobCannotStartButTheProjectStaysEditable()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);

            using (shell.Session.BeginBatch())
            {
                Assert.False(shell.Main.AutomateCommand.CanExecute(null));
                Assert.False(shell.Main.NewCommand.CanExecute(null));
                Assert.False(shell.Main.OpenCommand.CanExecute(null));
                Assert.False(shell.Main.ExecuteScriptCommand.CanExecute(null));
                Assert.True(shell.Main.CancelCommand.CanExecute(null));
                Assert.True(shell.FrameList.AddFramesCommand.CanExecute(null));
                Assert.True(shell.Main.SaveCommand.CanExecute(null));
            }

            Assert.True(shell.Main.AutomateCommand.CanExecute(null));
            Assert.False(shell.Main.CancelCommand.CanExecute(null));
        }

        [Fact]
        public void WhileAScriptRunsTheProjectIsLocked()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);

            using (shell.Session.BeginBusy())
            {
                Assert.True(shell.Main.IsBusy);
                Assert.False(shell.Main.SaveCommand.CanExecute(null));
                Assert.False(shell.FrameList.AddFramesCommand.CanExecute(null));
                Assert.False(shell.FrameList.RemoveFramesCommand.CanExecute(null));
                Assert.False(shell.Canvas.ToggleMoveCommand.CanExecute(null));
                Assert.True(shell.Main.CancelCommand.CanExecute(null));
            }

            Assert.False(shell.Main.IsBusy);
            Assert.True(shell.FrameList.AddFramesCommand.CanExecute(null));
        }

        [Fact]
        public void CancelAsksForConfirmationBeforeRequestingTheStop()
        {
            Shell shell = Shell.Create();
            using (shell.Session.BeginBatch())
            {
                shell.Dialogs.YesNoAnswer = false;
                shell.Main.CancelCommand.Execute(null);
                Assert.False(shell.Session.Interaction.CancelRequested);

                shell.Dialogs.YesNoAnswer = true;
                shell.Main.CancelCommand.Execute(null);
                Assert.True(shell.Session.Interaction.CancelRequested);
            }

            Assert.Equal(["Stop automated routine?", "Stop automated routine?"], shell.Dialogs.Questions);
        }

        [Fact]
        public void ABusyScopeClearsAPreviousCancelRequest()
        {
            Shell shell = Shell.Create();
            shell.Session.Interaction.CancelRequested = true;

            using (shell.Session.BeginBatch())
                Assert.False(shell.Session.Interaction.CancelRequested);
        }
    }
}
