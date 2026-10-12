namespace FrameAnimator.App.Views
{
    using System;
    using System.IO;
    using System.Windows;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;
    using FrameAnimator.App.Services;
    using FrameAnimator.App.ViewModels;
    using Microsoft.Win32;

    /// <summary>
    /// Single entry point for file dialogs: the application's own dialog (<see cref="FileDialogWindow"/>) or, depending on
    /// <see cref="IDialogPreferences.UseSystemDialogs"/>, the legacy Windows dialog. Both remember the last folder
    /// used, separately for opening and for saving; only the application's dialog has favorites.
    /// </summary>
    public sealed class FileDialogs(
        DialogHost host,
        IDialogPreferences preferences,
        IFileDialogMemory memory,
        ILocalizer localizer,
        IMessageService messages)
    {
        /// <summary>Open dialog; empty array if canceled.</summary>
        public String[] ShowOpen(Window? owner, String title, String filter, Boolean multiselect)
        {
            if (!preferences.UseSystemDialogs)
                return ShowOwn(owner, FileDialogOptions.ForOpen(title, filter, multiselect));

            OpenFileDialog dialog = new()
            {
                Title = localizer.T(title),
                Filter = filter,
                Multiselect = multiselect,
                InitialDirectory = memory.GetLastDirectory(save: false) ?? "",
            };
            if (dialog.ShowDialog(owner) != true)
                return [];
            Remember(save: false, dialog.FileName);
            return dialog.FileNames;
        }

        /// <summary>Save dialog; <c>null</c> if canceled.</summary>
        public String? ShowSave(Window? owner, String title, String filter, Int32 filterIndex, String? fileName,
            String defaultExt, Boolean overwritePrompt = true)
        {
            if (!preferences.UseSystemDialogs)
            {
                String[] chosen = ShowOwn(owner, FileDialogOptions.ForSave(title, filter, filterIndex, fileName, defaultExt, overwritePrompt));
                return chosen.Length > 0 ? chosen[0] : null;
            }

            String initialDirectory = memory.GetLastDirectory(save: true) ?? "";
            String name = fileName ?? "";
            if (Path.GetDirectoryName(name) is { Length: > 0 } directory && Directory.Exists(directory))
            {
                initialDirectory = directory;
                name = Path.GetFileName(name);
            }

            SaveFileDialog dialog = new()
            {
                Title = localizer.T(title),
                Filter = filter,
                FilterIndex = filterIndex,
                FileName = name,
                InitialDirectory = initialDirectory,
                DefaultExt = defaultExt,
                AddExtension = defaultExt.Length > 0,
                OverwritePrompt = overwritePrompt,
                CheckFileExists = false,
            };
            if (dialog.ShowDialog(owner) != true)
                return null;
            Remember(save: true, dialog.FileName);
            return dialog.FileName;
        }

        /// <summary>The view model of the application's own dialog, wired to the real message boxes and the new folder prompt.</summary>
        public FileDialogViewModel CreateViewModel(FileDialogOptions options)
            => new(options, messages, PromptForText, localizer, memory);

        /// <summary>Asks for a line of text over the active window; <c>null</c> if canceled or empty.</summary>
        private String? PromptForText(String title, String prompt, String initial)
        {
            TextPromptDialogViewModel viewModel = new(localizer, title, prompt, initial);
            return host.ShowModal(new TextPromptDialog(), viewModel, host.ActiveWindow) ? viewModel.Result : null;
        }

        private String[] ShowOwn(Window? owner, FileDialogOptions options)
        {
            FileDialogViewModel viewModel = CreateViewModel(options);
            return host.ShowModal(new FileDialogWindow(), viewModel, owner) ? viewModel.Result : [];
        }

        private void Remember(Boolean save, String file)
        {
            if (Path.GetDirectoryName(file) is { Length: > 0 } directory && Directory.Exists(directory))
                memory.SetLastDirectory(save, directory);
        }
    }
}
