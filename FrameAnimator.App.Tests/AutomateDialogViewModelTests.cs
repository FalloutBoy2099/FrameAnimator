namespace FrameAnimator.App.Tests
{
    using System;
    using System.Collections.Generic;
    using FrameAnimator.App.ViewModels;
    using FrameAnimator.Core.Scripting;

    /// <summary>"Automate" dialog: sources, browse commands, mask/format behavior and the resulting options.</summary>
    public class AutomateDialogViewModelTests
    {
        private const String PaletteFilter = "All (*.ACT, *.PAL)|*.ACT;*.PAL|Palette (*.ACT)|*.ACT|Fallout Palette (*.PAL)|*.PAL";

        [Fact]
        public void StartsWithTheDefaults()
        {
            AutomateDialogViewModel viewModel = new("Default.act", new FakeDialogService());

            Assert.Empty(viewModel.Sources);
            Assert.Equal("Default.act", viewModel.PalettePath);
            Assert.Equal("", viewModel.ScriptPath);
            Assert.False(viewModel.UsePalette);
            Assert.False(viewModel.UseScript);
            Assert.Equal(["FPR-Project", "FRM", "FR0-FR5"], viewModel.Formats);
            Assert.Equal(["BMP", "PNG", "GIF"], viewModel.FrameFormats);
            Assert.Equal(0, viewModel.FormatIndex);
            Assert.Equal(0, viewModel.FrameFormatIndex);
            Assert.Equal("*.FPR", viewModel.DestinationMask);
        }

        [Fact]
        public void ChoosingAFormatReplacesTheExtensionOfTheMask()
        {
            AutomateDialogViewModel viewModel = new("", new FakeDialogService());

            viewModel.DestinationMask = "out\\*.FPR";
            viewModel.FormatIndex = 1;
            Assert.Equal("out\\*.FRM", viewModel.DestinationMask);

            viewModel.FormatIndex = 2;
            Assert.Equal("out\\*.FR0", viewModel.DestinationMask);

            viewModel.FormatIndex = 0;
            Assert.Equal("out\\*.FPR", viewModel.DestinationMask);
        }

        [Fact]
        public void EditingTheMaskDoesNotChangeTheFormat()
        {
            AutomateDialogViewModel viewModel = new("", new FakeDialogService());

            viewModel.DestinationMask = "x\\*.TXT";

            Assert.Equal("x\\*.TXT", viewModel.DestinationMask);
            Assert.Equal(0, viewModel.FormatIndex);
        }

        [Fact]
        public void AddSourcesAsksForSeveralFiles()
        {
            FakeDialogService dialogs = new();
            List<(String Title, String Filter, Boolean Multi)> calls = [];
            dialogs.OpenFilesAnswer = (title, filter, multi) =>
            {
                calls.Add((title, filter, multi));
                return ["a.fpr", "b.frm"];
            };
            AutomateDialogViewModel viewModel = new("", dialogs);

            viewModel.AddSourcesCommand.Execute(null);

            Assert.Equal(["a.fpr", "b.frm"], viewModel.Sources);
            (String title, String filter, Boolean multi) = Assert.Single(calls);
            Assert.Equal("Select Sources", title);
            Assert.StartsWith("All (*.FPR,*.FRM,*.FR0-*.FR5)|", filter);
            Assert.True(multi);
        }

        [Fact]
        public void RemoveSourcesRemovesTheSelectedOnes()
        {
            FakeDialogService dialogs = new() { OpenFilesAnswer = (_, _, _) => ["a", "b", "c"] };
            AutomateDialogViewModel viewModel = new("", dialogs);
            viewModel.AddSourcesCommand.Execute(null);

            viewModel.RemoveSourcesCommand.Execute(new List<String> { "a", "c" });

            Assert.Equal(["b"], viewModel.Sources);
            viewModel.RemoveSourcesCommand.Execute(null); // no selection: nothing happens
            Assert.Equal(["b"], viewModel.Sources);
        }

        [Fact]
        public void BrowseCommandsFollowTheCheckboxes()
        {
            AutomateDialogViewModel viewModel = new("", new FakeDialogService());
            Assert.False(viewModel.BrowsePaletteCommand.CanExecute(null));
            Assert.False(viewModel.BrowseScriptCommand.CanExecute(null));
            Assert.True(viewModel.BrowseMaskCommand.CanExecute(null));

            Int32 raised = 0;
            viewModel.BrowsePaletteCommand.CanExecuteChanged += (_, _) => raised++;
            viewModel.UsePalette = true;
            viewModel.UseScript = true;

            Assert.Equal(1, raised);
            Assert.True(viewModel.BrowsePaletteCommand.CanExecute(null));
            Assert.True(viewModel.BrowseScriptCommand.CanExecute(null));
        }

        [Fact]
        public void BrowsePaletteAndScriptTakeTheFirstFile()
        {
            FakeDialogService dialogs = new();
            List<(String Title, String Filter, Boolean Multi)> calls = [];
            dialogs.OpenFilesAnswer = (title, filter, multi) =>
            {
                calls.Add((title, filter, multi));
                return [title == "Load Palette" ? "p.pal" : "s.fas"];
            };
            AutomateDialogViewModel viewModel = new("Default.act", dialogs);
            viewModel.UsePalette = true;
            viewModel.UseScript = true;

            viewModel.BrowsePaletteCommand.Execute(null);
            viewModel.BrowseScriptCommand.Execute(null);

            Assert.Equal("p.pal", viewModel.PalettePath);
            Assert.Equal("s.fas", viewModel.ScriptPath);
            Assert.Equal(("Load Palette", PaletteFilter, false), calls[0]);
            Assert.Equal(("Open Script", "Action Script (*.FAS)|*.FAS", false), calls[1]);
        }

        [Fact]
        public void BrowseDoesNothingWhenCancelled()
        {
            AutomateDialogViewModel viewModel = new("Default.act", new FakeDialogService());
            viewModel.UsePalette = true;

            viewModel.BrowsePaletteCommand.Execute(null);
            viewModel.BrowseMaskCommand.Execute(null);

            Assert.Equal("Default.act", viewModel.PalettePath);
            Assert.Equal("*.FPR", viewModel.DestinationMask);
        }

        [Fact]
        public void BrowseMaskUsesTheSaveDialogWithTheCurrentMask()
        {
            FakeDialogService dialogs = new();
            List<(String Title, String Filter, Int32 Index, String? Name, String Ext)> calls = [];
            dialogs.SaveFileAnswer = (title, filter, index, name, ext) =>
            {
                calls.Add((title, filter, index, name, ext));
                return "d:\\out\\*.FPR";
            };
            AutomateDialogViewModel viewModel = new("", dialogs);

            viewModel.BrowseMaskCommand.Execute(null);

            Assert.Equal("d:\\out\\*.FPR", viewModel.DestinationMask);
            Assert.Equal(("Destination", "All (*.*)|*.*", 1, "*.FPR", ""), Assert.Single(calls));
            Assert.False(dialogs.LastOverwritePrompt); // a mask is not a file: no "replace it?" question
        }

        [Fact]
        public void OkWithoutSourceWarnsAndStaysOpen()
        {
            FakeDialogService dialogs = new();
            AutomateDialogViewModel viewModel = new("", dialogs);
            Boolean closed = false;
            viewModel.CloseRequested += (_, _) => closed = true;

            viewModel.OkCommand.Execute(null);

            Assert.Equal(["Select at least one source file."], dialogs.Warnings);
            Assert.False(closed);
        }

        [Fact]
        public void OkFillsTheOptionsAndRequestsClose()
        {
            FakeDialogService dialogs = new() { OpenFilesAnswer = (_, _, _) => ["a.fpr", "b.fr0"] };
            AutomateDialogViewModel viewModel = new("Default.act", dialogs);
            List<Boolean> closed = [];
            viewModel.CloseRequested += (_, accepted) => closed.Add(accepted);
            viewModel.AddSourcesCommand.Execute(null);

            viewModel.UsePalette = true;
            viewModel.PalettePath = "x.pal";
            viewModel.UseScript = true;
            viewModel.ScriptPath = "s.fas";
            viewModel.FormatIndex = 1;
            viewModel.FrameFormatIndex = 2;
            viewModel.OkCommand.Execute(null);

            AutomationOptions options = viewModel.Options;
            Assert.Equal(["a.fpr", "b.fr0"], options.Sources);
            Assert.True(options.UsePalette);
            Assert.Equal("x.pal", options.PalettePath);
            Assert.True(options.UseScript);
            Assert.Equal("s.fas", options.ScriptPath);
            Assert.Equal(AutomationFormat.Frm, options.Format);
            Assert.Equal("*.FRM", options.DestinationMask);
            Assert.Equal("GIF", options.FrameExtension);
            Assert.Equal([true], closed);
        }
    }
}
