namespace FrameAnimator.Core.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>Cancellation of an operation (equivalent of the VCL's <c>EAbort</c>).</summary>
    public sealed class OperationAbortedException(String? message = null) : Exception(message ?? "Operation aborted.");

    /// <summary>
    /// User interactions requested by the core. The labels reproduce those of the original (specification §6.5).
    /// </summary>
    public interface IUserInteraction
    {
        /// <summary>Error message (<c>mtError</c>, OK button).</summary>
        void ShowError(String message);

        /// <summary>Yes/No question (<c>mtConfirmation</c>). Returns <c>true</c> for Yes.</summary>
        Boolean Confirm(String message);

        /// <summary>
        /// "Save Frames As" dialog (BMP/PNG/GIF), pre-filled with <paramref name="suggestedName"/> (project name without extension).
        /// Returns the chosen path (with extension), or <c>null</c> if canceled.
        /// </summary>
        String? PromptSaveFramesAs(String suggestedName);

        /// <summary>"Load Palette" dialog; returns the chosen path, or <c>null</c>.</summary>
        String? PromptPalette();

        /// <summary>Progress of an operation (<c>done</c> out of <c>total</c>; <c>0,0</c> = finished).</summary>
        void Progress(Int32 done, Int32 total);

        /// <summary>True if the user requested to stop an automated process (Esc key in the original).</summary>
        Boolean CancelRequested { get; }
    }

    /// <summary>Silent implementation: all errors are collected, questions receive a fixed answer.</summary>
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