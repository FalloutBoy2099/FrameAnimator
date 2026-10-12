namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using CommunityToolkit.Mvvm.Messaging;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Services;
    using FrameAnimator.App.ViewModels;

    /// <summary>The drawing area: zoom, view flags, tools, sample color and mouse actions.</summary>
    public class CanvasViewModelTests
    {
        [Fact]
        public void StartsWithTheBackgroundShownAndNothingElse()
        {
            Shell shell = Shell.Create();

            Assert.True(shell.Canvas.ShowBackground);
            Assert.False(shell.Canvas.ShowHexes);
            Assert.False(shell.Canvas.ShowGhost);
            Assert.Equal(EditTool.None, shell.Canvas.Tool);
            Assert.Equal(2, shell.Canvas.Zoom);
        }

        [Fact]
        public void ZoomIsClampedAndTheZoomCommandsFollow()
        {
            Shell shell = Shell.Create();

            for (Int32 i = 0; i < 20; i++)
                shell.Canvas.ZoomInCommand.Execute(null);
            Assert.Equal(8, shell.Canvas.Zoom);
            Assert.False(shell.Canvas.ZoomInCommand.CanExecute(null));
            Assert.Equal("800 %", shell.Canvas.ZoomText);
            Assert.Equal("800 %", shell.Status.ZoomText);

            shell.Canvas.ZoomResetCommand.Execute(null);
            Assert.Equal(1, shell.Canvas.Zoom);
            Assert.False(shell.Canvas.ZoomOutCommand.CanExecute(null));
            Assert.True(shell.Canvas.ZoomInCommand.CanExecute(null));
        }

        [Fact]
        public void ViewTogglesFlipTheirFlagAndRedraw()
        {
            Shell shell = Shell.Create();
            Int32 redraws = 0;
            shell.Canvas.RenderRequested += (_, _) => redraws++;

            shell.Canvas.ToggleHexesCommand.Execute(null);
            shell.Canvas.ToggleGhostCommand.Execute(null);
            shell.Canvas.ToggleBackgroundCommand.Execute(null);

            Assert.True(shell.Canvas.ShowHexes);
            Assert.True(shell.Canvas.ShowGhost);
            Assert.False(shell.Canvas.ShowBackground);
            Assert.Equal(3, redraws);
        }

        [Fact]
        public void ToolsAreExclusiveAndNotifyTheirFlags()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            List<String?> changed = [];
            shell.Canvas.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            shell.Canvas.ToggleMoveCommand.Execute(null);
            Assert.True(shell.Canvas.IsMoveTool);
            Assert.False(shell.Canvas.IsPickTool);

            shell.Canvas.TogglePickCommand.Execute(null);
            Assert.False(shell.Canvas.IsMoveTool);
            Assert.True(shell.Canvas.IsPickTool);
            Assert.Contains(nameof(CanvasViewModel.IsMoveTool), changed);
            Assert.Contains(nameof(CanvasViewModel.IsPickTool), changed);

            shell.Canvas.TogglePickCommand.Execute(null);
            Assert.Equal(EditTool.None, shell.Canvas.Tool);
        }

        [Fact]
        public void ToolsNeedFrames()
        {
            Shell shell = Shell.Create();

            Assert.False(shell.Canvas.ToggleMoveCommand.CanExecute(null));
            Assert.False(shell.Canvas.TogglePickCommand.CanExecute(null));
            Assert.False(shell.Canvas.PickSampleColorCommand.CanExecute(null));
        }

        [Fact]
        public void PickingASampleColorStoresIt()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Dialogs.ColorAnswer = _ => 0x123456;

            shell.Canvas.PickSampleColorCommand.Execute(null);

            Assert.Equal(0x123456, shell.Session.Project.Settings.SampleColor);
            Assert.Contains("123456", shell.Canvas.SampleToolTip);
        }

        [Fact]
        public void TheSampleColorIsReadFromTheFrameWithThePickTool()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Canvas.TogglePickCommand.Execute(null);
            (Int32 x, Int32 y) = FrameAnimator.Core.Geometry.FrameMath.DisplayPosition(shell.Session.CurrentDir, 0, 0, 0);

            shell.Canvas.CanvasMouseDown(x + 4, y + 4); // inside the colored square of the test image

            Assert.Equal(0xE0A040, shell.Session.Project.Settings.SampleColor);
        }

        [Fact]
        public void DraggingWithTheMoveToolShiftsTheDirectionAndFillsTheUndoStack()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Canvas.ToggleMoveCommand.Execute(null);
            Assert.False(shell.Main.UndoCommand.CanExecute(null));

            shell.Canvas.CanvasMouseDown(0, 0);
            shell.Canvas.CanvasMouseMove(4, 2, shift: false, ctrl: false);
            shell.Canvas.CanvasMouseUp();

            Assert.Equal((4, 2), (shell.Session.CurrentDir.ShiftBaseX, shell.Session.CurrentDir.ShiftBaseY));
            Assert.True(shell.Main.UndoCommand.CanExecute(null));

            shell.Canvas.CanvasMouseMove(9, 9, shift: false, ctrl: false); // after mouse up: ignored
            Assert.Equal((4, 2), (shell.Session.CurrentDir.ShiftBaseX, shell.Session.CurrentDir.ShiftBaseY));
        }

        [Fact]
        public void ChangingTheDirectionCancelsADrag()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            shell.Canvas.ToggleMoveCommand.Execute(null);
            shell.Canvas.CanvasMouseDown(0, 0);

            shell.FrameList.CurrentDirection = 1;
            shell.FrameList.CurrentDirection = 0;
            shell.Canvas.CanvasMouseMove(5, 5, shift: false, ctrl: false);

            Assert.Equal((0, 0), (shell.Session.CurrentDir.ShiftBaseX, shell.Session.CurrentDir.ShiftBaseY));
        }

        [Fact]
        public void TheSelectionRedrawsTheDrawingArea()
        {
            using TempDir dir = new();
            Shell shell = Shell.WithTwoFrames(dir);
            Int32 redraws = 0;
            shell.Canvas.RenderRequested += (_, _) => redraws++;

            shell.FrameList.SelectedFrameIndex = 1;

            Assert.Equal(1, redraws);
        }

        [Fact]
        public void TheLanguageMessageRefreshesTheSampleTexts()
        {
            Shell shell = Shell.Create();
            List<String?> changed = [];
            shell.Canvas.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            WeakReferenceMessenger.Default.Send(new LanguageChangedMessage("en"));

            Assert.Contains(nameof(CanvasViewModel.SampleToolTip), changed);
            Assert.Contains(nameof(CanvasViewModel.SampleText), changed);
        }

        [Fact]
        public void TheThemeMessageRedrawsTheDrawingArea()
        {
            Shell shell = Shell.Create();
            Int32 redraws = 0;
            shell.Canvas.RenderRequested += (_, _) => redraws++;

            WeakReferenceMessenger.Default.Send(new ThemeChangedMessage(ThemePreference.Dark));

            Assert.True(redraws >= 1); // the messenger is shared: other tests may announce too
        }

        [Fact]
        public void RaisesPropertyChangedThroughTheToolkit()
        {
            Shell shell = Shell.Create();
            List<String?> changed = [];
            ((INotifyPropertyChanged)shell.Canvas).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            shell.Canvas.Zoom = 4;

            Assert.Contains(nameof(CanvasViewModel.Zoom), changed);
            Assert.Contains(nameof(CanvasViewModel.ZoomText), changed);
        }
    }
}
