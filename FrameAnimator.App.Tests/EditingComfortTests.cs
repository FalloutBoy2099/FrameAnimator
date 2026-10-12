namespace FrameAnimator.App.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using FrameAnimator.App.ViewModels;
    using FrameAnimator.Core.Model;

    /// <summary>Unsaved changes, recent projects, dropped files, arrow-key nudging and the status hint.</summary>
    public class EditingComfortTests
    {
        private static Shell SavedProject(TempDir dir, out String project)
        {
            Shell shell = Shell.WithTwoFrames(dir);
            String target = dir.File("p.fpr");
            project = target;
            shell.Dialogs.SaveFileAnswer = (_, _, _, _, _) => target;
            shell.Main.SaveAsCommand.Execute(null);
            shell.Dialogs.Questions.Clear();
            return shell;
        }

        // ------------------------------------------------------------------ unsaved changes

        [Fact]
        public void ATouchedProjectIsStarredInTheTitleUntilItIsSaved()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            Assert.StartsWith("*", shell.Main.Title);

            shell.Dialogs.SaveFileAnswer = (_, _, _, _, _) => dir.File("p.fpr");
            shell.Main.SaveAsCommand.Execute(null);

            Assert.Equal("p.fpr — Frame Animator", shell.Main.Title);
        }

        [Fact]
        public void ADragStarsTheTitleAndUndoClearsIt()
        {
            using TempDir dir = new();
            Shell shell = SavedProject(dir, out _);
            shell.Canvas.ToggleMoveCommand.Execute(null);
            Assert.DoesNotContain("*", shell.Main.Title);

            shell.Canvas.CanvasMouseDown(0, 0);
            shell.Canvas.CanvasMouseMove(3, 3, shift: false, ctrl: false);
            shell.Canvas.CanvasMouseUp();
            Assert.StartsWith("*", shell.Main.Title);

            shell.Main.UndoCommand.Execute(null);
            Assert.DoesNotContain("*", shell.Main.Title);
        }

        [Fact]
        public void ACleanProjectIsReplacedWithoutAsking()
        {
            using TempDir dir = new();
            Shell shell = SavedProject(dir, out _);

            shell.Main.NewCommand.Execute(null);

            Assert.Empty(shell.Dialogs.Questions);
            Assert.Empty(shell.FrameList.Frames);
        }

        [Fact]
        public void ModifiedProjectAsksAndCancelKeepsEverything()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Dialogs.YesNoCancelAnswer = MessageBoxResult.Cancel;

            shell.Main.NewCommand.Execute(null);

            Assert.Equal(["Save changes to 'Untitled.fpr'?"], shell.Dialogs.Questions);
            Assert.Equal(2, shell.FrameList.Frames.Count);
        }

        [Fact]
        public void AnsweringNoDiscardsTheChanges()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Dialogs.YesNoCancelAnswer = MessageBoxResult.No;

            shell.Main.NewCommand.Execute(null);

            Assert.Empty(shell.FrameList.Frames);
        }

        [Fact]
        public void AnsweringYesSavesThenContinues()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            String target = dir.File("keep.fpr");
            shell.Dialogs.YesNoCancelAnswer = MessageBoxResult.Yes;
            shell.Dialogs.SaveFileAnswer = (_, _, _, _, _) => target;

            shell.Main.NewCommand.Execute(null);

            Assert.True(File.Exists(target));
            Assert.Empty(shell.FrameList.Frames);
        }

        [Fact]
        public void CancellingTheSaveThatWasAskedForKeepsTheProject()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Dialogs.YesNoCancelAnswer = MessageBoxResult.Yes;
            shell.Dialogs.SaveFileAnswer = (_, _, _, _, _) => null; // the user closes the "save as" dialog

            shell.Main.NewCommand.Execute(null);

            Assert.Equal(2, shell.FrameList.Frames.Count);
        }

        [Fact]
        public void OpeningAProjectAlsoAsksFirst()
        {
            using TempDir dir = new();
            Shell shell = SavedProject(dir, out String project);
            shell.Canvas.ToggleMoveCommand.Execute(null);
            shell.Canvas.Nudge(2, 2, propagate: false); // an edit
            shell.Dialogs.YesNoCancelAnswer = MessageBoxResult.Cancel;
            shell.Dialogs.OpenFilesAnswer = (_, _, _) => [project];

            shell.Main.OpenCommand.Execute(null);

            Assert.Single(shell.Dialogs.Questions);
            Assert.Equal((2, 2), (shell.Session.CurrentDir.ShiftBaseX, shell.Session.CurrentDir.ShiftBaseY)); // not reloaded
        }

        [Fact]
        public void TheWindowMayCloseWhenNothingIsUnsaved()
        {
            using TempDir dir = new();
            Shell shell = SavedProject(dir, out _);

            Assert.True(shell.Main.ConfirmClose());
            Assert.Empty(shell.Dialogs.Questions);
        }

        [Fact]
        public void TheWindowAsksBeforeClosingOnUnsavedChanges()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);

            shell.Dialogs.YesNoCancelAnswer = MessageBoxResult.Cancel;
            Assert.False(shell.Main.ConfirmClose());

            shell.Dialogs.YesNoCancelAnswer = MessageBoxResult.No;
            Assert.True(shell.Main.ConfirmClose());
        }

        // ------------------------------------------------------------------ recent projects

        [Fact]
        public void SavingAndOpeningFeedTheRecentList()
        {
            using TempDir dir = new();
            Shell shell = SavedProject(dir, out String project);

            Assert.Equal(project, shell.Preferences.RecentFiles.Single());
            Assert.Equal(["p.fpr", "Clear list"], shell.Main.RecentFiles.Select(item => item.Header));
            Assert.True(shell.Main.AnyRecentFiles);
        }

        [Fact]
        public void TheMostRecentProjectComesFirstWithoutDuplicates()
        {
            using TempDir dir = new();
            Shell shell = SavedProject(dir, out String first);
            String second = dir.File("second.fpr");
            shell.Dialogs.SaveFileAnswer = (_, _, _, _, _) => second;
            shell.Main.SaveAsCommand.Execute(null);

            shell.Dialogs.OpenFilesAnswer = (_, _, _) => [first];
            shell.Main.OpenCommand.Execute(null);

            Assert.Equal([first, second], shell.Preferences.RecentFiles);
        }

        [Fact]
        public void TheListIsLimited()
        {
            Shell shell = Shell.Create();
            for (Int32 i = 0; i < ProjectPreferencesLimit + 3; i++)
                shell.Preferences.AddRecentFile($"C:\\x\\project{i}.fpr");

            Assert.Equal(Services.ProjectPreferences.MaxRecentFiles, shell.Preferences.RecentFiles.Count);
            Assert.Equal($"C:\\x\\project{ProjectPreferencesLimit + 2}.fpr", shell.Preferences.RecentFiles[0]);
        }

        private const Int32 ProjectPreferencesLimit = Services.ProjectPreferences.MaxRecentFiles;

        [Fact]
        public void PickingARecentProjectOpensIt()
        {
            using TempDir dir = new();
            Shell source = SavedProject(dir, out String project);
            Shell shell = Shell.Create();
            shell.Preferences.AddRecentFile(project);

            shell.Main.OpenRecentCommand.Execute(project);

            Assert.Equal(2, shell.FrameList.Frames.Count);
            Assert.Equal("p.fpr — Frame Animator", shell.Main.Title);
        }

        [Fact]
        public void AMissingRecentProjectIsReportedAndForgotten()
        {
            using TempDir dir = new();
            Shell shell = Shell.Create();
            String gone = dir.File("gone.fpr");
            shell.Preferences.AddRecentFile(gone);

            shell.Main.OpenRecentCommand.Execute(gone);

            Assert.Single(shell.Dialogs.Warnings);
            Assert.Contains("gone.fpr", shell.Dialogs.Warnings[0]);
            Assert.Empty(shell.Preferences.RecentFiles);
        }

        [Fact]
        public void ClearingTheListEmptiesTheMenu()
        {
            using TempDir dir = new();
            Shell shell = SavedProject(dir, out _);

            shell.Main.ClearRecentFilesCommand.Execute(null);

            Assert.Empty(shell.Preferences.RecentFiles);
            Assert.Empty(shell.Main.RecentFiles);
            Assert.False(shell.Main.AnyRecentFiles);
            Assert.False(shell.Main.ClearRecentFilesCommand.CanExecute(null));
        }

        // ------------------------------------------------------------------ dropped files

        [Fact]
        public void ADroppedProjectIsOpened()
        {
            using TempDir dir = new();
            Shell source = SavedProject(dir, out String project);
            Shell shell = Shell.Create();

            shell.Main.DropFiles([project]);

            Assert.Equal(2, shell.FrameList.Frames.Count);
        }

        [Fact]
        public void DroppedImagesBecomeFramesInNameOrderNumbersByValue()
        {
            using TempDir dir = new();
            Shell shell = Shell.Create();
            String f10 = Images.WriteBmp(dir, "frame10.bmp");
            String f2 = Images.WriteBmp(dir, "frame2.bmp");
            String f1 = Images.WriteBmp(dir, "Frame1.bmp");

            shell.Main.DropFiles([f10, f2, f1]);

            Assert.Equal(["Frame1.bmp", "frame2.bmp", "frame10.bmp"], shell.FrameList.Frames);
        }

        [Fact]
        public void ADroppedPaletteIsLoaded()
        {
            using TempDir dir = new();
            Shell shell = Shell.Create();
            String palette = dir.File("custom.act");
            File.WriteAllBytes(palette, new Byte[768]);

            shell.Main.DropFiles([palette]);

            Assert.Equal(palette, shell.Session.Project.PaletteFile);
        }

        [Fact]
        public void AnUnknownDroppedFileIsReported()
        {
            using TempDir dir = new();
            Shell shell = Shell.Create();
            String text = dir.File("notes.txt");
            File.WriteAllText(text, "x");

            shell.Main.DropFiles([text]);

            Assert.Contains(".txt", shell.Dialogs.Warnings.Single());
        }

        [Fact]
        public void NothingIsAcceptedWhileAJobRuns()
        {
            using TempDir dir = new();
            Shell shell = Shell.Create();
            String image = Images.WriteBmp(dir, "a.bmp");

            Assert.True(shell.Main.CanDrop([image]));
            using (shell.Session.BeginBatch())
            {
                Assert.False(shell.Main.CanDrop([image]));
                shell.Main.DropFiles([image]);
            }

            Assert.Empty(shell.FrameList.Frames);
            Assert.False(shell.Main.CanDrop([Path.Combine(dir.Path, "x.txt")]));
        }

        // ------------------------------------------------------------------ arrow keys

        [Fact]
        public void ArrowKeysNeedTheMoveTool()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);

            Assert.False(shell.Canvas.Nudge(1, 0, propagate: false));
            Assert.Equal(0, shell.Session.CurrentDir.ShiftBaseX);
        }

        [Fact]
        public void EachArrowPressMovesTheDirectionAndIsItsOwnUndoStep()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Canvas.ToggleMoveCommand.Execute(null);

            Assert.True(shell.Canvas.Nudge(1, 0, propagate: false));
            Assert.True(shell.Canvas.Nudge(0, -1, propagate: false));
            Assert.Equal((1, -1), (shell.Session.CurrentDir.ShiftBaseX, shell.Session.CurrentDir.ShiftBaseY));

            shell.Main.UndoCommand.Execute(null);
            Assert.Equal((1, 0), (shell.Session.CurrentDir.ShiftBaseX, shell.Session.CurrentDir.ShiftBaseY));
            shell.Main.UndoCommand.Execute(null);
            Assert.Equal((0, 0), (shell.Session.CurrentDir.ShiftBaseX, shell.Session.CurrentDir.ShiftBaseY));
        }

        [Fact]
        public void ArrowKeysMoveTheSelectedFrameWhenOffsetsAreUnlocked()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Session.Project.Settings.LockOffsets = false;
            shell.FrameList.SelectedFrameIndex = 1;
            shell.Canvas.ToggleMoveCommand.Execute(null);

            shell.Canvas.Nudge(3, 2, propagate: false);

            Assert.Equal((3, 2), shell.Session.Editor.GetFrameOffset(0, 1));
        }

        [Fact]
        public void ALockedFrameDoesNotMove()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.FrameList.SelectedFrameIndex = 1;
            shell.Canvas.ToggleMoveCommand.Execute(null);
            Assert.True(shell.Session.Project.Settings.LockOffsets);
            (Int32 x, Int32 y) = shell.Session.Editor.GetFrameOffset(0, 1);

            shell.Canvas.Nudge(3, 2, propagate: false);

            Assert.Equal((x, y), shell.Session.Editor.GetFrameOffset(0, 1));
            Assert.False(shell.Main.UndoCommand.CanExecute(null));
        }

        [Fact]
        public void ANudgeRedrawsTheDrawingArea()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Canvas.ToggleMoveCommand.Execute(null);
            Int32 redraws = 0;
            shell.Canvas.RenderRequested += (_, _) => redraws++;

            shell.Canvas.Nudge(1, 1, propagate: false);

            Assert.Equal(1, redraws);
        }

        // ------------------------------------------------------------------ status hint

        [Fact]
        public void TheStatusBarExplainsTheActiveTool()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            Assert.Equal("", shell.Status.StatusHint);

            shell.Canvas.ToggleMoveCommand.Execute(null);
            Assert.Contains("arrow keys", shell.Status.StatusHint);

            shell.Canvas.TogglePickCommand.Execute(null);
            Assert.Contains("pixel", shell.Status.StatusHint);

            shell.Canvas.TogglePickCommand.Execute(null);
            Assert.Equal("", shell.Status.StatusHint);
        }

        [Fact]
        public void TheHintMakesRoomForTheProgressOfAJob()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Canvas.ToggleMoveCommand.Execute(null);
            Assert.NotEqual("", shell.Status.StatusHint);

            shell.Session.Interaction.Progress(1, 5);
            Assert.Equal("", shell.Status.StatusHint);

            shell.Session.Interaction.Progress(0, 0);
            Assert.NotEqual("", shell.Status.StatusHint);
        }
    }
}
