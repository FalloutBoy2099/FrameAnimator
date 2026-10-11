namespace FrameAnimator.Core.Model
{
    using System;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Complete state of a Frame Animator project: 6 directions, palette, settings, associated files, undo stack.
    /// Equivalent of the fields of the main window <c>TfrmFrameAnimator</c> (see specification §3.1).
    /// </summary>
    public sealed class Project
    {
        public const Int32 DirectionCount = 6;

        public static readonly String[] DirectionNames = ["North East", "East", "South East", "South West", "West", "North West"];

        public Direction[] Directions { get; } = Enumerable.Range(0, DirectionCount).Select(_ => new Direction()).ToArray();

        public Palette Palette { get; set; } = Palette.LoadDefault();

        public ProjectSettings Settings { get; } = new();

        public UndoStack Undo { get; } = new();

        /// <summary>Path of the current project/FRM (empty if "Untitled").</summary>
        public String ProjectFile { get; set; } = "";

        /// <summary>Key <c>Background=</c> of the FPR (background BMP file, empty if none).</summary>
        public String BackgroundFile { get; set; } = "";

        /// <summary>Loaded background image (displayed centered behind the frames), or <c>null</c>.</summary>
        public Bitmap32? Background { get; set; }

        /// <summary>Key <c>FRM=</c> of the FPR (last FRM created).</summary>
        public String FrmFile { get; set; } = "";

        /// <summary>Key <c>Pallete=</c> of the FPR: file of the current palette (<see cref="Palette.FileName"/>).</summary>
        public String PaletteFile => Palette.FileName;

        /// <summary>Direction currently displayed.</summary>
        public Int32 CurrentDirection { get; set; }

        public Direction Dir(Int32 index) => Directions[index];

        /// <summary>Resets to an empty project (without touching the palette — see<see cref="ProjectService.NewProject"/>).</summary>
        public void ClearContent()
        {
            foreach (var d in Directions)
                d.Clear();
            Undo.Clear();
            Settings.ResetToDefaults();
            ProjectFile = "";
            BackgroundFile = "";
            Background = null;
            FrmFile = "";
            CurrentDirection = 0;
        }

        /// <summary>Window title: project name or "Untitled.fpr".</summary>
        public String Title => String.IsNullOrEmpty(ProjectFile) ? "Untitled.fpr" : Path.GetFileName(ProjectFile);
    }
}