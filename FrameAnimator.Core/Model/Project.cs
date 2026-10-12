namespace FrameAnimator.Core.Model
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

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

        /// <summary>
        /// A fingerprint of what a project file stores: the persisted options, the offsets and files of every frame, the palette, the
        /// background and the FRM. Two states with the same fingerprint would write the same project file; it is how "has the project
        /// changed since it was loaded or saved" is answered, whatever way it was edited (dialog, mouse, script).
        /// </summary>
        public String ContentSignature()
        {
            StringBuilder text = new();
            ProjectSettings s = Settings;
            text.Append(CultureInfo.InvariantCulture, $"{(Int32)s.TransparentMode},{s.AutoCutting},{(Int32)s.DitherMethod},{s.Fps},{s.ActionFrame},{s.SampleColor}").Append('\n');
            text.Append(PaletteFile).Append('\n').Append(BackgroundFile).Append('\n').Append(FrmFile).Append('\n');
            foreach (Direction dir in Directions)
            {
                text.Append(CultureInfo.InvariantCulture, $"D{dir.ShiftBaseX},{dir.ShiftBaseY},{dir.FirstFrameOffsetX},{dir.FirstFrameOffsetY}").Append('\n');
                foreach (Frame frame in dir.Frames)
                    text.Append(CultureInfo.InvariantCulture, $"F{frame.OffsetX},{frame.OffsetY},{frame.SourceIndex},{frame.FileName}").Append('\n');
            }
            return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }

        /// <summary>Window title: project name or "Untitled.fpr".</summary>
        public String Title => String.IsNullOrEmpty(ProjectFile) ? "Untitled.fpr" : Path.GetFileName(ProjectFile);
    }
}