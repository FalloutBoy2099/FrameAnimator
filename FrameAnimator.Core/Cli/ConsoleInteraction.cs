namespace FrameAnimator.Core.Cli
{
    using System;
    using System.IO;
    using FrameAnimator.Core.Services;

    /// <summary>
    /// Interaction for the console version: no question is asked to the user. Errors go to the error output and
    /// are counted; the original's questions get an answer fixed by the <c>/continue</c> option.
    /// </summary>
    internal sealed class ConsoleInteraction(TextWriter error, Boolean continueOnError, String framesExtension) : IUserInteraction
    {
        /// <summary>Number of errors reported since the start.</summary>
        public Int32 ErrorCount { get; private set; }

        public void ShowError(String message)
        {
            ErrorCount++;
            error.WriteLine("ERROR: " + message.Replace("\n", " | "));
        }

        /// <summary>
        /// Questions of the original: "Stop automated routine?" (yes = stop) and "Continue …?" (yes = proceed).
        /// By default processing stops at the first error; with <c>/continue</c> it proceeds.
        /// </summary>
        public Boolean Confirm(String message)
        {
            Boolean answer = message.StartsWith("Stop", StringComparison.Ordinal) ? !continueOnError : continueOnError;
            error.WriteLine($"? {message} -> {(answer ? "yes" : "no")}");
            return answer;
        }

        /// <summary>Saving the images of a project derived from an FRM: next to the project, in the format requested by <c>/frames</c>.</summary>
        public String? PromptSaveFramesAs(String suggestedName) => suggestedName + "." + framesExtension;

        public String? PromptPalette() => null;

        public void Progress(Int32 done, Int32 total)
        {
        }

        public Boolean CancelRequested => false;
    }
}
