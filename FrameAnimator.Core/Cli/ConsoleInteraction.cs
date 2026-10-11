namespace FrameAnimator.Core.Cli
{
    using System;
    using System.IO;
    using FrameAnimator.Core.Services;

    /// <summary>
    /// Interaction de la version console : aucune question n'est posée à l'utilisateur. Les erreurs vont sur la sortie d'erreur et
    /// sont comptées ; les questions de l'original reçoivent une réponse fixée par l'option <c>/continue</c>.
    /// </summary>
    internal sealed class ConsoleInteraction(TextWriter error, Boolean continueOnError, String framesExtension) : IUserInteraction
    {
        /// <summary>Nombre d'erreurs signalées depuis le début.</summary>
        public Int32 ErrorCount { get; private set; }

        public void ShowError(String message)
        {
            ErrorCount++;
            error.WriteLine("ERROR: " + message.Replace("\n", " | "));
        }

        /// <summary>
        /// Questions de l'original : « Stop automated routine? » (oui = arrêter) et « Continue …? » (oui = poursuivre).
        /// Par défaut le traitement s'arrête à la première erreur ; avec <c>/continue</c> il poursuit.
        /// </summary>
        public Boolean Confirm(String message)
        {
            Boolean answer = message.StartsWith("Stop", StringComparison.Ordinal) ? !continueOnError : continueOnError;
            error.WriteLine($"? {message} -> {(answer ? "yes" : "no")}");
            return answer;
        }

        /// <summary>Enregistrement des images d'un projet issu d'un FRM : à côté du projet, au format demandé par <c>/frames</c>.</summary>
        public String? PromptSaveFramesAs(String suggestedName) => suggestedName + "." + framesExtension;

        public String? PromptPalette() => null;

        public void Progress(Int32 done, Int32 total)
        {
        }

        public Boolean CancelRequested => false;
    }
}
