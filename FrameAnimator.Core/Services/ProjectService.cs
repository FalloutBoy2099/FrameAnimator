namespace FrameAnimator.Core.Services
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using Formats;
    using Geometry;
    using Imaging;
    using Model;

    /// <summary>
    /// Opérations de haut niveau sur un <see cref="Project"/> : nouveau projet, chargement de palette / fond / images /
    /// FPR / FRM, enregistrement de FPR / FRM. Chaque méthode reproduit la séquence et les messages de l'original
    /// (spécification §4 et §8) ; les erreurs sont signalées via <see cref="IUserInteraction"/> et un résultat booléen.
    /// </summary>
    public sealed class ProjectService(Project project, IUserInteraction ui, String? appDirectory = null)
    {
        public Project Project { get; } = project;
        public IUserInteraction UI { get; } = ui;

        /// <summary>Dossier contenant <c>Default.act</c> (dossier de l'exécutable dans l'original).</summary>
        public String AppDirectory { get; } = appDirectory ?? AppContext.BaseDirectory;

        /// <summary>
        /// <c>true</c> (défaut, comportement de l'original) : l'ouverture d'un FRM sans palette <c>.PAL</c> voisine pose la question
        /// « Use palette '…'? ». <c>false</c> : la palette courante est conservée sans question (réponse « oui »).
        /// </summary>
        public Boolean ConfirmPaletteOnOpen { get; set; } = true;

        /// <summary>Déclenché après toute opération qui modifie le projet (pour rafraîchir une interface).</summary>
        public event EventHandler? Changed;

        private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

        private static String Name(String path) => Path.GetFileName(path);

        // ------------------------------------------------------------------ nouveau projet

        /// <summary>
        /// <c>NewProject</c> (0x401d04) : vide les directions, remet les options par défaut et recharge <c>Default.act</c>
        /// (depuis <see cref="AppDirectory"/> si présent, sinon depuis la ressource embarquée).
        /// </summary>
        public Boolean NewProject()
        {
            Project.ClearContent();
            Project.Palette = Palette.LoadDefault();
            Project.Palette.FileName = "";
            String actPath = Path.Combine(AppDirectory, "Default.act");
            Boolean ok = !File.Exists(actPath) || LoadPalette(actPath);
            RaiseChanged();
            return ok;
        }

        // ------------------------------------------------------------------ palette / fond

        /// <summary>Charge une palette .ACT/.PAL (0x402ca4). Retourne <c>false</c> et affiche un message en cas d'échec.</summary>
        public Boolean LoadPalette(String path)
        {
            try
            {
                Project.Palette = Palette.LoadFromFile(path);
                RaiseChanged();
                return true;
            }
            catch (UnknownPaletteTypeException e)
            {
                UI.ShowError(e.Message);
            }
            catch (PaletteLoadException e)
            {
                UI.ShowError(e.Message);
            }
            return false;
        }

        /// <summary>Charge l'image de fond (BMP) (0x403230).</summary>
        public Boolean LoadBackground(String path)
        {
            try
            {
                Project.Background = BmpCodec.ReadFile(path);
                Project.BackgroundFile = path;
                RaiseChanged();
                return true;
            }
            catch (Exception e) when (e is IOException or ImageFormatException or UnauthorizedAccessException or ArgumentException)
            {
                Project.Background = null;
                Project.BackgroundFile = "";
                UI.ShowError($"Unable to load background '{Name(path)}'!");
                return false;
            }
        }

        // ------------------------------------------------------------------ images sources

        /// <summary>
        /// <c>LoadFrameFile</c> (0x403f18). Charge une image BMP/GIF/PNG et ajoute la ou les frames à <paramref name="dir"/>.
        /// <paramref name="gifIndex"/> = −1 : toutes les sous‑images d'un GIF. Retourne la dernière frame ajoutée, ou <c>null</c>.
        /// </summary>
        public Frame? LoadFrameFile(Direction dir, String path, Int32 gifIndex = -1)
        {
            String rawExt = Path.GetExtension(path);
            String ext = rawExt.ToUpperInvariant();
            Frame? last = null;
            Int32 current = gifIndex;
            try
            {
                switch (ext)
                {
                    case ".BMP":
                        last = AddFrame(dir, BmpCodec.ReadFile(path), path, Frame.NoSourceIndex);
                        break;
                    case ".PNG":
                        last = AddFrame(dir, PngCodec.ReadFile(path), path, Frame.NoSourceIndex);
                        break;
                    case ".GIF":
                        {
                            var gif = GifCodec.ReadFile(path);
                            var selected = gifIndex < 0
                                ? gif.SubImages
                                : gif.SubImages.Where(s => s.Index == gifIndex).ToList();
                            if (gifIndex >= 0 && selected.Count == 0)
                                throw new ImageFormatException("Image index out of range");
                            foreach (var sub in selected)
                            {
                                current = sub.Index;
                                var frame = AddFrame(dir, sub.Bitmap, path, (UInt16)sub.Index);
                                frame.OffsetX = sub.Left + sub.Width / 2 - gif.ScreenWidth / 2;
                                frame.OffsetY = sub.Top + sub.Height - gif.ScreenHeight;
                                last = frame;
                            }
                            break;
                        }
                    default:
                        UI.ShowError($"Unknown frame type '*{rawExt}'!");
                        return null;
                }
            }
            catch (Exception e) when (e is IOException or ImageFormatException or UnauthorizedAccessException or InvalidDataException or ArgumentException or IndexOutOfRangeException)
            {
                String label = ext == ".GIF" && current >= 0 ? $"{Name(path)}[{current}]" : Name(path);
                UI.ShowError($"Unable to load frame '{label}'!");
                RaiseChanged();
                return null;
            }
            RaiseChanged();
            return last;
        }

        private static Frame AddFrame(Direction dir, Bitmap32 bmp, String path, UInt16 sourceIndex)
        {
            var frame = new Frame(bmp) { FileName = path, SourceIndex = sourceIndex };
            dir.Frames.Add(frame);
            return frame;
        }

        // ------------------------------------------------------------------ projet FPR

        private static Int32 ToInt(String s)
        {
            s = s.Trim();
            if (!Int32.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 v) && !TryParseHex(s, out v))
                throw new FormatException($"'{s}' is not a valid integer value");
            return v;
        }

        private static Boolean TryParseHex(String s, out Int32 v)
        {
            v = 0;
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v))
                return true;
            if (s.StartsWith('$') && Int32.TryParse(s.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v))
                return true;
            return false;
        }

        /// <summary>Parse un entier comme <c>StrToInt</c> de la VCL (décimal, <c>0x…</c> ou <c>$…</c>). Lève <see cref="FormatException"/>.</summary>
        public static Int32 ParseInt(String s) => ToInt(s);

        /// <summary><c>LoadProject</c> (0x4048ec) : charge un fichier .FPR.</summary>
        public Boolean LoadProject(String path)
        {
            Boolean ok = false;
            try
            {
                var kv = KeyValueList.Parse(AnsiText.ReadFile(path));

                Project.Settings.SampleColor = ToInt(kv["TransparentColor"]);
                Project.Settings.TransparentMode = (TransparentMode)(Byte)ToInt(kv["TransparentColorMode"]);
                Project.Settings.AutoCutting = ToInt(kv["AutoCutting"]) != 0;
                Project.Settings.DitherMethod = (DitherMethod)(Byte)ToInt(kv["DitheringMethod"]);
                Project.Settings.Fps = (UInt16)ToInt(kv["FPS"]);
                Project.Settings.ActionFrame = (UInt16)ToInt(kv["ActionFrame"]);

                String pal = kv["Pallete"];
                if (pal.Length > 0 && !LoadPalette(pal))
                    ContinueOrAbort(path);

                String bg = kv["Background"];
                if (bg.Length > 0 && !LoadBackground(bg))
                    ContinueOrAbort(path);

                Project.FrmFile = kv["FRM"];

                for (Int32 d = 0; d < Project.DirectionCount; d++)
                {
                    var dir = Project.Directions[d];
                    String off = kv[$"Offset({d})"];
                    dir.ShiftBaseX = ToInt(Csv.Field(off, 0));
                    dir.ShiftBaseY = ToInt(Csv.Field(off, 1));
                    dir.FirstFrameOffsetX = ToInt(Csv.Field(off, 2));
                    dir.FirstFrameOffsetY = ToInt(Csv.Field(off, 3));

                    for (Int32 f = 0; ; f++)
                    {
                        String line = kv[$"Frame({d},{f})"];
                        if (line.Length == 0)
                            break;
                        String file = Csv.Field(line, 2);
                        String idxText = Csv.Field(line, 3);
                        Int32 gifIndex = idxText.Length > 0 ? ToInt(idxText) : -1;
                        var frame = LoadFrameFile(dir, file, gifIndex);
                        if (frame is not null)
                        {
                            frame.OffsetX = ToInt(Csv.Field(line, 0));
                            frame.OffsetY = ToInt(Csv.Field(line, 1));
                        }
                        else
                            ContinueOrAbort(path);
                    }
                    UI.Progress(d + 1, Project.DirectionCount);
                }
                SetProjectFile(path);
                ok = true;
            }
            catch (OperationAbortedException)
            {
                // choix « Non » : abandon silencieux comme dans l'original
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
            {
                UI.ShowError($"Unable to load project '{Name(path)}'!");
            }
            finally
            {
                UI.Progress(0, 0);
                Project.CurrentDirection = 0;
                RaiseChanged();
            }
            return ok;
        }

        private void ContinueOrAbort(String projectPath)
        {
            if (!UI.Confirm($"Continue loading project '{Name(projectPath)}'?"))
                throw new OperationAbortedException();
        }

        /// <summary>Met à jour le nom du projet courant (<c>0x40c110</c>).</summary>
        public void SetProjectFile(String path)
        {
            Project.ProjectFile = path;
            RaiseChanged();
        }

        /// <summary>
        /// <c>SaveProject</c> (0x4063f0). Écrit d'abord sur disque les images des frames dont le fichier n'existe pas
        /// (nom : <c>base + "%02d%04d" + EXT</c>), puis le fichier FPR. <paramref name="framesBase"/> vide : le nom est demandé.
        /// </summary>
        public Boolean SaveProject(String path, String framesBase = "")
        {
            Boolean ok = false;
            try
            {
                String baseNoExt = "";
                String ext = "";
                Boolean nameResolved = false;

                for (Int32 d = 0; d < Project.DirectionCount; d++)
                {
                    var dir = Project.Directions[d];
                    for (Int32 f = 0; f < dir.Count; f++)
                    {
                        var frame = dir.Frames[f];
                        if (File.Exists(frame.FileName))
                            continue;

                        if (!nameResolved)
                        {
                            if (framesBase.Length == 0)
                            {
                                String suggestion = path;
                                if (suggestion.Length >= 4)
                                    suggestion = suggestion[..^4];
                                framesBase = UI.PromptSaveFramesAs(suggestion) ?? throw new OperationAbortedException();
                            }
                            ext = Path.GetExtension(framesBase).ToUpperInvariant();
                            baseNoExt = framesBase.Length >= 4 ? framesBase[..^4] : framesBase;
                            nameResolved = true;
                        }

                        String file = baseNoExt + (1_000_000 + d * 10_000 + f).ToString()[1..] + ext;
                        frame.FileName = file;
                        switch (ext)
                        {
                            case ".PNG":
                                PngCodec.WriteFile(frame.Bitmap, file);
                                break;
                            case ".GIF":
                                GifCodec.WriteFile(frame.Bitmap, file, Project.Palette);
                                break;
                            case ".BMP":
                                BmpCodec.WriteFile(frame.Bitmap, file);
                                break;
                            default:
                                throw new OperationAbortedException();
                        }
                    }
                }

                var kv = new KeyValueList();
                var s = Project.Settings;
                kv.Add("TransparentColor", s.SampleColor.ToString(CultureInfo.InvariantCulture));
                kv.Add("TransparentColorMode", ((Int32)s.TransparentMode).ToString(CultureInfo.InvariantCulture));
                kv.Add("AutoCutting", s.AutoCutting ? "1" : "0");
                kv.Add("DitheringMethod", ((Int32)s.DitherMethod).ToString(CultureInfo.InvariantCulture));
                kv.Add("FPS", s.Fps.ToString(CultureInfo.InvariantCulture));
                kv.Add("ActionFrame", s.ActionFrame.ToString(CultureInfo.InvariantCulture));
                kv.Add("Pallete", Project.PaletteFile);
                kv.Add("Background", Project.BackgroundFile);
                kv.Add("FRM", Project.FrmFile);
                for (Int32 d = 0; d < Project.DirectionCount; d++)
                {
                    var dir = Project.Directions[d];
                    kv.Add($"Offset({d})", $"{dir.ShiftBaseX},{dir.ShiftBaseY},{dir.FirstFrameOffsetX},{dir.FirstFrameOffsetY}");
                    for (Int32 f = 0; f < dir.Count; f++)
                    {
                        var fr = dir.Frames[f];
                        String index = fr.HasSourceIndex ? "," + fr.SourceIndex.ToString(CultureInfo.InvariantCulture) : "";
                        kv.Add($"Frame({d},{f})", $"{fr.OffsetX},{fr.OffsetY},{fr.FileName}{index}");
                    }
                }
                AnsiText.WriteFile(path, kv.ToString());
                SetProjectFile(path);
                ok = true;
            }
            catch (OperationAbortedException)
            {
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                UI.ShowError($"Unable to write project '{Name(path)}'!");
            }
            finally
            {
                RaiseChanged();
            }
            return ok;
        }

        // ------------------------------------------------------------------ FRM

        /// <summary>
        /// <c>LoadFRM</c> (0x405a28) : charge un .FRM ou un jeu .FR0‑FR5 comme projet. Impose le mode de transparence
        /// « Palette », sans auto‑coupe, méthode de tramage « Default ».
        /// </summary>
        public Boolean LoadFrm(String path)
        {
            Project.Settings.TransparentMode = TransparentMode.Palette;
            Project.Settings.AutoCutting = false;
            Project.Settings.DitherMethod = DitherMethod.Default;

            String rawExt = Path.GetExtension(path);
            String ext = rawExt.ToUpperInvariant();
            Boolean ok = false;
            try
            {
                if (ext == ".FRM")
                {
                    if (!File.Exists(path) || new FileInfo(path).Length < FrmFormat.HeaderSize)
                    {
                        UI.ShowError($"Unable to load FRM '{Name(path)}'!");
                        return false;
                    }
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    ReadFrmStream(path, fs, singleDirection: null);
                    SetProjectFile(path);
                    ok = true;
                }
                else if (ext.Length >= 3 && ext[..3] == ".FR")
                {
                    for (Int32 d = 0; d < Project.DirectionCount; d++)
                    {
                        String file = Path.ChangeExtension(path, ".FR" + d);
                        if (!File.Exists(file) || new FileInfo(file).Length < FrmFormat.HeaderSize)
                        {
                            UI.ShowError($"Unable to load FRM '{Name(file)}'!");
                            continue;
                        }
                        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                        ReadFrmStream(path, fs, singleDirection: d);
                        SetProjectFile(path);
                        ok = true;
                    }
                }
                else
                {
                    UI.ShowError($"Unknown FRM type '*{rawExt}'!");
                }
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OverflowException)
            {
                UI.ShowError($"Unable to load FRM '{Name(path)}'!");
            }
            finally
            {
                UI.Progress(0, 0);
                RaiseChanged();
            }
            return ok;
        }

        private void ReadFrmStream(String chosenPath, Stream fs, Int32? singleDirection)
        {
            var h = FrmFormat.ReadHeader(fs);
            Project.Settings.Fps = h.Fps;
            Project.Settings.ActionFrame = h.ActionFrame;
            Int32 count = h.FramesPerDirection;

            for (Int32 d = 0; d < Project.DirectionCount; d++)
            {
                if (singleDirection is Int32 only)
                {
                    if (d != only)
                        continue;
                }
                else if (!(h.DataOffset[d] > 0 || d == 0))
                    continue;

                var dir = Project.Directions[d];
                for (Int32 f = 0; f < count; f++)
                {
                    var frame = FrmFormat.ReadFrame(Project, fs, dir, out Int32 rx, out Int32 ry);
                    frame.FileName = FrmFormat.FrameName(chosenPath, d, f);
                    frame.SourceIndex = Frame.NoSourceIndex;
                    if (f == 0)
                    {
                        dir.FirstFrameOffsetX = rx;
                        dir.FirstFrameOffsetY = ry;
                    }
                    else
                        FrameMath.SetRelative(dir.Frames[f - 1], frame, rx, ry);
                    UI.Progress(f + 1, count);
                }
                // l'original positionne le décalage de direction après chargement de la dernière frame
                if (dir.Count > 0)
                {
                    FrameMath.SetShift(dir, singleDirection is null ? h.ShiftX[d] : h.ShiftX[0], singleDirection is null ? h.ShiftY[d] : h.ShiftY[0]);
                }
            }
        }

        /// <summary>
        /// <c>SaveFRM</c> (0x407b18) : écrit un <c>.FRM</c> ou un jeu <c>.FR0…FR5</c> (seules les extensions .FRM et .FR0 sont
        /// acceptées). Retourne <c>false</c> avec message en cas d'échec.
        /// </summary>
        public Boolean SaveFrm(String path)
        {
            if (Project.Settings.DitherMethod == DitherMethod.Fallout && Project.Palette.FalloutTable is null)
            {
                UI.ShowError($"Invalid palette type for dithering method 'Fallout'\nUnable to write FRM '{Name(path)}'!");
                return false;
            }

            var layout = FrmFormat.Prepare(Project);
            String rawExt = Path.GetExtension(path);
            String ext = rawExt.ToUpperInvariant();

            if (ext == ".FRM")
            {
                if (!FrmFormat.IsValidForFrm(Project))
                {
                    UI.ShowError($"Incomplete frames set for '{Name(path)}'!");
                    return false;
                }
                try
                {
                    using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                    FrmFormat.WriteFrm(Project, layout, fs);
                    return true;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    UI.ShowError($"Unable to write FRM '{Name(path)}'!");
                    return false;
                }
            }

            if (ext == ".FR0")
            {
                if (!FrmFormat.IsValidForFrN(Project))
                {
                    UI.ShowError($"Incomplete frames set for '{Name(path)}'!");
                    return false;
                }
                for (Int32 d = 0; d < Project.DirectionCount; d++)
                {
                    String file = Path.ChangeExtension(path, ".FR" + d);
                    try
                    {
                        using var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
                        FrmFormat.WriteFrDirection(Project, layout, d, fs);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        UI.ShowError($"Unable to write FRM '{Name(file)}'!");
                        return false;
                    }
                    UI.Progress(d + 1, Project.DirectionCount);
                }
                UI.Progress(0, 0);
                return true;
            }

            UI.ShowError($"Unknown FRM file type '*{rawExt}'!");
            return false;
        }

        /// <summary>
        /// Action « Open Project » (<c>acOpen</c> 0x40c7e8) : nouveau projet puis ouverture d'un .FPR, ou d'un FRM / FR0‑FR5
        /// (palette homonyme .PAL si elle se charge, sinon question « Use palette '…'? » puis éventuellement choix d'une palette).
        /// </summary>
        public Boolean OpenFile(String path)
        {
            NewProject();
            String rawExt = Path.GetExtension(path);
            String ext = rawExt.ToUpperInvariant();
            if (ext == ".FPR")
                return LoadProject(path);

            if (ext.Length >= 3 && ext[..3] == ".FR")
            {
                String sibling = PathHelpers.ChangeFileExt(path, ".PAL");
                if (!(File.Exists(sibling) && LoadPalette(sibling)))
                {
                    String current = String.IsNullOrEmpty(Project.PaletteFile) ? "Default.act" : Name(Project.PaletteFile);
                    if (ConfirmPaletteOnOpen && !UI.Confirm($"Use palette '{current}'?"))
                    {
                        String? chosen = UI.PromptPalette();
                        if (chosen is null || !LoadPalette(chosen))
                            return false;
                    }
                }
                return LoadFrm(path);
            }

            UI.ShowError($"Unknown file type '*{rawExt}'!");
            return false;
        }

        /// <summary>Action « Create FRM » : mémorise le chemin (clé <c>FRM=</c> du FPR) puis écrit le fichier.</summary>
        public Boolean CreateFrm(String path)
        {
            Project.FrmFile = path;
            return SaveFrm(path);
        }
    }
}