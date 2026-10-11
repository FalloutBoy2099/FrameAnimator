namespace FrameAnimator.App.Infrastructure
{
    using System;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;

    /// <summary>Parameters of the "Offset" dialog (Frames Offset / Frame Offset / First Frame Offset).</summary>
    public sealed class OffsetDialogRequest
    {
        public String Title { get; init; } = "Offset";
        public Int32 X { get; init; }
        public Int32 Y { get; init; }

        /// <summary>Shows the "Inherit shift down" checkbox.</summary>
        public Boolean ShowInherit { get; init; }
        public Boolean InheritEnabled { get; init; } = true;
        public Boolean InheritChecked { get; init; }

        /// <summary>Shows the offset wizard button.</summary>
        public Boolean ShowWizard { get; init; }
        public Boolean WizardEnabled { get; init; }

        /// <summary>Wizard: receives X and returns the computed Y.</summary>
        public Func<Int32, Int32>? Wizard { get; init; }
    }

    public readonly record struct OffsetDialogResult(Int32 X, Int32 Y, Boolean Inherit);

    /// <summary>Dialog services (files, modal boxes) used by the view model.</summary>
    public interface IDialogService
    {
        /// <summary>Open dialog; empty array if cancelled.</summary>
        String[] OpenFiles(String title, String filter, Boolean multiSelect);

        /// <summary>Save dialog; <c>null</c> if cancelled.</summary>
        String? SaveFile(String title, String filter, Int32 filterIndex, String? fileName, String defaultExt);

        OffsetDialogResult? ShowOffset(OffsetDialogRequest request);

        /// <summary>Options dialog: modifies <paramref name="settings"/> if confirmed.</summary>
        Boolean ShowOptions(ProjectSettings settings, Int32 frameCount);

        AutomationOptions? ShowAutomate(String currentPalette);

        void ShowAbout();

        /// <summary>Color picker (0xRRGGBB); <c>null</c> if cancelled.</summary>
        Int32? PickColor(Int32 initial);

        void ShowWarning(String message);

        Boolean AskYesNo(String message);
    }
}