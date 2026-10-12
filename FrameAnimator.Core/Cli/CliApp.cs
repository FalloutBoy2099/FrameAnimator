namespace FrameAnimator.Core.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Formats;
    using Imaging;
    using Model;
    using Scripting;
    using Services;

    /// <summary>
    /// Console version of Frame Animator: conversion, script execution and automation (<c>File &gt; Automate</c>) from
    /// the command line, plus a few diagnostic tools. Help is available with <c>/?</c>.
    /// The GUI executable calls it as soon as its command line is a command (<see cref="IsConsoleInvocation"/>).
    /// </summary>
    public sealed class CliApp(TextWriter output, TextWriter error, String language, String exe)
    {
        public const Int32 Success = 0;
        public const Int32 UsageError = 1;
        public const Int32 UnexpectedError = 2;
        public const Int32 OperationFailed = 3;

        private static readonly String[] SourceExtensions = [".FPR", ".FRM", ".FR0", ".FR1", ".FR2", ".FR3", ".FR4", ".FR5"];
        private static readonly String[] FrameFormats = ["BMP", "PNG", "GIF"];
        private static readonly String[] ImageExtensions = [".BMP", ".PNG", ".GIF"];

        private static readonly String[] Commands = ["convert", "image", "script", "run", "automate", "info", "decode", "encode", "help"];

        /// <summary>
        /// Indicates whether the command line requests console mode (command, <c>/?</c>, <c>--help</c>, <c>--version</c>…) rather
        /// than opening the window (no arguments, or a file to open).
        /// </summary>
        public static Boolean IsConsoleInvocation(IReadOnlyList<String> args)
            => args.Count > 0 && (Commands.Contains(args[0], StringComparer.OrdinalIgnoreCase) || CommandLine.IsHelpOrVersion(args[0]));

        /// <summary>True if the command line requests help or the version (console mode then has nothing else to do).</summary>
        public static Boolean IsHelpRequest(IReadOnlyList<String> args)
            => args.Count > 0 && (args[0].Equals("help", StringComparison.OrdinalIgnoreCase) || args.Any(CommandLine.IsHelpOrVersion));

        private static String Version => Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "?";

        private String T(String english, String french) => language == "fr" ? french : english;

        /// <summary>Executes the command line and returns the exit code.</summary>
        public Int32 Execute(String[] args)
        {
            ParsedCommandLine line;
            try
            {
                line = CommandLine.Parse(args);
            }
            catch (CommandLineException e)
            {
                error.WriteLine("ERROR: " + e.Message);
                error.WriteLine(T($"Use '{exe} /?' for help.", $"Utilisez '{exe} /?' pour l'aide."));
                return UsageError;
            }

            String command = line.Command == "run" ? "script" : line.Command;
            if (line.Has("version"))
            {
                output.WriteLine(Version);
                return Success;
            }
            if (args.Length == 0 || command == "" && !line.Has("help"))
            {
                output.WriteLine(HelpText.Get("", language, exe, Version));
                return UsageError;
            }
            if (line.Has("help") || command == "help")
            {
                String topic = command == "help" ? (line.Positional.Count > 0 ? line.Positional[0].ToLowerInvariant() : "") : command;
                output.WriteLine(HelpText.Get(topic == "run" ? "script" : topic, language, exe, Version));
                return Success;
            }

            try
            {
                return command switch
                {
                    "convert" => Convert(line),
                    "image" => ConvertImages(line),
                    "script" => RunScript(line),
                    "automate" => Automate(line),
                    "info" => Info(line),
                    "decode" => Decode(line),
                    "encode" => Encode(line),
                    _ => throw new CommandLineException($"Unknown command '{line.Command}'."),
                };
            }
            catch (CommandLineException e)
            {
                error.WriteLine("ERROR: " + e.Message);
                String helpTarget = command is "convert" or "image" or "script" or "automate" or "info" or "decode" or "encode" ? command + " " : "";
                error.WriteLine(T($"Use '{exe} {helpTarget}/?' for help.", $"Utilisez '{exe} {helpTarget}/?' pour l'aide."));
                return UsageError;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ImageFormatException or InvalidDataException or PaletteLoadException or UnknownPaletteTypeException)
            {
                error.WriteLine("ERROR: " + e.Message);
                return OperationFailed;
            }
            catch (Exception e)
            {
                error.WriteLine("FATAL: " + e.Message);
                return UnexpectedError;
            }
        }

        // ------------------------------------------------------------------ options

        private static void ValidateOptions(ParsedCommandLine line, params String[] allowed)
        {
            foreach (String name in line.OptionNames)
            {
                if (!allowed.Contains(name, StringComparer.OrdinalIgnoreCase))
                    throw new CommandLineException($"Option '/{name}' is not valid for '{line.Command}'.");
            }
        }

        private static String ParseFrames(ParsedCommandLine line)
        {
            String frames = (line.Get("frames") ?? "BMP").ToUpperInvariant();
            if (!FrameFormats.Contains(frames))
                throw new CommandLineException($"Invalid frame format '{frames}': use BMP, PNG or GIF.");
            return frames;
        }

        private static AutomationFormat? ParseFormat(ParsedCommandLine line)
        {
            String? text = line.Get("format");
            if (text is null)
                return null;
            return text.ToLowerInvariant() switch
            {
                "fpr" or "project" or "fpr-project" => AutomationFormat.FprProject,
                "frm" => AutomationFormat.Frm,
                "fr" or "fr0" or "fr0-fr5" or "frn" => AutomationFormat.FrN,
                _ => throw new CommandLineException($"Invalid format '{text}': use fpr, frm or fr."),
            };
        }

        private static String Extension(AutomationFormat format) => format switch
        {
            AutomationFormat.FprProject => ".FPR",
            AutomationFormat.Frm => ".FRM",
            _ => ".FR0",
        };

        private static AutomationFormat? FormatFromExtension(String extension) => extension.ToUpperInvariant() switch
        {
            ".FPR" => AutomationFormat.FprProject,
            ".FRM" => AutomationFormat.Frm,
            ".FR0" => AutomationFormat.FrN,
            _ => null,
        };

        private static Boolean IsFolder(String path) => Directory.Exists(path) || path.EndsWith('\\') || path.EndsWith('/');

        /// <summary>
        /// Destination mask and format derived from <c>/dest</c> (folder or mask) and <c>/format</c>. Without a folder in the
        /// mask, the result is written next to each source (behavior of <c>File &gt; Automate</c>).
        /// </summary>
        internal static (String Mask, AutomationFormat Format) BuildMask(String? dest, AutomationFormat? format, Boolean replaceExtension)
        {
            if (dest is null)
            {
                if (format is null)
                    throw new CommandLineException("Specify the destination format with /to (or a destination with /out or /dest).");
                return ("*" + Extension(format.Value), format.Value);
            }

            if (IsFolder(dest))
            {
                if (format is null)
                    throw new CommandLineException("Specify the destination format with /to or /format.");
                String folder = Path.GetFullPath(dest);
                Directory.CreateDirectory(folder);
                return (Path.Combine(folder, "*" + Extension(format.Value)), format.Value);
            }

            String name = Path.GetFileName(dest);
            String directory = Path.GetDirectoryName(dest) ?? "";
            String extension = Path.GetExtension(name);
            AutomationFormat resolved;
            if (format is not null && (extension.Length == 0 || replaceExtension))
            {
                // the mask extension follows the chosen format, like the "Destination format" list of the Automate dialog
                name = PathHelpers.ChangeFileExt(name, Extension(format.Value));
                resolved = format.Value;
            }
            else
            {
                AutomationFormat? fromName = FormatFromExtension(extension);
                if (fromName is null)
                    throw new CommandLineException($"Cannot tell the destination format from '{name}': use .fpr, .frm or .fr0, or /format.");
                if (format is not null && format != fromName)
                    throw new CommandLineException($"The destination format '{format}' does not match the extension of '{name}'.");
                resolved = fromName.Value;
            }

            String mask = directory.Length == 0 ? name : Path.Combine(Path.GetFullPath(directory), name);
            return (mask, resolved);
        }

        // ------------------------------------------------------------------ sources

        /// <summary>
        /// Expands wildcards in the sources (the Windows command interpreter does not do it) and checks their extension.
        /// Paths are made absolute: a script run during processing changes the current folder.
        /// </summary>
        private List<String> ExpandSources(IEnumerable<String> patterns, String[] extensions, out Int32 problems)
        {
            List<String> sources = [];
            problems = 0;
            foreach (String pattern in patterns)
            {
                String fileName = Path.GetFileName(pattern);
                List<String> found;
                if (fileName.Contains('*') || fileName.Contains('?'))
                {
                    found = PathHelpers.FindFiles(pattern).Select(Path.GetFullPath).ToList();
                    if (found.Count == 0)
                    {
                        error.WriteLine($"ERROR: No file matches '{pattern}'.");
                        problems++;
                        continue;
                    }
                    found = found.Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)).ToList();
                }
                else
                {
                    String full = Path.GetFullPath(pattern);
                    if (!File.Exists(full))
                    {
                        error.WriteLine($"ERROR: File not found: {pattern}");
                        problems++;
                        continue;
                    }
                    if (!extensions.Contains(Path.GetExtension(full), StringComparer.OrdinalIgnoreCase))
                    {
                        error.WriteLine($"ERROR: Unsupported source type '{Path.GetExtension(full)}': {pattern}");
                        problems++;
                        continue;
                    }
                    found = [full];
                }

                foreach (String file in found)
                {
                    if (!sources.Contains(file, StringComparer.OrdinalIgnoreCase))
                        sources.Add(file);
                }
            }
            return sources;
        }

        private ConsoleInteraction NewInteraction(ParsedCommandLine line)
            => new ConsoleInteraction(error, line.Has("continue"), ParseFrames(line), language);

        private static ProjectService NewService(ConsoleInteraction ui)
        {
            ProjectService service = new ProjectService(new Project(), ui) { ConfirmPaletteOnOpen = false };
            service.NewProject();
            return service;
        }

        /// <summary>Absolute path of a file given as an option; <c>null</c> (with a message) if it does not exist.</summary>
        private String? ExistingFile(String path, String what)
        {
            String full = Path.GetFullPath(path);
            if (File.Exists(full))
                return full;
            error.WriteLine($"ERROR: {what} not found: {path}");
            return null;
        }

        // ------------------------------------------------------------------ convert / automate

        private Int32 Convert(ParsedCommandLine line)
        {
            ValidateOptions(line, "format", "dest", "palette", "frames", "continue", "quiet", "relative");
            if (line.Positional.Count == 0)
                throw new CommandLineException("No source file: give at least one FPR, FRM or FR0-FR5 file.");
            (String mask, AutomationFormat format) = BuildMask(line.Get("dest"), ParseFormat(line), replaceExtension: false);
            return RunBatch(line, line.Positional, mask, format, script: null);
        }

        private Int32 Automate(ParsedCommandLine line)
        {
            ValidateOptions(line, "format", "dest", "source", "palette", "script", "frames", "continue", "quiet", "relative");
            List<String> patterns = [.. line.Positional, .. line.GetAll("source")];
            if (patterns.Count == 0)
                throw new CommandLineException("No source file: give at least one FPR, FRM or FR0-FR5 file.");
            (String mask, AutomationFormat format) = BuildMask(line.Get("dest"), ParseFormat(line), replaceExtension: true);
            return RunBatch(line, patterns, mask, format, line.Get("script"));
        }

        private Int32 RunBatch(ParsedCommandLine line, IEnumerable<String> patterns, String mask, AutomationFormat format, String? script)
        {
            Boolean quiet = line.Has("quiet");
            String frames = ParseFrames(line);

            String? palette = null;
            if (line.Get("palette") is { } paletteOption && (palette = ExistingFile(paletteOption, "Palette")) is null)
                return OperationFailed;
            String? scriptPath = null;
            if (script is not null && (scriptPath = ExistingFile(script, "Script")) is null)
                return OperationFailed;

            List<String> sources = ExpandSources(patterns, SourceExtensions, out Int32 problems);
            if (sources.Count == 0)
            {
                error.WriteLine("ERROR: Nothing to do.");
                return OperationFailed;
            }

            ConsoleInteraction ui = NewInteraction(line);
            ProjectService service = NewService(ui);
            service.SaveRelativePaths = line.Has("relative");
            AutomationRunner runner = new AutomationRunner(service);

            Int32 failed = problems;
            String folder = Directory.GetCurrentDirectory();
            try
            {
                for (Int32 i = 0; i < sources.Count; i++)
                {
                    AutomationOptions options = new AutomationOptions
                    {
                        Format = format,
                        DestinationMask = mask,
                        UsePalette = palette is not null,
                        PalettePath = palette ?? "",
                        UseScript = scriptPath is not null,
                        ScriptPath = scriptPath ?? "",
                        FrameExtension = frames,
                    };
                    options.Sources.Add(sources[i]);

                    Int32 errorsBefore = ui.ErrorCount;
                    Boolean completed = runner.Run(options);
                    Boolean ok = completed && ui.ErrorCount == errorsBefore;
                    if (!ok)
                        failed++;
                    if (!quiet)
                        output.WriteLine($"[{i + 1}/{sources.Count}] {sources[i]} ... {(ok ? "OK" : "FAILED")}");
                    if (!completed)
                        break;
                }
            }
            finally
            {
                // a script run during processing may have changed the current folder
                Directory.SetCurrentDirectory(folder);
            }

            if (!quiet)
                output.WriteLine(T($"{sources.Count} file(s) processed, {failed} error(s).", $"{sources.Count} fichier(s) traité(s), {failed} erreur(s)."));
            return failed > 0 ? OperationFailed : Success;
        }


        // ------------------------------------------------------------------ images

        private static String ParseImageExtension(String text) => text.ToLowerInvariant().TrimStart('.') switch
        {
            "bmp" => ".bmp",
            "png" => ".png",
            "gif" => ".gif",
            _ => throw new CommandLineException($"Invalid image format '{text}': use bmp, png or gif."),
        };

        /// <summary>Writes an image in the format given by the file extension (a GIF is reduced to the given palette).</summary>
        private static void WriteImage(Bitmap32 bitmap, String path, Palette palette)
        {
            switch (Path.GetExtension(path).ToUpperInvariant())
            {
                case ".PNG":
                    PngCodec.WriteFile(bitmap, path);
                    break;
                case ".GIF":
                    GifCodec.WriteFile(bitmap, path, palette);
                    break;
                default:
                    BmpCodec.WriteFile(bitmap, path);
                    break;
            }
        }

        /// <summary>
        /// <c>image</c> command: converts BMP / PNG / GIF images to another format. An animated GIF yields one file per
        /// sub-image (<c>name_0</c>, <c>name_1</c>…). Destination: next to the source, in a folder, or under a mask
        /// (<c>*</c> and <c>?</c> take over the source name).
        /// </summary>
        private Int32 ConvertImages(ParsedCommandLine line)
        {
            ValidateOptions(line, "format", "dest", "palette", "continue", "quiet");
            if (line.Positional.Count == 0)
                throw new CommandLineException("No source image: give at least one BMP, PNG or GIF file.");

            String? target = line.Get("format") is { } format ? ParseImageExtension(format) : null;
            String? destinationFolder = null;
            String? destinationName = null;
            if (line.Get("dest") is { } dest)
            {
                if (IsFolder(dest))
                    destinationFolder = Path.GetFullPath(dest);
                else
                {
                    destinationName = Path.GetFileName(dest);
                    if (Path.GetDirectoryName(dest) is { Length: > 0 } directory)
                        destinationFolder = Path.GetFullPath(directory);
                    String extension = Path.GetExtension(destinationName);
                    if (extension.Length == 0)
                    {
                        if (target is null)
                            throw new CommandLineException("Specify the image format with /to (or an extension in /out).");
                        destinationName += target;
                    }
                    else
                    {
                        String fromName = ParseImageExtension(extension);
                        if (target is not null && target != fromName)
                            throw new CommandLineException($"The format '{target[1..]}' does not match the extension of '{destinationName}'.");
                        target = fromName;
                    }
                }
                if (destinationFolder is not null)
                    Directory.CreateDirectory(destinationFolder);
            }
            if (target is null)
                throw new CommandLineException("Specify the image format with /to (or give an extension in /out).");

            Palette palette = Palette.LoadDefault();
            if (line.Get("palette") is { } paletteOption)
            {
                String? file = ExistingFile(paletteOption, "Palette");
                if (file is null)
                    return OperationFailed;
                palette = Palette.LoadFromFile(file);
            }

            List<String> sources = ExpandSources(line.Positional, ImageExtensions, out Int32 problems);
            if (sources.Count == 0)
            {
                error.WriteLine("ERROR: Nothing to do.");
                return OperationFailed;
            }
            if (destinationName is not null && !HasWildcards(destinationName) && sources.Count > 1)
                throw new CommandLineException($"'{destinationName}' names a single file but {sources.Count} images are to be converted: use a mask such as *.png.");

            Boolean quiet = line.Has("quiet");
            Boolean keepGoing = line.Has("continue");
            Int32 failed = problems;
            for (Int32 i = 0; i < sources.Count; i++)
            {
                String source = sources[i];
                try
                {
                    foreach ((Bitmap32 bitmap, String suffix) in ReadImages(source))
                    {
                        String name;
                        if (destinationName is null)
                            name = Path.GetFileNameWithoutExtension(source) + suffix + target;
                        else if (HasWildcards(destinationName))
                            name = InsertSuffix(NameMask.Apply(Path.GetFileName(source), destinationName), suffix);
                        else if (suffix.Length > 0)
                            throw new InvalidDataException($"'{destinationName}' names a single file but the animated GIF has several images: use a mask such as *.png.");
                        else
                            name = destinationName;

                        String path = Path.Combine(destinationFolder ?? Path.GetDirectoryName(source) ?? "", name);
                        if (String.Equals(Path.GetFullPath(path), source, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("the result would overwrite its own source");
                        WriteImage(bitmap, path, palette);
                        if (!quiet)
                            output.WriteLine($"[{i + 1}/{sources.Count}] {source} -> {path}");
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ImageFormatException or InvalidDataException)
                {
                    failed++;
                    error.WriteLine($"ERROR: Unable to convert '{Path.GetFileName(source)}': {e.Message}");
                    if (!keepGoing)
                        break;
                }
            }

            if (!quiet)
                output.WriteLine(T($"{sources.Count} file(s) processed, {failed} error(s).", $"{sources.Count} fichier(s) traité(s), {failed} erreur(s)."));
            return failed > 0 ? OperationFailed : Success;
        }

        private static Boolean HasWildcards(String name) => name.Contains('*') || name.Contains('?');

        /// <summary>Inserts the sub-image suffix (<c>_3</c>) before the name's extension.</summary>
        private static String InsertSuffix(String name, String suffix)
            => suffix.Length == 0 ? name : Path.GetFileNameWithoutExtension(name) + suffix + Path.GetExtension(name);

        /// <summary>Images of a file: a single one for a BMP / PNG, one per sub-image (suffix <c>_n</c>) for an animated GIF.</summary>
        private static IEnumerable<(Bitmap32 Bitmap, String Suffix)> ReadImages(String path)
        {
            switch (Path.GetExtension(path).ToUpperInvariant())
            {
                case ".PNG":
                    yield return (PngCodec.ReadFile(path), "");
                    break;
                case ".GIF":
                    GifImage gif = GifCodec.ReadFile(path);
                    foreach (GifSubImage sub in gif.SubImages)
                        yield return (sub.Bitmap, gif.SubImages.Count > 1 ? "_" + sub.Index : "");
                    break;
                default:
                    yield return (BmpCodec.ReadFile(path), "");
                    break;
            }
        }

        // ------------------------------------------------------------------ script

        private Int32 RunScript(ParsedCommandLine line)
        {
            ValidateOptions(line, "open", "palette", "frames", "continue", "quiet", "relative");
            if (line.Positional.Count != 1)
                throw new CommandLineException("Give exactly one script file (.fas).");

            String? script = ExistingFile(line.Positional[0], "Script");
            if (script is null)
                return OperationFailed;
            String? palette = null;
            if (line.Get("palette") is { } paletteOption && (palette = ExistingFile(paletteOption, "Palette")) is null)
                return OperationFailed;
            String? open = null;
            if (line.Get("open") is { } openOption && (open = ExistingFile(openOption, "Project or FRM file")) is null)
                return OperationFailed;

            ConsoleInteraction ui = NewInteraction(line);
            ProjectService service = NewService(ui);
            service.SaveRelativePaths = line.Has("relative");
            if (open is not null)
            {
                if (!OpenSource(service, open, palette))
                    return OperationFailed;
            }
            else if (palette is not null && !service.LoadPalette(palette))
                return OperationFailed;

            String folder = Directory.GetCurrentDirectory();
            Boolean ok;
            try
            {
                ok = new ScriptRunner(service) { UnexpectedException = e => error.WriteLine("DEBUG: " + e) }.Run(script);
            }
            finally
            {
                Directory.SetCurrentDirectory(folder);
            }

            ok = ok && ui.ErrorCount == 0;
            if (!line.Has("quiet"))
                output.WriteLine(ok ? T("Script executed.", "Script exécuté.") : T("Script stopped on an error.", "Script arrêté sur une erreur."));
            return ok ? Success : OperationFailed;
        }

        /// <summary>Opens a project or an FRM the way <c>File &gt; Automate</c> does (forced palette, otherwise the neighboring <c>.PAL</c>).</summary>
        private Boolean OpenSource(ProjectService service, String path, String? palette)
        {
            service.NewProject();
            String extension = Path.GetExtension(path).ToUpperInvariant();
            if (extension == ".FPR")
                return service.LoadProject(path);
            if (!SourceExtensions.Contains(extension))
            {
                error.WriteLine($"ERROR: Unsupported file type '{extension}': {path}");
                return false;
            }

            String sibling = PathHelpers.ChangeFileExt(path, ".PAL");
            if (palette is not null)
            {
                if (!service.LoadPalette(palette))
                    return false;
            }
            else if (File.Exists(sibling) && !service.LoadPalette(sibling))
                return false;
            return service.LoadFrm(path);
        }

        // ------------------------------------------------------------------ diagnostic

        private Int32 Info(ParsedCommandLine line)
        {
            ValidateOptions(line);
            if (line.Positional.Count != 1)
                throw new CommandLineException("Give exactly one file (.frm or .fpr).");

            String path = line.Positional[0];
            if (!File.Exists(path))
            {
                error.WriteLine($"ERROR: File not found: {path}");
                return OperationFailed;
            }

            if (Path.GetExtension(path).Equals(".FPR", StringComparison.OrdinalIgnoreCase))
            {
                ProjectService service = NewService(new ConsoleInteraction(error, continueOnError: true, "BMP", language));
                if (!service.LoadProject(path))
                    return OperationFailed;
                Project p = service.Project;
                output.WriteLine($"Project: {path}");
                output.WriteLine($"  transp={p.Settings.TransparentMode} autocut={p.Settings.AutoCutting} dither={p.Settings.DitherMethod} fps={p.Settings.Fps} action={p.Settings.ActionFrame} sample=0x{p.Settings.SampleColor:X6}");
                output.WriteLine($"  palette={p.PaletteFile}  background={p.BackgroundFile}  frm={p.FrmFile}");
                for (Int32 d = 0; d < Project.DirectionCount; d++)
                {
                    Direction dir = p.Directions[d];
                    output.WriteLine($"  [{Project.DirectionNames[d],-10}] {dir.Count} frames, base=({dir.ShiftBaseX},{dir.ShiftBaseY}) first=({dir.FirstFrameOffsetX},{dir.FirstFrameOffsetY})");
                }
                return Success;
            }

            using FileStream stream = File.OpenRead(path);
            FrmFormat.Header h = FrmFormat.ReadHeader(stream);
            output.WriteLine($"FRM: version={h.Version} fps={h.Fps} action={h.ActionFrame} frames/dir={h.FramesPerDirection} areaSize={h.FrameAreaSize} fileSize={new FileInfo(path).Length}");
            output.WriteLine($"  shiftX=[{String.Join(",", h.ShiftX)}] shiftY=[{String.Join(",", h.ShiftY)}] dataOffset=[{String.Join(",", h.DataOffset)}]");
            return Success;
        }

        private Int32 Decode(ParsedCommandLine line)
        {
            ValidateOptions(line);
            if (line.Positional.Count != 2)
                throw new CommandLineException("Give the image to decode and the BMP file to write.");

            String input = line.Positional[0];
            String target = line.Positional[1];
            switch (Path.GetExtension(input).ToUpperInvariant())
            {
                case ".BMP":
                    BmpCodec.WriteFile(BmpCodec.ReadFile(input), target);
                    break;
                case ".PNG":
                    BmpCodec.WriteFile(PngCodec.ReadFile(input), target);
                    break;
                case ".GIF":
                    GifImage gif = GifCodec.ReadFile(input);
                    output.WriteLine($"screen={gif.ScreenWidth}x{gif.ScreenHeight} subimages={gif.SubImages.Count}");
                    foreach (GifSubImage sub in gif.SubImages)
                    {
                        String file = Path.Combine(Path.GetDirectoryName(target) ?? "", Path.GetFileNameWithoutExtension(target) + "_" + sub.Index + ".bmp");
                        BmpCodec.WriteFile(sub.Bitmap, file);
                        output.WriteLine($"  #{sub.Index}: {sub.Width}x{sub.Height} at ({sub.Left},{sub.Top}) -> {file}");
                    }
                    break;
                default:
                    throw new CommandLineException($"Unsupported image type '{Path.GetExtension(input)}': use BMP, PNG or GIF.");
            }
            return Success;
        }

        private Int32 Encode(ParsedCommandLine line)
        {
            ValidateOptions(line);
            if (line.Positional.Count != 2)
                throw new CommandLineException("Give the BMP image to encode and the file to write.");

            Bitmap32 bitmap = BmpCodec.ReadFile(line.Positional[0]);
            String target = line.Positional[1];
            switch (Path.GetExtension(target).ToUpperInvariant())
            {
                case ".PNG":
                    PngCodec.WriteFile(bitmap, target);
                    break;
                case ".GIF":
                    GifCodec.WriteFile(bitmap, target, Palette.LoadDefault());
                    break;
                case ".BMP":
                    BmpCodec.WriteFile(bitmap, target);
                    break;
                default:
                    throw new CommandLineException($"Unsupported output type '{Path.GetExtension(target)}': use PNG, GIF or BMP.");
            }
            return Success;
        }
    }
}