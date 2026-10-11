namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Media;
    using FrameAnimator.App.ViewModels;

    /// <summary>Color picker: R, G, B and the hexadecimal text stay in sync.</summary>
    public class ColorDialogViewModelTests
    {
        private static Color PreviewColor(ColorDialogViewModel viewModel) => ((SolidColorBrush)viewModel.Preview).Color;

        [Fact]
        public void StartsFromTheInitialColor()
        {
            ColorDialogViewModel viewModel = new(0x80C040);

            Assert.Equal(0x80, viewModel.R);
            Assert.Equal(0xC0, viewModel.G);
            Assert.Equal(0x40, viewModel.B);
            Assert.Equal("80C040", viewModel.Hex);
            Assert.Equal(0x80C040, viewModel.Current);
            Assert.Equal(Color.FromRgb(0x80, 0xC0, 0x40), PreviewColor(viewModel));
        }

        [Fact]
        public void TheAlphaByteOfTheInitialColorIsIgnored()
        {
            ColorDialogViewModel viewModel = new(unchecked((Int32)0xFF102030));

            Assert.Equal("102030", viewModel.Hex);
        }

        [Fact]
        public void ChangingAComponentUpdatesTheHexAndThePreview()
        {
            ColorDialogViewModel viewModel = new(0x000000);
            List<String?> changed = [];
            viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            viewModel.G = 255;
            viewModel.B = 1;

            Assert.Equal("00FF01", viewModel.Hex);
            Assert.Equal(Color.FromRgb(0, 255, 1), PreviewColor(viewModel));
            Assert.Contains(nameof(ColorDialogViewModel.Preview), changed);
        }

        [Fact]
        public void TypingAValidHexUpdatesTheComponentsAndKeepsTheText()
        {
            ColorDialogViewModel viewModel = new(0x000000);

            viewModel.Hex = "FF";

            Assert.Equal(0, viewModel.R);
            Assert.Equal(0, viewModel.G);
            Assert.Equal(255, viewModel.B);
            Assert.Equal("FF", viewModel.Hex); // not reformatted while typing
            Assert.Equal(Color.FromRgb(0, 0, 255), PreviewColor(viewModel));
        }

        [Fact]
        public void AnInvalidHexIsIgnored()
        {
            ColorDialogViewModel viewModel = new(0x102030);

            viewModel.Hex = "zz";
            viewModel.Hex = "";

            Assert.Equal(0x102030, viewModel.Current);
        }

        [Fact]
        public void ComponentsAreClamped()
        {
            ColorDialogViewModel viewModel = new(0x000000);

            viewModel.R = 300;
            viewModel.G = -5;

            Assert.Equal(255, viewModel.R);
            Assert.Equal(0, viewModel.G);
            Assert.Equal("FF0000", viewModel.Hex);
        }

        [Fact]
        public void OkStoresTheColorAndRequestsClose()
        {
            ColorDialogViewModel viewModel = new(0x000000);
            List<Boolean> closed = [];
            viewModel.CloseRequested += (_, accepted) => closed.Add(accepted);

            viewModel.Hex = "A1B2C3";
            viewModel.OkCommand.Execute(null);

            Assert.Equal(0xA1B2C3, viewModel.Result);
            Assert.Equal([true], closed);
        }
    }
}
