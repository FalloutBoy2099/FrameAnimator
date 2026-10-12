namespace FrameAnimator.Core.Services
{
    using System;

    /// <summary>
    /// Every message the core can show (errors and questions). The English text is the format string and the key of the
    /// translations in <c>Messages/*.json</c>; the labels reproduce those of the original (specification §6.5).
    /// </summary>
    public static class CoreMessages
    {
        public const String TwoLines = "{0}\n{1}";

        // palettes, background, images
        public const String UnableToLoadPalette = "Unable to load palette '{0}'!";
        public const String UnknownPaletteType = "Unknown palette type '*{0}'!";
        public const String UnableToLoadBackground = "Unable to load background '{0}'!";
        public const String UnknownFrameType = "Unknown frame type '*{0}'!";
        public const String UnableToLoadFrame = "Unable to load frame '{0}'!";

        // projects
        public const String UnableToLoadProject = "Unable to load project '{0}'!";
        public const String ContinueLoadingProject = "Continue loading project '{0}'?";
        public const String UnableToWriteProject = "Unable to write project '{0}'!";

        // FRM files
        public const String UnableToLoadFrm = "Unable to load FRM '{0}'!";
        public const String UnknownFrmType = "Unknown FRM type '*{0}'!";
        public const String InvalidPaletteForFallout = "Invalid palette type for dithering method 'Fallout'";
        public const String UnableToWriteFrm = "Unable to write FRM '{0}'!";
        public const String IncompleteFramesSet = "Incomplete frames set for '{0}'!";
        public const String UnknownFrmFileType = "Unknown FRM file type '*{0}'!";

        // opening
        public const String UsePalette = "Use palette '{0}'?";
        public const String UnknownFileType = "Unknown file type '*{0}'!";

        // scripts and batch processing
        public const String StopAutomatedRoutine = "Stop automated routine?";
        public const String UnableToExecuteScript = "Unable to execute script '{0}'!";
        public const String ContinueExecutingScript = "Continue executing script '{0}'?";
        public const String ScriptErrorPosition = "{0}\nin line {1}, parameter {2}";
        public const String IndexOutOfRange = "Index '{0}' out of range";
        public const String ParameterRequired = "Parameter is required";
        public const String UnknownKeyword = "Unknown keyword '{0}'";
        public const String InvalidParameter = "Invalid parameter '{0}'";
        public const String InvalidIntegerValue = "'{0}' is not a valid integer value";
    }
}
