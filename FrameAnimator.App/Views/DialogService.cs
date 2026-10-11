namespace FrameAnimator.App.Views
{
    using System;
    using System.Windows;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;
	using Infrastructure;
	using Localization;

	/// <summary>Implémentation WPF de <see cref="IDialogService"/>.</summary>
	public sealed class DialogService : IDialogService
    {
        private const String Caption = "Frame Animator";

        public Window? Owner { get; set; }

        /// <summary>Traduit le nom de chaque filtre d'une boîte de fichiers (<c>Nom (*.ext)|*.ext|…</c>), pas ses motifs.</summary>
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
            => FileDialogs.ShowOpen(Owner, title, TranslateFilter(filter), multiSelect);

        public String? SaveFile(String title, String filter, Int32 filterIndex, String? fileName, String defaultExt)
            => FileDialogs.ShowSave(Owner, title, TranslateFilter(filter), filterIndex, fileName, defaultExt);

        public OffsetDialogResult? ShowOffset(OffsetDialogRequest request)
        {
            OffsetDialog dialog = new(request) { Owner = Owner };
            return dialog.ShowDialog() == true ? dialog.Result : null;
        }

        public Boolean ShowOptions(ProjectSettings settings, Int32 frameCount)
        {
            OptionsDialog dialog = new(settings, frameCount) { Owner = Owner };
            return dialog.ShowDialog() == true;
        }

        public AutomationOptions? ShowAutomate(String currentPalette)
        {
            AutomateDialog dialog = new(currentPalette) { Owner = Owner };
            return dialog.ShowDialog() == true ? dialog.Options : null;
        }

        public void ShowAbout() => new AboutDialog { Owner = Owner }.ShowDialog();

        public Int32? PickColor(Int32 initial)
        {
            ColorDialogWindow dialog = new(initial) { Owner = Owner };
            return dialog.ShowDialog() == true ? dialog.Result : null;
        }

        public void ShowWarning(String message)
            => MessageDialog.Show(Owner, Localizer.T(message), Caption, MessageBoxButton.OK, MessageBoxImage.Warning);

        public Boolean AskYesNo(String message)
            => MessageDialog.Show(Owner, Localizer.T(message), Caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}