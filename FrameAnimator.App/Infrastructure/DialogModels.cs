namespace FrameAnimator.App.Infrastructure
{
    using System;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;

    /// <summary>Paramètres de la boîte « Offset » (Frames Offset / Frame Offset / First Frame Offset).</summary>
    public sealed class OffsetDialogRequest
    {
        public String Title { get; init; } = "Offset";
        public Int32 X { get; init; }
        public Int32 Y { get; init; }

        /// <summary>Affiche la case « Inherit shift down ».</summary>
        public Boolean ShowInherit { get; init; }
        public Boolean InheritEnabled { get; init; } = true;
        public Boolean InheritChecked { get; init; }

        /// <summary>Affiche le bouton de l'assistant d'offset.</summary>
        public Boolean ShowWizard { get; init; }
        public Boolean WizardEnabled { get; init; }

        /// <summary>Assistant : reçoit X et retourne le Y calculé.</summary>
        public Func<Int32, Int32>? Wizard { get; init; }
    }

    public readonly record struct OffsetDialogResult(Int32 X, Int32 Y, Boolean Inherit);

    /// <summary>Services de dialogue (fichiers, boîtes modales) utilisés par le modèle de vue.</summary>
    public interface IDialogService
    {
        /// <summary>Boîte d'ouverture ; tableau vide si annulée.</summary>
        String[] OpenFiles(String title, String filter, Boolean multiSelect);

        /// <summary>Boîte d'enregistrement ; <c>null</c> si annulée.</summary>
        String? SaveFile(String title, String filter, Int32 filterIndex, String? fileName, String defaultExt);

        OffsetDialogResult? ShowOffset(OffsetDialogRequest request);

        /// <summary>Boîte Options : modifie <paramref name="settings"/> si validée.</summary>
        Boolean ShowOptions(ProjectSettings settings, Int32 frameCount);

        AutomationOptions? ShowAutomate(String currentPalette);

        void ShowAbout();

        /// <summary>Sélecteur de couleur (0xRRGGBB) ; <c>null</c> si annulé.</summary>
        Int32? PickColor(Int32 initial);

        void ShowWarning(String message);

        Boolean AskYesNo(String message);
    }
}