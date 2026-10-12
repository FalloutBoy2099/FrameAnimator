namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using FrameAnimator.App.Infrastructure;

    /// <summary>Directions and frames: the list, its selection, the enabling rules and the frame commands.</summary>
    public class FrameListViewModelTests
    {
        [Fact]
        public void StartsWithAnEmptyList()
        {
            Shell shell = Shell.Create();

            Assert.Empty(shell.FrameList.Frames);
            Assert.False(shell.Session.HasFrames);
            Assert.Equal(-1, shell.FrameList.SelectedFrameIndex);
            Assert.Equal(6, shell.FrameList.DirectionNames.Count);
        }

        [Fact]
        public void EnablingRulesFollowTheContent()
        {
            using TempDir dir = new();
            Shell shell = Shell.Create();

            Assert.True(shell.FrameList.AddFramesCommand.CanExecute(null));
            Assert.False(shell.FrameList.FramesOffsetCommand.CanExecute(null));
            Assert.False(shell.FrameList.FrameUpCommand.CanExecute(null));
            Assert.False(shell.FrameList.FrameDownCommand.CanExecute(null));
            Assert.False(shell.FrameList.RemoveFramesCommand.CanExecute(null));

            shell.Dialogs.OpenFilesAnswer = (_, _, _) => [Images.WriteBmp(dir, "a.bmp"), Images.WriteBmp(dir, "b.bmp")];
            shell.FrameList.AddFramesCommand.Execute(null);

            Assert.Equal(2, shell.FrameList.Frames.Count);
            Assert.Equal(0, shell.FrameList.SelectedFrameIndex);
            Assert.True(shell.FrameList.FramesOffsetCommand.CanExecute(null));
            Assert.True(shell.FrameList.FrameOffsetCommand.CanExecute(null));
            Assert.True(shell.FrameList.RemoveFramesCommand.CanExecute(null));
            Assert.False(shell.FrameList.FrameUpCommand.CanExecute(null));
            Assert.True(shell.FrameList.FrameDownCommand.CanExecute(null));
        }

        [Fact]
        public void ChangingTheSelectionUpdatesTheCommandsAndRaisesCanExecuteChanged()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);

            Int32 raised = 0;
            shell.FrameList.FrameDownCommand.CanExecuteChanged += (_, _) => raised++;
            shell.FrameList.SelectedFrameIndex = 1;

            Assert.True(raised > 0);
            Assert.False(shell.FrameList.FrameDownCommand.CanExecute(null));
            Assert.True(shell.FrameList.FrameUpCommand.CanExecute(null));
            Assert.Equal("Frame 2/2", shell.Status.StatusFrame);
        }

        [Fact]
        public void TheSelectionChangedEventAnnouncesTheNewCurrentFrame()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            Int32 raised = 0;
            shell.FrameList.SelectionChanged += (_, _) => raised++;

            shell.FrameList.SelectedFrameIndex = 1;
            shell.FrameList.SelectedFrameIndex = 1; // unchanged: no event

            Assert.Equal(1, raised);
        }

        [Fact]
        public void FrameOffsetOfAFollowingFrameIsBlockedByLockOffsets()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);

            shell.FrameList.SelectedFrameIndex = 1;
            Assert.True(shell.Session.Project.Settings.LockOffsets);
            Assert.False(shell.FrameList.FrameOffsetCommand.CanExecute(null));

            shell.Session.Project.Settings.LockOffsets = false;
            shell.Session.Refresh();
            Assert.True(shell.FrameList.FrameOffsetCommand.CanExecute(null));
        }

        [Fact]
        public void FrameDownAndFrameUpMoveTheFrameAndKeepItSelected()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            String first = shell.FrameList.Frames[0];

            shell.FrameList.FrameDownCommand.Execute(null);
            Assert.Equal(first, shell.FrameList.Frames[1]);
            Assert.Equal(1, shell.FrameList.SelectedFrameIndex);

            shell.FrameList.FrameUpCommand.Execute(null);
            Assert.Equal(first, shell.FrameList.Frames[0]);
            Assert.Equal(0, shell.FrameList.SelectedFrameIndex);
        }

        [Fact]
        public void RemoveFramesDeletesTheSelectedFrames()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            String second = shell.FrameList.Frames[1];

            shell.FrameList.SetSelectedIndices([0]);
            shell.FrameList.RemoveFramesCommand.Execute(null);

            Assert.Single(shell.FrameList.Frames);
            Assert.Equal(second, shell.FrameList.Frames[0]);
        }

        [Fact]
        public void FramesOffsetAsksForTheValuesAndStoresThem()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Dialogs.OffsetAnswer = _ => new OffsetDialogResult(5, -3, false);

            shell.FrameList.FramesOffsetCommand.Execute(null);

            Assert.Equal("Frames Offset", shell.Dialogs.OffsetRequests.Single().Title);
            Assert.Equal((5, -3), shell.Session.Editor.GetFramesOffset(0));
            Assert.True(shell.Main.UndoCommand.CanExecute(null));

            shell.Main.UndoCommand.Execute(null);
            Assert.False(shell.Main.UndoCommand.CanExecute(null));
        }

        [Fact]
        public void FirstFrameOffsetDialogEnablesTheWizardOnlyForTheFirstFrame()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Session.Project.Settings.LockOffsets = false;

            shell.FrameList.FrameOffsetCommand.Execute(null);
            shell.FrameList.SelectedFrameIndex = 1;
            shell.FrameList.FrameOffsetCommand.Execute(null);

            OffsetDialogRequest first = shell.Dialogs.OffsetRequests[0];
            OffsetDialogRequest following = shell.Dialogs.OffsetRequests[1];
            Assert.Equal("First Frame Offset", first.Title);
            Assert.True(first.WizardEnabled);
            Assert.False(first.InheritEnabled);
            Assert.Equal("Frame Offset", following.Title);
            Assert.False(following.WizardEnabled);
        }

        [Fact]
        public void ChangingTheDirectionShowsItsFrames()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);

            shell.FrameList.CurrentDirection = 3;

            Assert.Empty(shell.FrameList.Frames);
            Assert.Equal(3, shell.FrameList.CurrentDirection);
            Assert.False(shell.FrameList.FramesOffsetCommand.CanExecute(null));

            shell.FrameList.CurrentDirection = 0;
            Assert.Equal(2, shell.FrameList.Frames.Count);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(6)]
        public void AnInvalidDirectionIsIgnored(Int32 direction)
        {
            Shell shell = Shell.Create();

            shell.FrameList.CurrentDirection = direction;

            Assert.Equal(0, shell.FrameList.CurrentDirection);
        }

        [Fact]
        public void FullNamesAreShownOnRequestAndRaiseThePropertyChange()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            List<String?> changed = [];
            ((INotifyPropertyChanged)shell.FrameList).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            shell.FrameList.ShowFullNames = true;

            Assert.Contains(nameof(shell.FrameList.ShowFullNames), changed);
            Assert.Equal(dir.File("a.bmp"), shell.FrameList.Frames[0]);
        }

        [Fact]
        public void FilesAddedByHandJoinTheCurrentDirection()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);

            shell.FrameList.AddFrameFiles([Images.WriteBmp(dir, "c.bmp")]);

            Assert.Equal(3, shell.FrameList.Frames.Count);
            Assert.False(shell.Main.UndoCommand.CanExecute(null)); // adding frames clears the undo stack, like the original
        }

        [Fact]
        public void AnUnreadableFileAsksWhetherToContinue()
        {
            using TempDir dir = new();
            Shell shell = Shell.Create();
            shell.Dialogs.YesNoAnswer = false;
            String bad = dir.File("bad.bmp");
            System.IO.File.WriteAllText(bad, "not an image");

            shell.FrameList.AddFrameFiles([bad, Images.WriteBmp(dir, "good.bmp")]);

            Assert.Contains("Continue loading frames?", shell.Dialogs.Questions);
            Assert.Empty(shell.FrameList.Frames);
        }
    }
}
