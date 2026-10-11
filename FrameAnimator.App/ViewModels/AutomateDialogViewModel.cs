namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.Collections;
    using System.Collections.ObjectModel;
    using System.Linq;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.Core.Scripting;

    /// <summary>View model of the "Automate" dialog (batch processing, specification §6.4).</summary>
    public sealed partial class AutomateDialogViewModel : DialogViewModelBase
    {
        private const String SourceFilter = "All (*.FPR,*.FRM,*.FR0-*.FR5)|*.FPR;*.FRM;*.FR0;*.FR1;*.FR2;*.FR3;*.FR4;*.FR5|Project (*.FPR)|*.FPR|FRM (*.FRM,*.FR0-*.FR5)|*.FRM;*.FR0;*.FR1;*.FR2;*.FR3;*.FR4;*.FR5";
        private const String PaletteFilter = "All (*.ACT, *.PAL)|*.ACT;*.PAL|Palette (*.ACT)|*.ACT|Fallout Palette (*.PAL)|*.PAL";
        private const String ScriptFilter = "Action Script (*.FAS)|*.FAS";
        private const String MaskFilter = "All (*.*)|*.*";

        private readonly IDialogService _dialogs;

        public AutomateDialogViewModel(String currentPalette, IDialogService dialogs)
        {
            _dialogs = dialogs;
            // the mask must be set before the format is chosen: the format replaces its extension
            DestinationMask = Options.DestinationMask;
            PalettePath = currentPalette;
            ApplyFormat();
        }

        public ObservableCollection<String> Sources { get; } = [];

        public String[] Formats { get; } = ["FPR-Project", "FRM", "FR0-FR5"];

        public String[] FrameFormats { get; } = ["BMP", "PNG", "GIF"];

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BrowsePaletteCommand))]
        public partial Boolean UsePalette { get; set; }

        [ObservableProperty]
        public partial String PalettePath { get; set; } = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BrowseScriptCommand))]
        public partial Boolean UseScript { get; set; }

        [ObservableProperty]
        public partial String ScriptPath { get; set; } = "";

        /// <summary>Index of the destination format; choosing one replaces the extension of the mask.</summary>
        [ObservableProperty]
        public partial Int32 FormatIndex { get; set; }

        [ObservableProperty]
        public partial Int32 FrameFormatIndex { get; set; }

        [ObservableProperty]
        public partial String DestinationMask { get; set; } = "";

        /// <summary>The chosen options, filled when the dialog was accepted.</summary>
        public AutomationOptions Options { get; } = new();

        partial void OnFormatIndexChanged(Int32 value) => ApplyFormat();

        private void ApplyFormat()
        {
            Options.Format = (AutomationFormat)Math.Max(0, FormatIndex);
            Options.DestinationMask = DestinationMask;
            Options.ApplyFormatToMask();
            DestinationMask = Options.DestinationMask;
        }

        [RelayCommand]
        private void AddSources()
        {
            foreach (String file in _dialogs.OpenFiles("Select Sources", SourceFilter, multiSelect: true))
                Sources.Add(file);
        }

        /// <summary>Removes the selected sources (the selected items of the list).</summary>
        [RelayCommand]
        private void RemoveSources(IList? selected)
        {
            if (selected is null)
                return;
            foreach (String item in selected.Cast<String>().ToList())
                Sources.Remove(item);
        }

        [RelayCommand(CanExecute = nameof(UsePalette))]
        private void BrowsePalette()
        {
            String[] files = _dialogs.OpenFiles("Load Palette", PaletteFilter, multiSelect: false);
            if (files.Length > 0)
                PalettePath = files[0];
        }

        [RelayCommand(CanExecute = nameof(UseScript))]
        private void BrowseScript()
        {
            String[] files = _dialogs.OpenFiles("Open Script", ScriptFilter, multiSelect: false);
            if (files.Length > 0)
                ScriptPath = files[0];
        }

        [RelayCommand]
        private void BrowseMask()
        {
            String? file = _dialogs.SaveFile("Destination", MaskFilter, 1, DestinationMask, "", overwritePrompt: false);
            if (file is not null)
                DestinationMask = file;
        }

        [RelayCommand]
        private void Ok()
        {
            if (Sources.Count == 0)
            {
                _dialogs.ShowWarning("Select at least one source file.");
                return;
            }
            Options.Sources.Clear();
            Options.Sources.AddRange(Sources);
            Options.UsePalette = UsePalette;
            Options.PalettePath = PalettePath;
            Options.UseScript = UseScript;
            Options.ScriptPath = ScriptPath;
            Options.Format = (AutomationFormat)Math.Max(0, FormatIndex);
            Options.DestinationMask = DestinationMask;
            Options.FrameExtension = FrameFormats[Math.Clamp(FrameFormatIndex, 0, FrameFormats.Length - 1)];
            RequestClose(true);
        }
    }
}
