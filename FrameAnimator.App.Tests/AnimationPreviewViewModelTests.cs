namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using FrameAnimator.App.ViewModels;

    /// <summary>The animated preview: pacing by the project frame rate, looping, pausing, stepping, and following the project.</summary>
    public class AnimationPreviewViewModelTests
    {
        private static Shell OpenedWithTwoFrames(TempDir dir)
        {
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Preview.Open();
            return shell;
        }

        [Fact]
        public void NothingRunsUntilTheWindowOpens()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);

            Assert.False(shell.Ticker.IsRunning);

            shell.Preview.Open();
            Assert.True(shell.Ticker.IsRunning);

            shell.Preview.Close();
            Assert.False(shell.Ticker.IsRunning);
        }

        [Fact]
        public void AProjectWithoutARateRunsAtTheDefaultOne()
        {
            using TempDir dir = new();
            Shell shell = OpenedWithTwoFrames(dir);

            Assert.Equal(0, shell.Session.Project.Settings.Fps);
            Assert.Equal(AnimationPreviewViewModel.DefaultFps, shell.Preview.Fps);
            Assert.Equal(TimeSpan.FromMilliseconds(100), shell.Ticker.Interval);
        }

        [Theory]
        [InlineData(20, 50)]
        [InlineData(1, 1000)]
        public void TheIntervalFollowsTheFrameRateOfTheProject(Int32 fps, Int32 milliseconds)
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Session.Project.Settings.Fps = (UInt16)fps;

            shell.Preview.Open();

            Assert.Equal(fps, shell.Preview.Fps);
            Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), shell.Ticker.Interval);
        }

        [Fact]
        public void TheRateIsCappedSoTheTimerCanFollow()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Session.Project.Settings.Fps = 500;

            shell.Preview.Open();

            Assert.Equal(AnimationPreviewViewModel.MaxFps, shell.Preview.Fps);
        }

        [Fact]
        public void ChangingTheRateWhileRunningRetimesTheAnimation()
        {
            using TempDir dir = new();
            Shell shell = OpenedWithTwoFrames(dir);

            shell.Session.Project.Settings.Fps = 25;
            shell.Session.Refresh();

            Assert.Equal(TimeSpan.FromMilliseconds(40), shell.Ticker.Interval);
            Assert.Equal(1, shell.Ticker.Starts); // retimed, not restarted
        }

        [Fact]
        public void EachTickShowsTheNextFrameAndTheAnimationLoops()
        {
            using TempDir dir = new();
            Shell shell = OpenedWithTwoFrames(dir);
            Int32 redraws = 0;
            shell.Preview.RenderRequested += (_, _) => redraws++;

            Assert.Equal(0, shell.Preview.FrameIndex);
            shell.Ticker.Fire();
            Assert.Equal(1, shell.Preview.FrameIndex);
            shell.Ticker.Fire();
            Assert.Equal(0, shell.Preview.FrameIndex);

            Assert.Equal(2, redraws);
        }

        [Fact]
        public void OpeningStartsFromTheFirstFrame()
        {
            using TempDir dir = new();
            Shell shell = OpenedWithTwoFrames(dir);
            shell.Ticker.Fire();
            shell.Preview.Close();

            shell.Preview.Open();

            Assert.Equal(0, shell.Preview.FrameIndex);
            Assert.True(shell.Preview.IsPlaying);
        }

        [Fact]
        public void PausingStopsTheTimerAndResumingRestartsIt()
        {
            using TempDir dir = new();
            Shell shell = OpenedWithTwoFrames(dir);

            shell.Preview.PlayPauseCommand.Execute(null);
            Assert.False(shell.Preview.IsPlaying);
            Assert.False(shell.Ticker.IsRunning);
            Assert.Equal("Play", shell.Preview.PlayPauseToolTip);

            shell.Preview.PlayPauseCommand.Execute(null);
            Assert.True(shell.Preview.IsPlaying);
            Assert.True(shell.Ticker.IsRunning);
            Assert.Equal("Pause", shell.Preview.PlayPauseToolTip);
        }

        [Fact]
        public void SteppingPausesAndWrapsAround()
        {
            using TempDir dir = new();
            Shell shell = OpenedWithTwoFrames(dir);

            shell.Preview.StepBackCommand.Execute(null);

            Assert.False(shell.Preview.IsPlaying);
            Assert.Equal(1, shell.Preview.FrameIndex); // before the first one comes the last one

            shell.Preview.StepForwardCommand.Execute(null);
            Assert.Equal(0, shell.Preview.FrameIndex);
        }

        [Fact]
        public void ASingleFrameHasNothingToAnimate()
        {
            using TempDir dir = new();
            Shell shell = Shell.Create();
            shell.Dialogs.OpenFilesAnswer = (_, _, _) => [Images.WriteBmp(dir, "only.bmp")];
            shell.FrameList.AddFramesCommand.Execute(null);

            shell.Preview.Open();

            Assert.False(shell.Preview.CanAnimate);
            Assert.False(shell.Ticker.IsRunning);
            Assert.False(shell.Preview.PlayPauseCommand.CanExecute(null));
            Assert.False(shell.Preview.StepForwardCommand.CanExecute(null));
        }

        [Fact]
        public void AnEmptyDirectoryStaysIdleAndSaysSo()
        {
            Shell shell = Shell.Create();

            shell.Preview.Open();

            Assert.False(shell.Ticker.IsRunning);
            Assert.Equal("No frames", shell.Preview.Caption);
            shell.Ticker.Fire();
            Assert.Equal(0, shell.Preview.FrameIndex);
        }

        [Fact]
        public void ThePreviewFollowsTheCurrentDirection()
        {
            using TempDir dir = new();
            Shell shell = OpenedWithTwoFrames(dir);

            shell.FrameList.CurrentDirection = 3; // empty

            Assert.Equal(0, shell.Preview.FrameCount);
            Assert.False(shell.Ticker.IsRunning);

            shell.FrameList.CurrentDirection = 0;

            Assert.Equal(2, shell.Preview.FrameCount);
            Assert.True(shell.Ticker.IsRunning);
        }

        [Fact]
        public void RemovingFramesKeepsTheShownFrameInRange()
        {
            using TempDir dir = new();
            Shell shell = OpenedWithTwoFrames(dir);
            shell.Ticker.Fire(); // now on the second frame
            Assert.Equal(1, shell.Preview.FrameIndex);

            shell.FrameList.SetSelectedIndices([1]);
            shell.FrameList.RemoveFramesCommand.Execute(null);

            Assert.Equal(0, shell.Preview.FrameIndex);
            Assert.False(shell.Ticker.IsRunning); // one frame left
        }

        [Fact]
        public void AScriptWorkingOnTheProjectPausesThePreviewWithoutChangingIt()
        {
            using TempDir dir = new();
            Shell shell = OpenedWithTwoFrames(dir);

            using (shell.Session.BeginBusy())
            {
                Assert.True(shell.Preview.IsBusy);
                Assert.False(shell.Ticker.IsRunning);
                Assert.True(shell.Preview.IsPlaying);
            }

            Assert.True(shell.Ticker.IsRunning);
        }

        [Fact]
        public void TheCaptionTellsTheFrameAndTheRate()
        {
            using TempDir dir = new();
            Shell shell = OpenedWithTwoFrames(dir);
            List<String?> changed = [];
            shell.Preview.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            Assert.Equal("Frame 1/2 - 10 fps", shell.Preview.Caption);
            shell.Ticker.Fire();

            Assert.Equal("Frame 2/2 - 10 fps", shell.Preview.Caption);
            Assert.Contains(nameof(AnimationPreviewViewModel.Caption), changed);
        }

        [Fact]
        public void TheZoomIsClampedAndRedraws()
        {
            Shell shell = Shell.Create();
            Int32 redraws = 0;
            shell.Preview.RenderRequested += (_, _) => redraws++;

            shell.Preview.Zoom = 20;
            Assert.Equal(8, shell.Preview.Zoom);
            shell.Preview.Zoom = 0;
            Assert.Equal(1, shell.Preview.Zoom);

            Assert.True(redraws >= 2);
        }

        [Fact]
        public void TheMenuCommandAsksTheDialogServiceToToggleTheWindow()
        {
            Shell shell = Shell.Create();

            shell.Main.TogglePreviewCommand.Execute(null);
            shell.Main.TogglePreviewCommand.Execute(null);

            Assert.Equal(2, shell.Dialogs.PreviewToggles);
        }
    }
}
