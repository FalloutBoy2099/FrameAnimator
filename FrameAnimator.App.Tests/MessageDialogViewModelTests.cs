namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using FrameAnimator.App.ViewModels;

    /// <summary>Message box: glyph, buttons per style and the clicked result.</summary>
    public class MessageDialogViewModelTests
    {
        [Theory]
        [InlineData(MessageBoxImage.Information, "")]
        [InlineData(MessageBoxImage.Warning, "")]
        [InlineData(MessageBoxImage.Error, "")]
        [InlineData(MessageBoxImage.Question, "")]
        [InlineData(MessageBoxImage.None, "")]
        public void GlyphDependsOnTheImage(MessageBoxImage image, String glyph)
        {
            MessageDialogViewModel viewModel = new(TestServices.Localizer(), "m", "c", MessageBoxButton.OK, image);

            Assert.Equal(glyph, viewModel.Glyph);
            Assert.Equal(glyph.Length > 0, viewModel.HasGlyph);
        }

        [Fact]
        public void YesNoCancelOffersThreeButtonsWithYesAsTheDefaultAndCancelToEscape()
        {
            MessageDialogViewModel viewModel = new(TestServices.Localizer(), "Save?", "c", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            Assert.Equal(["Yes", "No", "Cancel"], viewModel.Buttons.Select(button => button.Caption));
            Assert.Equal(MessageBoxResult.Yes, viewModel.Buttons.Single(button => button.IsDefault).Result);
            Assert.Equal(MessageBoxResult.Cancel, viewModel.Buttons.Single(button => button.IsCancel).Result);
        }

        [Fact]
        public void KeepsTheMessageAndTheCaption()
        {
            MessageDialogViewModel viewModel = new(TestServices.Localizer(), "Hello", "Frame Animator", MessageBoxButton.OK, MessageBoxImage.None);

            Assert.Equal("Hello", viewModel.Message);
            Assert.Equal("Frame Animator", viewModel.Caption);
            Assert.Equal(MessageBoxResult.None, viewModel.Result);
        }

        [Fact]
        public void OkHasASingleDefaultAndCancelButton()
        {
            MessageDialogViewModel viewModel = new(TestServices.Localizer(), "m", "c", MessageBoxButton.OK, MessageBoxImage.None);

            MessageButton button = Assert.Single(viewModel.Buttons);
            Assert.Equal(new MessageButton("OK", MessageBoxResult.OK, true, true), button);
        }

        [Fact]
        public void YesNoHasYesAsDefaultAndNoAsCancel()
        {
            MessageDialogViewModel viewModel = new(TestServices.Localizer(), "m", "c", MessageBoxButton.YesNo, MessageBoxImage.Question);

            Assert.Equal(
                [new MessageButton("Yes", MessageBoxResult.Yes, true, false), new MessageButton("No", MessageBoxResult.No, false, true)],
                viewModel.Buttons.ToArray());
        }

        [Fact]
        public void OkCancelHasTwoButtons()
        {
            MessageDialogViewModel viewModel = new(TestServices.Localizer(), "m", "c", MessageBoxButton.OKCancel, MessageBoxImage.None);

            Assert.Equal([MessageBoxResult.OK, MessageBoxResult.Cancel], viewModel.Buttons.Select(b => b.Result).ToArray());
            Assert.Equal([true, false], viewModel.Buttons.Select(b => b.IsDefault).ToArray());
        }

        [Fact]
        public void ChoosingAButtonStoresItsResultAndRequestsClose()
        {
            MessageDialogViewModel viewModel = new(TestServices.Localizer(), "m", "c", MessageBoxButton.YesNo, MessageBoxImage.Question);
            List<Boolean> closed = [];
            viewModel.CloseRequested += (_, accepted) => closed.Add(accepted);

            viewModel.ChooseCommand.Execute(MessageBoxResult.No);

            Assert.Equal(MessageBoxResult.No, viewModel.Result);
            Assert.Equal([true], closed);
        }
    }
}
