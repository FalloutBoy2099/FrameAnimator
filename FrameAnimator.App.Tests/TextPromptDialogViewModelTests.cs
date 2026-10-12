namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using FrameAnimator.App.ViewModels;

    /// <summary>Text prompt: titles, trimmed result, empty text refused.</summary>
    public class TextPromptDialogViewModelTests
    {
        [Fact]
        public void StartsWithTheInitialText()
        {
            TextPromptDialogViewModel viewModel = new(TestServices.Localizer(), "New folder", "Folder name:", "Frames");

            Assert.Equal("New folder", viewModel.Title);
            Assert.Equal("Folder name:", viewModel.Prompt);
            Assert.Equal("Frames", viewModel.Text);
            Assert.Null(viewModel.Result);
        }

        [Fact]
        public void OkReturnsTheTrimmedTextAndRequestsClose()
        {
            TextPromptDialogViewModel viewModel = new(TestServices.Localizer(), "t", "p", "");
            List<Boolean> closed = [];
            viewModel.CloseRequested += (_, accepted) => closed.Add(accepted);

            viewModel.Text = "  my folder \t";
            viewModel.OkCommand.Execute(null);

            Assert.Equal("my folder", viewModel.Result);
            Assert.Equal([true], closed);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void OkWithAnEmptyTextDoesNothing(String text)
        {
            TextPromptDialogViewModel viewModel = new(TestServices.Localizer(), "t", "p", text);
            Boolean closed = false;
            viewModel.CloseRequested += (_, _) => closed = true;

            viewModel.OkCommand.Execute(null);

            Assert.Null(viewModel.Result);
            Assert.False(closed);
        }
    }
}
