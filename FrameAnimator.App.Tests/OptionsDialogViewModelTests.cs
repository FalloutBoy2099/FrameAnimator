namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using FrameAnimator.App.ViewModels;
    using FrameAnimator.Core.Model;

    /// <summary>"Options" dialog: initial state, lists, validation and writing into the project settings.</summary>
    public class OptionsDialogViewModelTests
    {
        [Fact]
        public void StartsFromTheSettings()
        {
            ProjectSettings settings = new()
            {
                TransparentMode = TransparentMode.Sample,
                AutoCutting = false,
                LockOffsets = true,
                LegacyColorMatching = false,
                GhostIncrement = -3,
                Fps = 12,
                DitherMethod = DitherMethod.Fallout,
                ActionFrame = 2,
            };

            OptionsDialogViewModel viewModel = new(settings, 5, new FakeDialogService(), TestServices.Localizer());

            Assert.False(viewModel.IsAutoTransparent);
            Assert.False(viewModel.IsPaletteTransparent);
            Assert.True(viewModel.IsSampleTransparent);
            Assert.False(viewModel.AutoCutting);
            Assert.True(viewModel.LockOffsets);
            Assert.False(viewModel.LegacyColorMatching);
            Assert.Equal("-3", viewModel.GhostText);
            Assert.Equal("12", viewModel.FpsText);
            Assert.Equal(["Default", "Advanced", "Fallout"], viewModel.DitherMethods);
            Assert.Equal(2, viewModel.DitherIndex);
            Assert.Equal(["0", "1", "2", "3", "4"], viewModel.ActionFrames);
            Assert.Equal(2, viewModel.ActionIndex);
        }

        [Fact]
        public void RadioButtonsFollowTheMode()
        {
            OptionsDialogViewModel viewModel = new(new ProjectSettings(), 1, new FakeDialogService(), TestServices.Localizer());
            List<String?> changed = [];
            viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            viewModel.IsPaletteTransparent = true;
            viewModel.IsAutoTransparent = false; // unchecking alone does not change the mode

            Assert.Equal(TransparentMode.Palette, viewModel.TransparentMode);
            Assert.True(viewModel.IsPaletteTransparent);
            Assert.False(viewModel.IsAutoTransparent);
            Assert.Contains(nameof(OptionsDialogViewModel.IsAutoTransparent), changed);
            Assert.Contains(nameof(OptionsDialogViewModel.IsPaletteTransparent), changed);
        }

        [Fact]
        public void ActionFramesHaveASingleEmptyItemWithoutFrames()
        {
            OptionsDialogViewModel viewModel = new(new ProjectSettings(), 0, new FakeDialogService(), TestServices.Localizer());

            Assert.Equal([""], viewModel.ActionFrames);
            Assert.Equal(0, viewModel.ActionIndex);
        }

        [Fact]
        public void ActionFrameIsClampedToTheLastFrame()
        {
            OptionsDialogViewModel viewModel = new(new ProjectSettings { ActionFrame = 9 }, 3, new FakeDialogService(), TestServices.Localizer());

            Assert.Equal(2, viewModel.ActionIndex);
        }

        [Fact]
        public void OkWritesTheSettingsAndRequestsClose()
        {
            ProjectSettings settings = new();
            OptionsDialogViewModel viewModel = new(settings, 4, new FakeDialogService(), TestServices.Localizer());
            List<Boolean> closed = [];
            viewModel.CloseRequested += (_, accepted) => closed.Add(accepted);

            viewModel.IsSampleTransparent = true;
            viewModel.AutoCutting = false;
            viewModel.LockOffsets = false;
            viewModel.LegacyColorMatching = false;
            viewModel.GhostText = " 2 ";
            viewModel.FpsText = "25";
            viewModel.DitherIndex = 1;
            viewModel.ActionIndex = 3;
            viewModel.OkCommand.Execute(null);

            Assert.Equal(TransparentMode.Sample, settings.TransparentMode);
            Assert.False(settings.AutoCutting);
            Assert.False(settings.LockOffsets);
            Assert.False(settings.LegacyColorMatching);
            Assert.Equal(2, settings.GhostIncrement);
            Assert.Equal(25, settings.Fps);
            Assert.Equal(DitherMethod.Advanced, settings.DitherMethod);
            Assert.Equal(3, settings.ActionFrame);
            Assert.Equal([true], closed);
        }

        [Fact]
        public void OkKeepsTheActionFrameWhenThereIsNoFrame()
        {
            ProjectSettings settings = new() { ActionFrame = 7 };
            OptionsDialogViewModel viewModel = new(settings, 0, new FakeDialogService(), TestServices.Localizer());

            viewModel.OkCommand.Execute(null);

            Assert.Equal(7, settings.ActionFrame);
        }

        [Theory]
        [InlineData("abc", "10")]
        [InlineData("40000", "10")]
        [InlineData("1", "-1")]
        [InlineData("1", "70000")]
        [InlineData("1", "")]
        public void OkWithAnInvalidNumberWarnsAndChangesNothing(String ghost, String fps)
        {
            ProjectSettings settings = new() { Fps = 5, GhostIncrement = -1 };
            FakeDialogService messages = new();
            OptionsDialogViewModel viewModel = new(settings, 2, messages, TestServices.Localizer());
            Boolean closed = false;
            viewModel.CloseRequested += (_, _) => closed = true;

            viewModel.IsPaletteTransparent = true;
            viewModel.GhostText = ghost;
            viewModel.FpsText = fps;
            viewModel.OkCommand.Execute(null);

            Assert.Equal(["You enter is not a valid integer value!"], messages.Warnings);
            Assert.False(closed);
            Assert.Equal(TransparentMode.Auto, settings.TransparentMode);
            Assert.Equal(5, settings.Fps);
            Assert.Equal(-1, settings.GhostIncrement);
        }
    }
}
