namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.ViewModels;

    /// <summary>"Offset" dialog: initial state from the request, wizard, validation and result.</summary>
    public class OffsetDialogViewModelTests
    {
        private static OffsetDialogRequest Request(Func<Int32, Int32>? wizard = null, Boolean wizardEnabled = true) => new()
        {
            Title = "Frame Offset",
            X = 3,
            Y = -2,
            ShowInherit = true,
            InheritEnabled = false,
            InheritChecked = true,
            ShowWizard = true,
            WizardEnabled = wizardEnabled,
            Wizard = wizard,
        };

        [Fact]
        public void StartsFromTheRequest()
        {
            OffsetDialogViewModel viewModel = new(Request(x => x), new FakeDialogService(), TestServices.Localizer());

            Assert.Equal("Frame Offset", viewModel.Title);
            Assert.Equal("3", viewModel.XText);
            Assert.Equal("-2", viewModel.YText);
            Assert.True(viewModel.Inherit);
            Assert.True(viewModel.ShowInherit);
            Assert.False(viewModel.InheritEnabled);
            Assert.True(viewModel.ShowWizard);
            Assert.True(viewModel.WizardEnabled);
            Assert.Null(viewModel.Result);
        }

        [Fact]
        public void WizardNeedsAFunctionAndTheEnabledFlag()
        {
            Assert.True(new OffsetDialogViewModel(Request(x => x), new FakeDialogService(), TestServices.Localizer()).WizardCommand.CanExecute(null));
            Assert.False(new OffsetDialogViewModel(Request(null), new FakeDialogService(), TestServices.Localizer()).WizardCommand.CanExecute(null));
            Assert.False(new OffsetDialogViewModel(Request(x => x, wizardEnabled: false), new FakeDialogService(), TestServices.Localizer()).WizardCommand.CanExecute(null));
        }

        [Fact]
        public void WizardComputesYFromTheTruncatedX()
        {
            List<Int32> received = [];
            OffsetDialogViewModel viewModel = new(Request(x =>
            {
                received.Add(x);
                return x * 2;
            }), new FakeDialogService(), TestServices.Localizer());

            viewModel.XText = " 70000 ";
            viewModel.WizardCommand.Execute(null);

            // 70000 truncated to 16 bits is 4464
            Assert.Equal([4464], received);
            Assert.Equal("4464", viewModel.XText);
            Assert.Equal("8928", viewModel.YText);
        }

        [Fact]
        public void WizardWithAnInvalidXWarnsAndKeepsTheValues()
        {
            FakeDialogService messages = new();
            OffsetDialogViewModel viewModel = new(Request(x => x), messages, TestServices.Localizer());

            viewModel.XText = "abc";
            viewModel.WizardCommand.Execute(null);

            Assert.Equal(["'abc' is not a valid integer value!"], messages.Warnings);
            Assert.Equal("abc", viewModel.XText);
            Assert.Equal("-2", viewModel.YText);
        }

        [Fact]
        public void OkStoresTheResultAndRequestsClose()
        {
            OffsetDialogViewModel viewModel = new(Request(x => x), new FakeDialogService(), TestServices.Localizer());
            List<Boolean> closed = [];
            viewModel.CloseRequested += (_, accepted) => closed.Add(accepted);

            viewModel.XText = " 12";
            viewModel.YText = "-7 ";
            viewModel.Inherit = false;
            viewModel.OkCommand.Execute(null);

            Assert.Equal(new OffsetDialogResult(12, -7, false), viewModel.Result);
            Assert.Equal([true], closed);
        }

        [Theory]
        [InlineData("x", "1")]
        [InlineData("1", "")]
        [InlineData("1.5", "2")]
        public void OkWithAnInvalidValueWarnsAndStaysOpen(String x, String y)
        {
            FakeDialogService messages = new();
            OffsetDialogViewModel viewModel = new(Request(v => v), messages, TestServices.Localizer());
            Boolean closed = false;
            viewModel.CloseRequested += (_, _) => closed = true;

            viewModel.XText = x;
            viewModel.YText = y;
            viewModel.OkCommand.Execute(null);

            Assert.Equal(["You enter is not a valid integer value!"], messages.Warnings);
            Assert.Null(viewModel.Result);
            Assert.False(closed);
        }
    }
}
