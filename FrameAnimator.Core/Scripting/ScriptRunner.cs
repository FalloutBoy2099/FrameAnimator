namespace FrameAnimator.Core.Scripting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Formats;
    using Geometry;
    using Model;
    using Services;

    /// <summary>Script error kinds (codes 1 to 5 of the original).</summary>
    public enum ScriptErrorKind
    {
        IndexOutOfRange = 1,
        ParameterRequired = 2,
        UnknownKeyword = 3,
        InvalidParameter = 4,
        OperationFailed = 5,
    }

    internal sealed class ScriptException(ScriptErrorKind kind, String text = "") : Exception
    {
        public ScriptErrorKind Kind { get; } = kind;
        public String Text { get; } = text;
    }

    /// <summary>
    /// Interpreter for <c>.FAS</c> scripts (0x4088bc). One command per line (<c>WORD,param,param…</c>), keyword
    /// case-insensitive. On a line error: message, then the question "Continue executing script '…'?"; "No" stops the script.
    /// </summary>
    public sealed class ScriptRunner(ProjectService service)
    {
        public ProjectService Service { get; } = service;
        private Project Project => Service.Project;
        private IUserInteraction UI => Service.UI;

        /// <summary>If <c>true</c> (default, as in the original), the current directory becomes the script's directory during execution.</summary>
        public Boolean ChangeCurrentDirectory { get; set; } = true;

        /// <summary>Skip blank lines (the original reports them as "Unknown keyword ''").</summary>
        public Boolean SkipBlankLines { get; set; } = true;

        /// <summary>Diagnostic: unexpected exception that interrupted the script (the original only shows "Unable to execute script").</summary>
        public Action<Exception>? UnexpectedException { get; set; }

        private Int32 _paramIndex;
        private String _lastText = "";

        /// <summary>Runs the script. Returns <c>true</c> if all lines were processed (<c>false</c> if stopped or unreadable).</summary>
        public Boolean Run(String scriptPath)
        {
            List<String> lines;
            try
            {
                lines = KeyValueListLines(AnsiText.ReadFile(scriptPath));
                if (ChangeCurrentDirectory)
                {
                    String dir = PathHelpers.ExtractFileDir(Path.GetFullPath(scriptPath));
                    if (dir.Length > 0 && Directory.Exists(dir))
                        Directory.SetCurrentDirectory(dir);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                UI.ShowError($"Unable to execute script '{PathHelpers.ExtractFileName(scriptPath)}'!");
                return false;
            }

            try
            {
                for (Int32 i = 0; i < lines.Count; i++)
                {
                    if (SkipBlankLines && lines[i].Trim().Length == 0)
                        continue;
                    if (!RunLine(lines[i], i, scriptPath))
                        return false;
                }
                return true;
            }
            catch (OperationAbortedException)
            {
                return false;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                UnexpectedException?.Invoke(e);
                UI.ShowError($"Unable to execute script '{PathHelpers.ExtractFileName(scriptPath)}'!");
                return false;
            }
        }

        private static List<String> KeyValueListLines(String text)
        {
            var l = new List<String>();
            using var r = new StringReader(text);
            String? s;
            while ((s = r.ReadLine()) is not null)
                l.Add(s);
            return l;
        }

        /// <summary>Runs one line; returns <c>false</c> if the user chooses to stop the script.</summary>
        private Boolean RunLine(String line, Int32 lineIndex, String scriptPath)
        {
            _paramIndex = 0;
            _lastText = "";
            try
            {
                Execute(line);
                return true;
            }
            catch (ScriptException e)
            {
                if (e.Kind != ScriptErrorKind.OperationFailed)
                {
                    String text = e.Kind switch
                    {
                        ScriptErrorKind.IndexOutOfRange => $"Index '{e.Text}' out of range",
                        ScriptErrorKind.ParameterRequired => "Parameter is required",
                        ScriptErrorKind.UnknownKeyword => $"Unknown keyword '{e.Text}'",
                        _ => $"Invalid parameter '{e.Text}'",
                    };
                    UI.ShowError($"{text}\nin line {lineIndex + 1}, parameter {_paramIndex + 1}");
                }
                return AskContinue(scriptPath);
            }
            catch (FormatException)
            {
                UI.ShowError($"'{_lastText}' is not a valid integer value\nin line {lineIndex + 1}, parameter {_paramIndex + 1}");
                return AskContinue(scriptPath);
            }
        }

        private Boolean AskContinue(String scriptPath) => UI.Confirm($"Continue executing script '{PathHelpers.ExtractFileName(scriptPath)}'?");

        // ------------------------------------------------------------------ parameter extraction

        private String Field(String line, Int32 n)
        {
            _paramIndex = n;
            String s = Csv.Field(line, n);
            _lastText = s;
            return s;
        }

        private String Required(String line, Int32 n)
        {
            String s = Field(line, n);
            if (s.Length == 0)
                throw new ScriptException(ScriptErrorKind.ParameterRequired);
            return s;
        }

        private Int32 Int(String line, Int32 n) => ProjectService.ParseInt(Required(line, n));

        private Int32 DirectionParam(String line, Int32 n)
        {
            Int32 d = Int(line, n);
            if (d < 0 || d > 5)
                throw new ScriptException(ScriptErrorKind.IndexOutOfRange, _lastText);
            return d;
        }

        private Int32 FrameParam(String line, Int32 n, Direction dir)
        {
            Int32 f = Int(line, n);
            if (f < 0)
                f += dir.Count;
            if (f < 0 || f >= dir.Count)
                throw new ScriptException(ScriptErrorKind.IndexOutOfRange, _lastText);
            return f;
        }

        private static void Fail() => throw new ScriptException(ScriptErrorKind.OperationFailed);

        // ------------------------------------------------------------------ commands

        private void Execute(String line)
        {
            String keyword = Field(line, 0).ToUpperInvariant();
            _lastText = keyword;
            switch (keyword)
            {
                case "SET_FRAME_OFFSET":
                    SetFrameOffset(line);
                    break;
                case "SET_FRAMES_OFFSET":
                    SetFramesOffset(line);
                    break;
                case "ADD_FRAMES":
                    AddFrames(line);
                    break;
                case "MOVE_FRAME":
                    MoveFrame(line);
                    break;
                case "REMOVE_FRAMES":
                    RemoveFrames(line);
                    break;
                case "LOAD_PALETTE":
                    if (!Service.LoadPalette(Required(line, 1)))
                        Fail();
                    break;
                case "SET_SAMPLE_COLOR":
                    SetSampleColor(line);
                    break;
                case "SET_OPTION":
                    SetOption(line);
                    break;
                case "NEW":
                    if (!Service.NewProject())
                        Fail();
                    break;
                case "OPEN":
                    Open(line);
                    break;
                case "RUN_SCRIPT":
                    if (!new ScriptRunner(Service) { ChangeCurrentDirectory = ChangeCurrentDirectory, SkipBlankLines = SkipBlankLines }.Run(Required(line, 1)))
                        Fail();
                    break;
                case "SAVE":
                    Save(line);
                    break;
                default:
                    throw new ScriptException(ScriptErrorKind.UnknownKeyword, keyword);
            }
        }

        private void SetFrameOffset(String line)
        {
            Int32 d = DirectionParam(line, 1);
            var dir = Project.Directions[d];
            Int32 f = FrameParam(line, 2, dir);
            Int32 x = Int(line, 3);
            Int32 y = Int(line, 4);
            Boolean inherit = false;
            String inh = Field(line, 5);
            if (inh.Length > 0)
                inherit = ProjectService.ParseInt(inh) != 0;

            if (f > 0)
            {
                var prev = dir.Frames[f - 1];
                var cur = dir.Frames[f];
                Int32 oldX = cur.OffsetX, oldY = cur.OffsetY;
                FrameMath.AutoCut(Project, prev);
                FrameMath.AutoCut(Project, cur);
                FrameMath.SetRelative(prev, cur, x, y);
                if (inherit)
                {
                    Int32 dx = cur.OffsetX - oldX, dy = cur.OffsetY - oldY;
                    for (Int32 i = f + 1; i < dir.Count; i++)
                    {
                        dir.Frames[i].OffsetX += dx;
                        dir.Frames[i].OffsetY += dy;
                    }
                }
            }
            else
            {
                dir.FirstFrameOffsetX = x;
                dir.FirstFrameOffsetY = y;
            }
        }

        private void SetFramesOffset(String line)
        {
            Int32 d = DirectionParam(line, 1);
            Int32 x = Int(line, 2);
            Int32 y = Int(line, 3);
            var dir = Project.Directions[d];
            if (dir.Count == 0)
                throw new InvalidOperationException("List index out of bounds (0)");
            FrameMath.AutoCut(Project, dir.Frames[0]);
            FrameMath.SetShift(dir, x, y);
        }

        private void AddFrames(String line)
        {
            Int32 d = DirectionParam(line, 1);
            String mask = Required(line, 2);
            foreach (String file in PathHelpers.FindFiles(mask))
            {
                if (Service.LoadFrameFile(Project.Directions[d], file, -1) is null)
                    Fail();
            }
        }

        private void MoveFrame(String line)
        {
            Int32 d = DirectionParam(line, 1);
            var dir = Project.Directions[d];
            Int32 cur = FrameParam(line, 2, dir);
            Int32 next = FrameParam(line, 3, dir);
            var f = dir.Frames[cur];
            dir.Frames.RemoveAt(cur);
            dir.Frames.Insert(next, f);
        }

        private void RemoveFrames(String line)
        {
            String dirText = Field(line, 1);
            if (dirText.Length == 0)
            {
                foreach (var dir in Project.Directions)
                    dir.ClearFrames();
                return;
            }
            Int32 d = ProjectService.ParseInt(dirText);
            if (d < 0 || d > 5)
                throw new ScriptException(ScriptErrorKind.IndexOutOfRange, _lastText);
            var direction = Project.Directions[d];

            String f1Text = Field(line, 2);
            if (f1Text.Length == 0)
            {
                direction.ClearFrames();
                return;
            }
            Int32 f1 = ProjectService.ParseInt(f1Text);
            if (f1 < 0)
                f1 += direction.Count;
            if (f1 < 0 || f1 >= direction.Count)
                throw new ScriptException(ScriptErrorKind.IndexOutOfRange, _lastText);

            String f2Text = Field(line, 3);
            if (f2Text.Length == 0)
            {
                direction.Frames.RemoveAt(f1);
                return;
            }
            Int32 f2 = ProjectService.ParseInt(f2Text);
            if (f2 < 0)
                f2 += direction.Count;
            if (f2 < 0 || f2 >= direction.Count)
                throw new ScriptException(ScriptErrorKind.IndexOutOfRange, _lastText);
            for (Int32 i = f1; i <= f2; i++)
                direction.Frames.RemoveAt(f1);
        }

        private void SetSampleColor(String line)
        {
            Int32 v = Int(line, 1);
            if ((UInt32)v > 0xFFFFFF)
                throw new ScriptException(ScriptErrorKind.InvalidParameter, _lastText);
            Project.Settings.SampleColor = v;
        }

        private void SetOption(String line)
        {
            String name = Field(line, 1).ToUpperInvariant();
            _lastText = name;
            if (name.Length == 0)
                throw new ScriptException(ScriptErrorKind.ParameterRequired);
            switch (name)
            {
                case "TRANSP_MODE":
                    {
                        Byte v = (Byte)Int(line, 2);
                        if (v is not (0 or 1 or 3))
                            throw new ScriptException(ScriptErrorKind.InvalidParameter, _lastText);
                        Project.Settings.TransparentMode = (TransparentMode)v;
                        break;
                    }
                case "AUTOCUTTING":
                    Project.Settings.AutoCutting = Int(line, 2) != 0;
                    break;
                case "DITHER_METHOD":
                    {
                        Byte v = (Byte)Int(line, 2);
                        if (v is not (0 or 1 or 2))
                            throw new ScriptException(ScriptErrorKind.InvalidParameter, _lastText);
                        Project.Settings.DitherMethod = (DitherMethod)v;
                        break;
                    }
                case "ACTION_FRAME":
                    Project.Settings.ActionFrame = (UInt16)Int(line, 2);
                    break;
                case "FPS":
                    Project.Settings.Fps = (UInt16)Int(line, 2);
                    break;
                default:
                    throw new ScriptException(ScriptErrorKind.UnknownKeyword, name);
            }
        }

        private void Open(String line)
        {
            Service.NewProject();
            String source = Required(line, 1);
            String rawExt = PathHelpers.ExtractFileExt(source);
            String ext = rawExt.ToUpperInvariant();
            if (ext == ".FPR")
            {
                if (!Service.LoadProject(source))
                    Fail();
            }
            else if (ext.Length >= 3 && ext[..3] == ".FR")
            {
                String palette = Field(line, 2);
                if (palette.Length == 0)
                {
                    String sibling = PathHelpers.ChangeFileExt(source, ".PAL");
                    if (File.Exists(sibling) && !Service.LoadPalette(sibling))
                        Fail();
                }
                else if (!Service.LoadPalette(palette))
                    Fail();
                if (!Service.LoadFrm(source))
                    Fail();
            }
            else
                throw new ScriptException(ScriptErrorKind.InvalidParameter, source);
        }

        private void Save(String line)
        {
            String dest = Field(line, 1);
            if (dest.Length > 0)
            {
                String name = PathHelpers.ExtractFileName(dest);
                if (name.Length == 0)
                    throw new ScriptException(ScriptErrorKind.InvalidParameter, dest);
                String path = PathHelpers.ExtractFilePath(dest);
                if (path.Length == 0 && Project.ProjectFile.Length == 0)
                    path = PathHelpers.ExtractFilePath(Path.Combine(Service.AppDirectory, "x"));

                if (NameMask.HasWildcards(name))
                {
                    if (Project.ProjectFile.Length == 0)
                        throw new ScriptException(ScriptErrorKind.InvalidParameter, name);
                    name = NameMask.Apply(PathHelpers.ExtractFileName(Project.ProjectFile), name);
                }
                dest = path + name;
            }
            else
            {
                if (Project.ProjectFile.Length == 0)
                    throw new ScriptException(ScriptErrorKind.ParameterRequired);
                dest = Project.ProjectFile;
            }

            _lastText = dest;
            String rawExt = PathHelpers.ExtractFileExt(dest);
            String ext = rawExt.ToUpperInvariant();
            if (ext == ".FPR")
            {
                String format = Field(line, 2);
                if (format.Length == 0)
                    format = "BMP";
                String baseName = dest.Length >= 4 ? dest[..^4] : dest;
                if (!Service.SaveProject(dest, baseName + "." + format))
                    Fail();
            }
            else if (ext is ".FRM" or ".FR0")
            {
                if (!Service.SaveFrm(dest))
                    Fail();
            }
            else
                throw new ScriptException(ScriptErrorKind.InvalidParameter, dest);
        }
    }
}