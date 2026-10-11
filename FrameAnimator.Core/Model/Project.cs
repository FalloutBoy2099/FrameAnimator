namespace FrameAnimator.Core.Model
{
    using System;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// État complet d'un projet Frame Animator : 6 directions, palette, réglages, fichiers associés, pile d'annulation.
    /// Équivalent des champs de la fenêtre principale <c>TfrmFrameAnimator</c> (voir spécification §3.1).
    /// </summary>
    public sealed class Project
    {
        public const Int32 DirectionCount = 6;

        public static readonly String[] DirectionNames = ["North East", "East", "South East", "South West", "West", "North West"];

        public Direction[] Directions { get; } = Enumerable.Range(0, DirectionCount).Select(_ => new Direction()).ToArray();

        public Palette Palette { get; set; } = Palette.LoadDefault();

        public ProjectSettings Settings { get; } = new();

        public UndoStack Undo { get; } = new();

        /// <summary>Chemin du projet/FRM courant (vide si « Untitled »).</summary>
        public String ProjectFile { get; set; } = "";

        /// <summary>Clé <c>Background=</c> du FPR (fichier BMP de fond, vide si aucun).</summary>
        public String BackgroundFile { get; set; } = "";

        /// <summary>Image de fond chargée (affichée centrée derrière les frames), ou <c>null</c>.</summary>
        public Bitmap32? Background { get; set; }

        /// <summary>Clé <c>FRM=</c> du FPR (dernier FRM créé).</summary>
        public String FrmFile { get; set; } = "";

        /// <summary>Clé <c>Pallete=</c> du FPR : fichier de la palette courante (<see cref="Palette.FileName"/>).</summary>
        public String PaletteFile => Palette.FileName;

        /// <summary>Direction actuellement affichée.</summary>
        public Int32 CurrentDirection { get; set; }

        public Direction Dir(Int32 index) => Directions[index];

        /// <summary>Remet un projet vide (sans toucher à la palette — voir <see cref="ProjectService.NewProject"/>).</summary>
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

        /// <summary>Titre de fenêtre : nom du projet ou « Untitled.fpr ».</summary>
        public String Title => String.IsNullOrEmpty(ProjectFile) ? "Untitled.fpr" : Path.GetFileName(ProjectFile);
    }
}