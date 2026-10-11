namespace FrameAnimator.Core.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>Annulation d'une opération (équivalent de <c>EAbort</c> de la VCL).</summary>
    public sealed class OperationAbortedException(String? message = null) : Exception(message ?? "Operation aborted.");

    /// <summary>
    /// Interactions utilisateur demandées par le cœur. Les libellés reproduisent ceux de l'original (spécification §6.5).
    /// </summary>
    public interface IUserInteraction
    {
        /// <summary>Message d'erreur (<c>mtError</c>, bouton OK).</summary>
        void ShowError(String message);

        /// <summary>Question Oui/Non (<c>mtConfirmation</c>). Retourne <c>true</c> pour Oui.</summary>
        Boolean Confirm(String message);

        /// <summary>
        /// Boîte « Save Frames As » (BMP/PNG/GIF), pré‑remplie avec <paramref name="suggestedName"/> (nom du projet sans extension).
        /// Retourne le chemin choisi (avec extension) ou <c>null</c> si annulé.
        /// </summary>
        String? PromptSaveFramesAs(String suggestedName);

        /// <summary>Boîte « Load Palette » ; retourne le chemin choisi ou <c>null</c>.</summary>
        String? PromptPalette();

        /// <summary>Progression d'une opération (<c>done</c> sur <c>total</c> ; <c>0,0</c> = fin).</summary>
        void Progress(Int32 done, Int32 total);

        /// <summary>Vrai si l'utilisateur a demandé l'arrêt d'un traitement automatisé (touche Échap dans l'original).</summary>
        Boolean CancelRequested { get; }
    }

    /// <summary>Implémentation silencieuse : toutes les erreurs sont collectées, les questions reçoivent une réponse fixe.</summary>
    public sealed class SilentInteraction(Boolean confirmAnswer = false) : IUserInteraction
    {
        public List<String> Errors { get; } = [];
        public List<String> Questions { get; } = [];

        public void ShowError(String message) => Errors.Add(message);

        public Boolean Confirm(String message)
        {
            Questions.Add(message);
            return confirmAnswer;
        }

        public Func<String, String?>? SaveFramesAsHandler { get; set; }
        public Func<String?>? PaletteHandler { get; set; }

        public String? PromptSaveFramesAs(String suggestedName) => SaveFramesAsHandler?.Invoke(suggestedName);
        public String? PromptPalette() => PaletteHandler?.Invoke();
        public void Progress(Int32 done, Int32 total) { }
        public Boolean CancelRequested { get; set; }
    }
}