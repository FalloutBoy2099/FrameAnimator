namespace FrameAnimator.App.Views
{
    using System;
    using System.Windows;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;
    using FrameAnimator.App.ViewModels;
    using Infrastructure;
    using Localization;

    /// <summary>WPF implementation of <see cref="IDialogService"/>.</summary>
    public sealed class DialogService : IDialogService
    {
        public Window? Owner { get; set; }

        /// <summary>Messages appear over the window the user works in (a dialog if one is open), else over <see cref="Owner"/>.</summary>
        private MessageService Messages => new() { Owner = Owner };

        /// <summary>Translates the name of each filter of a file dialog (<c>Name (*.ext)|*.ext|…</c>), not its patterns.</summary>
        internal static String TranslateFilter(String filter)
        {
            String[] parts = filter.Split('|');
            for (Int32 i = 0; i < parts.Length; i += 2)
            {
                Int32 paren = parts[i].IndexOf(" (", StringComparison.Ordinal);
                parts[i] = paren < 0 ? Localizer.T(parts[i]) : Localizer.T(parts[i][..paren]) + parts[i][paren..];
            }
            return String.Join('|', parts);
        }

        public String[] OpenFiles(String title, String filter, Boolean multiSelect)
            => FileDialogs.ShowOpen(DialogHost.ActiveWindow ?? Owner, title, TranslateFilter(filter), multiSelect);

        public String? SaveFile(String title, String filter, Int32 filterIndex, String? fileName, String defaultExt, Boolean overwritePrompt = true)
            => FileDialogs.ShowSave(DialogHost.ActiveWindow ?? Owner, title, TranslateFilter(filter), filterIndex, fileName, defaultExt, overwritePrompt);

        public OffsetDialogResult? ShowOffset(OffsetDialogRequest request)
        {
            OffsetDialogViewModel viewModel = new(request, Messages);
            return DialogHost.ShowModal(new OffsetDialog(), viewModel, Owner) ? viewModel.Result : null;
        }

        public Boolean ShowOptions(ProjectSettings settings, Int32 frameCount)
            => DialogHost.ShowModal(new OptionsDialog(), new OptionsDialogViewModel(settings, frameCount, Messages), Owner);

        public AutomationOptions? ShowAutomate(String currentPalette)
        {
            AutomateDialogViewModel viewModel = new(currentPalette, this);
            return DialogHost.ShowModal(new AutomateDialog(), viewModel, Owner) ? viewModel.Options : null;
        }

        public void ShowAbout() => DialogHost.ShowModal(new AboutDialog(), new AboutDialogViewModel(), Owner);

        public Int32? PickColor(Int32 initial)
        {
            ColorDialogViewModel viewModel = new(initial);
            return DialogHost.ShowModal(new ColorDialogWindow(), viewModel, Owner) ? viewModel.Result : null;
        }

        public void ShowWarning(String message) => Messages.ShowWarning(message);

        public Boolean AskYesNo(String message) => Messages.AskYesNo(message);
    }
}