namespace FrameAnimator.App.Views
{
    using System;
    using System.Windows;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;
    using FrameAnimator.App.ViewModels;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;

    /// <summary>WPF implementation of <see cref="IDialogService"/>.</summary>
    public sealed class DialogService(DialogHost host, FileDialogs files, IMessageService messages, ILocalizer localizer) : IDialogService
    {
        private AnimationPreviewWindow? _preview;

        /// <summary>Translates the name of each filter of a file dialog (<c>Name (*.ext)|*.ext|…</c>), not its patterns.</summary>
        public String TranslateFilter(String filter)
        {
            String[] parts = filter.Split('|');
            for (Int32 i = 0; i < parts.Length; i += 2)
            {
                Int32 paren = parts[i].IndexOf(" (", StringComparison.Ordinal);
                parts[i] = paren < 0 ? localizer.T(parts[i]) : localizer.T(parts[i][..paren]) + parts[i][paren..];
            }
            return String.Join('|', parts);
        }

        public String[] OpenFiles(String title, String filter, Boolean multiSelect)
            => files.ShowOpen(host.Target, title, TranslateFilter(filter), multiSelect);

        public String? SaveFile(String title, String filter, Int32 filterIndex, String? fileName, String defaultExt, Boolean overwritePrompt = true)
            => files.ShowSave(host.Target, title, TranslateFilter(filter), filterIndex, fileName, defaultExt, overwritePrompt);

        public OffsetDialogResult? ShowOffset(OffsetDialogRequest request)
        {
            OffsetDialogViewModel viewModel = new(request, messages, localizer);
            return host.ShowModal(new OffsetDialog(), viewModel, host.Owner) ? viewModel.Result : null;
        }

        public Boolean ShowOptions(ProjectSettings settings, Int32 frameCount)
            => host.ShowModal(new OptionsDialog(), new OptionsDialogViewModel(settings, frameCount, messages, localizer), host.Owner);

        public AutomationOptions? ShowAutomate(String currentPalette)
        {
            AutomateDialogViewModel viewModel = new(currentPalette, this);
            return host.ShowModal(new AutomateDialog(), viewModel, host.Owner) ? viewModel.Options : null;
        }

        public void ShowAbout() => host.ShowModal(new AboutDialog(), new AboutDialogViewModel(), host.Owner);

        public void TogglePreview(AnimationPreviewViewModel viewModel)
        {
            if (_preview is not null)
            {
                _preview.Close();
                return;
            }
            _preview = new AnimationPreviewWindow();
            _preview.Closed += (_, _) => _preview = null;
            host.Show(_preview, viewModel, host.Owner);
        }

        public Int32? PickColor(Int32 initial)
        {
            ColorDialogViewModel viewModel = new(initial);
            return host.ShowModal(new ColorDialogWindow(), viewModel, host.Owner) ? viewModel.Result : null;
        }

        public void ShowWarning(String message) => messages.ShowWarning(message);

        public Boolean AskYesNo(String message) => messages.AskYesNo(message);

        public MessageBoxResult AskYesNoCancel(String message) => messages.AskYesNoCancel(message);
    }
}
