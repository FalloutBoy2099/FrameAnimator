namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Windows.Threading;
    using CommunityToolkit.Mvvm.Messaging;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.ViewModels;
    using FrameAnimator.App.Views;
    using FrameAnimator.Core.Model;

    /// <summary>View model of the main window: commands (CommunityToolkit.Mvvm), enabling rules, state and messages.</summary>
    public class MainViewModelTests
    {
        private static MainViewModel Create(FakeDialogService dialogs) => new(Dispatcher.CurrentDispatcher, dialogs, "");

        /// <summary>A view model that already holds two frames in the current direction.</summary>
        private static MainViewModel WithTwoFrames(TempDir dir, FakeDialogService dialogs)
        {
            dialogs.OpenFilesAnswer = (_, _, _) => [Images.WriteBmp(dir, "a.bmp"), Images.WriteBmp(dir, "b.bmp")];
            MainViewModel viewModel = Create(dialogs);
            viewModel.AddFramesCommand.Execute(null);
            return viewModel;
        }

        [Fact]
        public void StartsWithAnEmptyProject()
        {
            MainViewModel viewModel = Create(new FakeDialogService());

            Assert.Empty(viewModel.Frames);
            Assert.False(viewModel.HasFrames);
            Assert.Equal(-1, viewModel.SelectedFrameIndex);
            Assert.Equal("Untitled.fpr — Frame Animator", viewModel.Title);
            Assert.Equal(6, viewModel.DirectionNames.Count);
            Assert.False(viewModel.ShowHexes);
            Assert.True(viewModel.ShowBackground);
        }

        [Fact]
        public void EnablingRulesFollowTheContent()
        {
            using TempDir dir = new();
            FakeDialogService dialogs = new();
            MainViewModel viewModel = Create(dialogs);

            Assert.True(viewModel.NewCommand.CanExecute(null));
            Assert.True(viewModel.AddFramesCommand.CanExecute(null));
            Assert.False(viewModel.FramesOffsetCommand.CanExecute(null));
            Assert.False(viewModel.FrameUpCommand.CanExecute(null));
            Assert.False(viewModel.FrameDownCommand.CanExecute(null));
            Assert.False(viewModel.RemoveFramesCommand.CanExecute(null));
            Assert.False(viewModel.CreateFrmCommand.CanExecute(null));
            Assert.False(viewModel.UndoCommand.CanExecute(null));
            Assert.False(viewModel.CancelCommand.CanExecute(null));

            dialogs.OpenFilesAnswer = (_, _, _) => [Images.WriteBmp(dir, "a.bmp"), Images.WriteBmp(dir, "b.bmp")];
            viewModel.AddFramesCommand.Execute(null);

            Assert.Equal(2, viewModel.Frames.Count);
            Assert.Equal(0, viewModel.SelectedFrameIndex);
            Assert.True(viewModel.FramesOffsetCommand.CanExecute(null));
            Assert.True(viewModel.FrameOffsetCommand.CanExecute(null));
            Assert.True(viewModel.RemoveFramesCommand.CanExecute(null));
            Assert.False(viewModel.FrameUpCommand.CanExecute(null));
            Assert.True(viewModel.FrameDownCommand.CanExecute(null));
        }

        [Fact]
        public void ChangingTheSelectionUpdatesTheCommandsAndRaisesCanExecuteChanged()
        {
            using TempDir dir = new();
            MainViewModel viewModel = WithTwoFrames(dir, new FakeDialogService());

            Int32 raised = 0;
            viewModel.FrameDownCommand.CanExecuteChanged += (_, _) => raised++;
            viewModel.SelectedFrameIndex = 1;

            Assert.True(raised > 0);
            Assert.False(viewModel.FrameDownCommand.CanExecute(null));
            Assert.True(viewModel.FrameUpCommand.CanExecute(null));
            Assert.Equal("Frame 2/2", viewModel.StatusFrame);
        }

        [Fact]
        public void FrameOffsetOfAFollowingFrameIsBlockedByLockOffsets()
        {
            using TempDir dir = new();
            MainViewModel viewModel = WithTwoFrames(dir, new FakeDialogService());

            viewModel.SelectedFrameIndex = 1;
            Assert.True(viewModel.Project.Settings.LockOffsets);
            Assert.False(viewModel.FrameOffsetCommand.CanExecute(null));

            viewModel.Project.Settings.LockOffsets = false;
            viewModel.RefreshAll();
            Assert.True(viewModel.FrameOffsetCommand.CanExecute(null));
        }

        [Fact]
        public void FrameDownAndFrameUpMoveTheFrameAndKeepItSelected()
        {
            using TempDir dir = new();
            MainViewModel viewModel = WithTwoFrames(dir, new FakeDialogService());
            String first = viewModel.Frames[0];

            viewModel.FrameDownCommand.Execute(null);
            Assert.Equal(first, viewModel.Frames[1]);
            Assert.Equal(1, viewModel.SelectedFrameIndex);

            viewModel.FrameUpCommand.Execute(null);
            Assert.Equal(first, viewModel.Frames[0]);
            Assert.Equal(0, viewModel.SelectedFrameIndex);
        }

        [Fact]
        public void RemoveFramesDeletesTheSelectedFrames()
        {
            using TempDir dir = new();
            MainViewModel viewModel = WithTwoFrames(dir, new FakeDialogService());
            String second = viewModel.Frames[1];

            viewModel.SetSelectedIndices([0]);
            viewModel.RemoveFramesCommand.Execute(null);

            Assert.Single(viewModel.Frames);
            Assert.Equal(second, viewModel.Frames[0]);
        }

        [Fact]
        public void FramesOffsetAsksForTheValuesAndStoresThem()
        {
            using TempDir dir = new();
            FakeDialogService dialogs = new();
            MainViewModel viewModel = WithTwoFrames(dir, dialogs);
            dialogs.OffsetAnswer = _ => new OffsetDialogResult(5, -3, false);

            viewModel.FramesOffsetCommand.Execute(null);

            Assert.Equal("Frames Offset", dialogs.OffsetRequests.Single().Title);
            Assert.Equal((5, -3), viewModel.Editor.GetFramesOffset(0));
            Assert.True(viewModel.UndoCommand.CanExecute(null));

            viewModel.UndoCommand.Execute(null);
            Assert.False(viewModel.UndoCommand.CanExecute(null));
        }

        [Fact]
        public void FirstFrameOffsetDialogEnablesTheWizardOnlyForTheFirstFrame()
        {
            using TempDir dir = new();
            FakeDialogService dialogs = new();
            MainViewModel viewModel = WithTwoFrames(dir, dialogs);
            viewModel.Project.Settings.LockOffsets = false;

            viewModel.FrameOffsetCommand.Execute(null);
            viewModel.SelectedFrameIndex = 1;
            viewModel.FrameOffsetCommand.Execute(null);

            OffsetDialogRequest first = dialogs.OffsetRequests[0];
            OffsetDialogRequest following = dialogs.OffsetRequests[1];
            Assert.Equal("First Frame Offset", first.Title);
            Assert.True(first.WizardEnabled);
            Assert.False(first.InheritEnabled);
            Assert.Equal("Frame Offset", following.Title);
            Assert.False(following.WizardEnabled);
        }

        [Fact]
        public void OptionsCommandPassesTheSettingsAndTheFrameCount()
        {
            using TempDir dir = new();
            FakeDialogService dialogs = new();
            MainViewModel viewModel = WithTwoFrames(dir, dialogs);
            ProjectSettings? seen = null;
            Int32 count = -1;
            dialogs.OptionsAnswer = (settings, frames) =>
            {
                seen = settings;
                count = frames;
                settings.AutoCutting = false;
                return true;
            };

            viewModel.OptionsCommand.Execute(null);

            Assert.Same(viewModel.Project.Settings, seen);
            Assert.Equal(2, count);
            Assert.Equal("", viewModel.StatusAutoCutting);
        }

        [Fact]
        public void ZoomIsClampedAndTheZoomCommandsFollow()
        {
            MainViewModel viewModel = Create(new FakeDialogService());
            Assert.Equal(2, viewModel.Zoom);

            for (Int32 i = 0; i < 20; i++)
                viewModel.ZoomInCommand.Execute(null);
            Assert.Equal(8, viewModel.Zoom);
            Assert.False(viewModel.ZoomInCommand.CanExecute(null));
            Assert.Equal("800 %", viewModel.ZoomText);

            viewModel.ZoomResetCommand.Execute(null);
            Assert.Equal(1, viewModel.Zoom);
            Assert.False(viewModel.ZoomOutCommand.CanExecute(null));
            Assert.True(viewModel.ZoomInCommand.CanExecute(null));
        }

        [Fact]
        public void ViewTogglesFlipTheirFlagAndRedraw()
        {
            MainViewModel viewModel = Create(new FakeDialogService());
            Int32 redraws = 0;
            viewModel.RenderRequested += (_, _) => redraws++;

            viewModel.ToggleHexesCommand.Execute(null);
            viewModel.ToggleGhostCommand.Execute(null);
            viewModel.ToggleBackgroundCommand.Execute(null);

            Assert.True(viewModel.ShowHexes);
            Assert.True(viewModel.ShowGhost);
            Assert.False(viewModel.ShowBackground);
            Assert.Equal(3, redraws);
        }

        [Fact]
        public void ToolsAreExclusiveAndNotifyTheirFlags()
        {
            using TempDir dir = new();
            MainViewModel viewModel = WithTwoFrames(dir, new FakeDialogService());
            List<String?> changed = [];
            viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            viewModel.ToggleMoveCommand.Execute(null);
            Assert.True(viewModel.IsMoveTool);
            Assert.False(viewModel.IsPickTool);

            viewModel.TogglePickCommand.Execute(null);
            Assert.False(viewModel.IsMoveTool);
            Assert.True(viewModel.IsPickTool);
            Assert.Contains(nameof(MainViewModel.IsMoveTool), changed);
            Assert.Contains(nameof(MainViewModel.IsPickTool), changed);

            viewModel.TogglePickCommand.Execute(null);
            Assert.Equal(EditTool.None, viewModel.Tool);
        }

        [Fact]
        public void PickingASampleColorStoresIt()
        {
            using TempDir dir = new();
            FakeDialogService dialogs = new();
            MainViewModel viewModel = WithTwoFrames(dir, dialogs);
            dialogs.ColorAnswer = _ => 0x123456;

            viewModel.PickSampleColorCommand.Execute(null);

            Assert.Equal(0x123456, viewModel.Project.Settings.SampleColor);
            Assert.Contains("123456", viewModel.SampleToolTip);
        }

        [Fact]
        public void DraggingWithTheMoveToolShiftsTheDirectionAndFillsTheUndoStack()
        {
            using TempDir dir = new();
            MainViewModel viewModel = WithTwoFrames(dir, new FakeDialogService());
            viewModel.ToggleMoveCommand.Execute(null);
            Assert.False(viewModel.UndoCommand.CanExecute(null));

            viewModel.CanvasMouseDown(0, 0);
            viewModel.CanvasMouseMove(4, 2, shift: false, ctrl: false);
            viewModel.CanvasMouseUp();

            Assert.Equal((4, 2), (viewModel.CurrentDir.ShiftBaseX, viewModel.CurrentDir.ShiftBaseY));
            Assert.True(viewModel.UndoCommand.CanExecute(null));

            viewModel.CanvasMouseMove(9, 9, shift: false, ctrl: false); // after mouse up: ignored
            Assert.Equal((4, 2), (viewModel.CurrentDir.ShiftBaseX, viewModel.CurrentDir.ShiftBaseY));
        }

        [Fact]
        public void ChangingTheDirectionShowsItsFrames()
        {
            using TempDir dir = new();
            MainViewModel viewModel = WithTwoFrames(dir, new FakeDialogService());

            viewModel.CurrentDirection = 3;

            Assert.Empty(viewModel.Frames);
            Assert.Equal(3, viewModel.CurrentDirection);
            Assert.False(viewModel.FramesOffsetCommand.CanExecute(null));

            viewModel.CurrentDirection = 0;
            Assert.Equal(2, viewModel.Frames.Count);
        }

        [Fact]
        public void ANewProjectEmptiesTheList()
        {
            using TempDir dir = new();
            MainViewModel viewModel = WithTwoFrames(dir, new FakeDialogService());

            viewModel.NewCommand.Execute(null);

            Assert.Empty(viewModel.Frames);
            Assert.False(viewModel.HasFrames);
        }

        [Fact]
        public void AFileOpenedFromTheDialogReplacesTheProject()
        {
            using TempDir dir = new();
            FakeDialogService dialogs = new();
            MainViewModel source = WithTwoFrames(dir, dialogs);
            String project = dir.File("p.fpr");
            Assert.True(source.Service.SaveProject(project, dir.File("p.BMP")));

            MainViewModel viewModel = Create(dialogs);
            dialogs.OpenFilesAnswer = (_, _, _) => [project];
            viewModel.OpenCommand.Execute(null);

            Assert.Equal(2, viewModel.Frames.Count);
            Assert.Equal("p.fpr — Frame Animator", viewModel.Title);
        }

        [Fact]
        public void TheLanguageMessageRefreshesTheComputedTexts()
        {
            using TempDir dir = new();
            MainViewModel viewModel = WithTwoFrames(dir, new FakeDialogService());
            List<String?> changed = [];
            viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            WeakReferenceMessenger.Default.Send(new LanguageChangedMessage("en"));

            Assert.Contains(nameof(MainViewModel.StatusFrame), changed);
            Assert.Contains(nameof(MainViewModel.StatusPalette), changed);
            Assert.Contains(nameof(MainViewModel.SampleToolTip), changed);
        }

        [Fact]
        public void TheThemeMessageRedrawsTheDrawingArea()
        {
            MainViewModel viewModel = Create(new FakeDialogService());
            Int32 redraws = 0;
            viewModel.RenderRequested += (_, _) => redraws++;

            WeakReferenceMessenger.Default.Send(new ThemeChangedMessage(ThemePreference.Dark));

            Assert.Equal(1, redraws);
        }

        [Fact]
        public void RaisesPropertyChangedThroughTheToolkit()
        {
            MainViewModel viewModel = Create(new FakeDialogService());
            List<String?> changed = [];
            ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            viewModel.ShowFullNames = true;
            viewModel.Zoom = 4;

            Assert.Contains(nameof(MainViewModel.ShowFullNames), changed);
            Assert.Contains(nameof(MainViewModel.Zoom), changed);
            Assert.Contains(nameof(MainViewModel.ZoomText), changed);
        }
    }
}
