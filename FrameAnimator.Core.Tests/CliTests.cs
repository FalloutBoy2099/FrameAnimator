namespace FrameAnimator.Core.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using FrameAnimator.Core.Cli;
    using FrameAnimator.Core.Imaging;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;
    using Xunit;

    /// <summary>Console version: command-line parsing, <c>/?</c> help, and the convert / script / automate commands.</summary>
    public class CliTests
    {
        private static (Int32 Code, String Out, String Err) RunIn(String language, params String[] args)
        {
            StringWriter output = new();
            StringWriter error = new();
            Int32 code = new CliApp(output, error, language, "fa").Execute(args);
            return (code, output.ToString(), error.ToString());
        }

        private static (Int32 Code, String Out, String Err) Run(params String[] args) => RunIn("en", args);

        /// <summary>Two-frame FRM written from an in-memory project.</summary>
        private static String MakeFrm(TempDir dir, String name = "hero.frm", Int32 directions = 1)
        {
            Project p = Samples.NewProject(out _, out var service);
            p.Settings.TransparentMode = TransparentMode.Palette;
            for (Int32 d = 0; d < directions; d++)
                Samples.Fill(p, d, 2);
            String frm = dir.File(name);
            Assert.True(service.SaveFrm(frm));
            return frm;
        }

        // ------------------------------------------------------------------ command-line parsing

        [Fact]
        public void ParsesTheThreeOptionForms()
        {
            ParsedCommandLine line = CommandLine.Parse(["convert", "a.frm", "/to:frm", "--out=C:\\x", "-palette", "p.act", "/continue", "/QUIET"]);
            Assert.Equal("convert", line.Command);
            Assert.Equal(["a.frm"], line.Positional);
            Assert.Equal("frm", line.Get("format"));
            Assert.Equal("C:\\x", line.Get("dest"));
            Assert.Equal("p.act", line.Get("palette"));
            Assert.True(line.Has("continue"));
            Assert.True(line.Has("quiet"));
        }

        [Fact]
        public void YesIsAnAliasOfContinueAndSourceCanBeRepeated()
        {
            ParsedCommandLine line = CommandLine.Parse(["automate", "/yes", "/source:a.frm", "/src", "b.frm", "/dest:*.fpr"]);
            Assert.True(line.Has("continue"));
            Assert.Equal(["a.frm", "b.frm"], line.GetAll("source"));
        }

        [Fact]
        public void ATrailingBackslashSwallowedByTheClosingQuoteIsRestored()
        {
            // /out:"C:\dossier\" arrives as /out:C:\dossier": the quote is the trailing separator, swallowed
            String swallowed = "C:\\dossier\"";
            ParsedCommandLine line = CommandLine.Parse(["image", "/out:" + swallowed, "-o", swallowed, swallowed]);
            Assert.Equal("C:\\dossier\\", line.Get("dest"));
            Assert.Equal(["C:\\dossier\\", "C:\\dossier\\"], line.GetAll("dest"));
            Assert.Equal(["C:\\dossier\\"], line.Positional);
            Assert.Equal("a\"b", CommandLine.FixTrailingQuote("a\"b"));
            Assert.Equal("plain", CommandLine.FixTrailingQuote("plain"));
        }

        [Fact]
        public void AWordStartingWithASlashThatIsNotAnOptionStaysAPath()
        {
            ParsedCommandLine line = CommandLine.Parse(["convert", "/data/hero.frm", "/to:fpr"]);
            Assert.Equal(["/data/hero.frm"], line.Positional);
        }

        [Fact]
        public void RejectsUnknownDashOptionsAndMissingValues()
        {
            Assert.Throws<CommandLineException>(() => CommandLine.Parse(["convert", "--bogus"]));
            Assert.Throws<CommandLineException>(() => CommandLine.Parse(["convert", "a.frm", "/to"]));
            Assert.Throws<CommandLineException>(() => CommandLine.Parse(["convert", "a.frm", "/quiet:1"]));
        }

        // ------------------------------------------------------------------ console mode of the GUI executable

        [Theory]
        [InlineData("convert")]
        [InlineData("CONVERT")]
        [InlineData("image")]
        [InlineData("script")]
        [InlineData("run")]
        [InlineData("automate")]
        [InlineData("info")]
        [InlineData("help")]
        [InlineData("/?")]
        [InlineData("-?")]
        [InlineData("-h")]
        [InlineData("--help")]
        [InlineData("/help")]
        [InlineData("--version")]
        [InlineData("/version")]
        public void ACommandOrAHelpSwitchSelectsTheConsoleMode(String first)
        {
            Assert.True(CliApp.IsConsoleInvocation([first, "x"]));
            Assert.True(CliApp.IsConsoleInvocation([first]));
        }

        [Fact]
        public void NoArgumentOrAFileToOpenKeepsTheWindow()
        {
            String[][] cases =
            [
                [],
                ["hero.frm"],
                ["D:/fo/hero.fpr"],
                ["/data/hero.frm"],
                ["--theme", "dark"],
                ["--snapshot", "out.png"],
                ["hero.frm", "--snapshot", "out.png"],
            ];
            foreach (String[] args in cases)
                Assert.False(CliApp.IsConsoleInvocation(args), String.Join(' ', args));
        }

        [Fact]
        public void HelpRequestsAreRecognised()
        {
            Assert.True(CliApp.IsHelpRequest(["/?"]));
            Assert.True(CliApp.IsHelpRequest(["convert", "/?"]));
            Assert.True(CliApp.IsHelpRequest(["help", "script"]));
            Assert.True(CliApp.IsHelpRequest(["--version"]));
            Assert.False(CliApp.IsHelpRequest(["convert", "a.frm", "/to:fpr"]));
            Assert.False(CliApp.IsHelpRequest([]));
        }

        // ------------------------------------------------------------------ help

        [Theory]
        [InlineData("/?")]
        [InlineData("-?")]
        [InlineData("-h")]
        [InlineData("--help")]
        [InlineData("help")]
        public void EveryHelpSpellingShowsTheGeneralHelp(String spelling)
        {
            (Int32 code, String output, _) = Run(spelling);
            Assert.Equal(0, code);
            Assert.Contains("Usage:", output);
            Assert.Contains("convert", output);
            Assert.Contains("script", output);
            Assert.Contains("automate", output);
        }

        [Fact]
        public void NoArgumentShowsTheHelpAndReportsAUsageError()
        {
            (Int32 code, String output, _) = Run();
            Assert.Equal(CliApp.UsageError, code);
            Assert.Contains("Usage:", output);
        }

        [Theory]
        [InlineData("convert", "/to:<fpr|frm|fr>")]
        [InlineData("image", "/to:<bmp|png|gif>")]
        [InlineData("script", "/open:<file>")]
        [InlineData("automate", "/dest:<mask>")]
        public void CommandHelpIsReachableWithASlashQuestionMark(String command, String expected)
        {
            Assert.Contains(expected, Run(command, "/?").Out);
            Assert.Contains(expected, Run("help", command).Out);
        }

        [Fact]
        public void RunIsAnAliasOfScriptAndHelpFollowsTheSystemLanguage()
        {
            Assert.Contains("/open:<file>", Run("run", "/?").Out);
            Assert.Contains("Utilisation", RunIn("fr", "/?").Out);
            Assert.Contains("Usage:", RunIn("de", "/?").Out);
        }

        [Fact]
        public void UnknownCommandAndUnknownOptionsAreUsageErrors()
        {
            (Int32 code, _, String error) = Run("bogus");
            Assert.Equal(CliApp.UsageError, code);
            Assert.Contains("Unknown command", error);

            Assert.Equal(CliApp.UsageError, Run("convert", "a.frm", "--bogus").Code);
            Assert.Equal(CliApp.UsageError, Run("convert", "a.frm", "/script:x.fas").Code);
        }

        [Fact]
        public void VersionIsPrinted()
        {
            (Int32 code, String output, _) = Run("/version");
            Assert.Equal(0, code);
            Assert.Matches(@"^\S+", output.Trim());
        }

        // ------------------------------------------------------------------ destination mask

        [Fact]
        public void DestinationMaskDefaultsToNextToTheSource()
        {
            Assert.Equal(("*.FRM", AutomationFormat.Frm), CliApp.BuildMask(null, AutomationFormat.Frm, replaceExtension: false));
            Assert.Equal(("*.FR0", AutomationFormat.FrN), CliApp.BuildMask(null, AutomationFormat.FrN, replaceExtension: false));
        }

        [Fact]
        public void DestinationFormatCanComeFromTheMaskExtension()
        {
            Assert.Equal(("*.fpr", AutomationFormat.FprProject), CliApp.BuildMask("*.fpr", null, replaceExtension: false));
            Assert.Throws<CommandLineException>(() => CliApp.BuildMask("*.txt", null, replaceExtension: false));
            Assert.Throws<CommandLineException>(() => CliApp.BuildMask(null, null, replaceExtension: false));
        }

        [Fact]
        public void ConvertRefusesAFormatThatContradictsTheMaskButAutomateReplacesTheExtension()
        {
            Assert.Throws<CommandLineException>(() => CliApp.BuildMask("*.frm", AutomationFormat.FprProject, replaceExtension: false));
            Assert.Equal(("*.FPR", AutomationFormat.FprProject), CliApp.BuildMask("*.frm", AutomationFormat.FprProject, replaceExtension: true));
        }

        [Fact]
        public void ADestinationFolderBecomesAMaskInThatFolder()
        {
            using TempDir dir = new();
            String folder = dir.File("out") + Path.DirectorySeparatorChar;
            (String mask, AutomationFormat format) = CliApp.BuildMask(folder, AutomationFormat.Frm, replaceExtension: false);
            Assert.Equal(AutomationFormat.Frm, format);
            Assert.Equal(Path.Combine(dir.File("out"), "*.FRM"), mask);
            Assert.True(Directory.Exists(dir.File("out")));
        }

        // ------------------------------------------------------------------ convert

        [Fact]
        public void ConvertsAnFrmToAProjectAndBack()
        {
            using TempDir dir = new();
            String frm = MakeFrm(dir);

            (Int32 code, _, String error) = Run("convert", frm, "/to:fpr", "/quiet");
            Assert.True(code == 0, error);
            Assert.True(File.Exists(dir.File("hero.fpr")));
            Assert.NotEmpty(Directory.GetFiles(dir.Path, "hero0000*.BMP"));

            String outFolder = dir.File("out") + Path.DirectorySeparatorChar;
            (code, _, error) = Run("convert", dir.File("hero.fpr"), "/to:frm", "/out:" + outFolder, "/quiet");
            Assert.True(code == 0, error);
            Assert.Equal(File.ReadAllBytes(frm), File.ReadAllBytes(dir.File("out\\hero.frm")));
        }

        [Fact]
        public void ConvertExpandsWildcardsAndHonoursTheFramesFormat()
        {
            using TempDir dir = new();
            MakeFrm(dir, "a.frm");
            MakeFrm(dir, "b.frm");

            (Int32 code, String output, String error) = Run("convert", dir.File("*.frm"), "/to:fpr", "/frames:png");
            Assert.True(code == 0, error);
            Assert.Contains("[1/2]", output);
            Assert.Contains("[2/2]", output);
            Assert.Contains("2 file(s) processed, 0 error(s).", output);
            Assert.True(File.Exists(dir.File("a.fpr")));
            Assert.True(File.Exists(dir.File("b.fpr")));
            Assert.NotEmpty(Directory.GetFiles(dir.Path, "a0000*.PNG"));
        }

        [Fact]
        public void ConvertWritesFrnSets()
        {
            using TempDir dir = new();
            String frm = MakeFrm(dir, directions: 6);
            (Int32 code, _, String error) = Run("convert", frm, "/to:fr", "/quiet");
            Assert.True(code == 0, error);
            for (Int32 d = 0; d < 6; d++)
                Assert.True(File.Exists(dir.File("hero.FR" + d)), "hero.FR" + d);
        }

        [Fact]
        public void ConvertNeedsADestinationFormat()
        {
            using TempDir dir = new();
            (Int32 code, _, String error) = Run("convert", MakeFrm(dir));
            Assert.Equal(CliApp.UsageError, code);
            Assert.Contains("/to", error);
        }

        // ------------------------------------------------------------------ source errors

        [Fact]
        public void MissingOrUnsupportedSourcesFailWithExitCode3()
        {
            using TempDir dir = new();
            File.WriteAllText(dir.File("notes.txt"), "x");

            (Int32 code, _, String error) = Run("convert", dir.File("missing.frm"), "/to:fpr", "/quiet");
            Assert.Equal(CliApp.OperationFailed, code);
            Assert.Contains("File not found", error);

            (code, _, error) = Run("convert", dir.File("notes.txt"), "/to:fpr", "/quiet");
            Assert.Equal(CliApp.OperationFailed, code);
            Assert.Contains("Unsupported source type", error);

            (code, _, error) = Run("convert", dir.File("*.frm"), "/to:fpr", "/quiet");
            Assert.Equal(CliApp.OperationFailed, code);
            Assert.Contains("No file matches", error);
        }

        [Fact]
        public void ABadSourceDoesNotPreventTheOthersAndTheExitCodeReportsIt()
        {
            using TempDir dir = new();
            String good = MakeFrm(dir);
            File.WriteAllBytes(dir.File("bad.frm"), [1, 2, 3]);

            (Int32 code, _, String error) = Run("convert", dir.File("bad.frm"), good, "/to:fpr", "/continue", "/quiet");
            Assert.Equal(CliApp.OperationFailed, code);
            Assert.Contains("Unable to load FRM 'bad.frm'", error);
            Assert.True(File.Exists(dir.File("hero.fpr")));
        }

        [Fact]
        public void ByDefaultTheBatchStopsAtTheFirstError()
        {
            using TempDir dir = new();
            String good = MakeFrm(dir);
            File.WriteAllBytes(dir.File("bad.frm"), [1, 2, 3]);

            (Int32 code, String output, _) = Run("convert", dir.File("bad.frm"), good, "/to:fpr");
            Assert.Equal(CliApp.OperationFailed, code);
            Assert.Contains("FAILED", output);
            Assert.False(File.Exists(dir.File("hero.fpr")));
        }

        // ------------------------------------------------------------------ automate

        [Fact]
        public void AutomateProcessesSeveralSourcesIntoADestinationMask()
        {
            using TempDir dir = new();
            MakeFrm(dir, "a.frm");
            MakeFrm(dir, "b.frm");
            Directory.CreateDirectory(dir.File("out"));

            (Int32 code, _, String error) = Run("automate", dir.File("a.frm"), "/source:" + dir.File("b.frm"), "/dest:" + dir.File("out\\*.fpr"), "/quiet");
            Assert.True(code == 0, error);
            Assert.True(File.Exists(dir.File("out\\a.fpr")));
            Assert.True(File.Exists(dir.File("out\\b.fpr")));
        }

        [Fact]
        public void AutomateAppliesAScriptToEverySourceBeforeSaving()
        {
            using TempDir dir = new();
            String frm = MakeFrm(dir);
            String script = dir.File("fps.fas");
            File.WriteAllText(script, "SET_OPTION,FPS,25");

            (Int32 code, _, String error) = Run("automate", frm, "/dest:*.fpr", "/script:" + script, "/quiet");
            Assert.True(code == 0, error);
            Assert.Contains("FPS=25", File.ReadAllText(dir.File("hero.fpr")));
        }

        [Fact]
        public void AutomateUsesTheGivenPaletteForFrmSources()
        {
            using TempDir dir = new();
            String frm = MakeFrm(dir);
            String act = dir.File("custom.act");
            File.Copy(System.IO.Path.Combine(AppContext.BaseDirectory, "Default.act"), act);

            (Int32 code, _, String error) = Run("automate", frm, "/dest:*.fpr", "/palette:" + act, "/quiet");
            Assert.True(code == 0, error);
            Assert.Contains("custom.act", File.ReadAllText(dir.File("hero.fpr")));
        }

        [Fact]
        public void AutomateNeedsADestinationOrAFormat()
        {
            using TempDir dir = new();
            Assert.Equal(CliApp.UsageError, Run("automate", MakeFrm(dir)).Code);
            Assert.Equal(CliApp.UsageError, Run("automate").Code);
        }

        // ------------------------------------------------------------------ script

        [Fact]
        public void ScriptRunsOnAFreshProject()
        {
            using TempDir dir = new();
            String frm = MakeFrm(dir);
            String script = dir.File("s.fas");
            File.WriteAllText(script, $"OPEN,{frm}{Environment.NewLine}SAVE,{dir.File("s.fpr")},PNG");

            (Int32 code, String output, String error) = Run("script", script);
            Assert.True(code == 0, error);
            Assert.Contains("Script executed.", output);
            Assert.True(File.Exists(dir.File("s.fpr")));
            Assert.NotEmpty(Directory.GetFiles(dir.Path, "s0000*.PNG"));
        }

        [Fact]
        public void ScriptCanOpenAFileFirst()
        {
            using TempDir dir = new();
            String frm = MakeFrm(dir);
            String script = dir.File("s.fas");
            File.WriteAllText(script, $"SET_OPTION,FPS,12{Environment.NewLine}SAVE,{dir.File("t.fpr")},BMP");

            (Int32 code, _, String error) = Run("script", script, "/open:" + frm, "/quiet");
            Assert.True(code == 0, error);
            String project = File.ReadAllText(dir.File("t.fpr"));
            Assert.Contains("FPS=12", project);
            Assert.Contains("Frame(0,1)=", project);
        }

        [Fact]
        public void ScriptErrorsStopTheScriptAndSetTheExitCode()
        {
            using TempDir dir = new();
            String script = dir.File("bad.fas");
            File.WriteAllText(script, $"BOGUS{Environment.NewLine}SAVE,{dir.File("never.fpr")}");

            (Int32 code, String output, String error) = Run("script", script);
            Assert.Equal(CliApp.OperationFailed, code);
            Assert.Contains("Unknown keyword 'BOGUS'", error);
            Assert.Contains("Script stopped on an error.", output);
            Assert.False(File.Exists(dir.File("never.fpr")));
        }

        [Fact]
        public void ScriptContinuesAfterAnErrorWithContinueButStillFails()
        {
            using TempDir dir = new();
            String script = dir.File("bad.fas");
            File.WriteAllText(script, $"BOGUS{Environment.NewLine}SET_OPTION,FPS,9{Environment.NewLine}SAVE,{dir.File("kept.fpr")}");

            (Int32 code, _, _) = Run("script", script, "/continue", "/quiet");
            Assert.Equal(CliApp.OperationFailed, code);
            Assert.True(File.Exists(dir.File("kept.fpr")));
        }

        [Fact]
        public void ScriptValidatesItsArguments()
        {
            using TempDir dir = new();
            Assert.Equal(CliApp.UsageError, Run("script").Code);
            Assert.Equal(CliApp.OperationFailed, Run("script", dir.File("missing.fas")).Code);
        }

        [Fact]
        public void ScriptRestoresTheCurrentFolder()
        {
            using TempDir dir = new();
            String script = dir.File("s.fas");
            File.WriteAllText(script, "SET_OPTION,FPS,1");
            String before = Directory.GetCurrentDirectory();

            Run("script", script, "/quiet");
            Assert.Equal(before, Directory.GetCurrentDirectory());
        }

        // ------------------------------------------------------------------ image

        private static String MakeBmp(TempDir dir, String name = "hero.bmp")
        {
            String path = dir.File(name);
            BmpCodec.WriteFile(Samples.Rect(12, 9, 0x102030, 0xE0A040, 2, 2, 8, 6), path);
            return path;
        }

        [Fact]
        public void ConvertsAnImageNextToItsSourceAndBackWithoutLoss()
        {
            using TempDir dir = new();
            String bmp = MakeBmp(dir);

            (Int32 code, String output, String error) = Run("image", bmp, "/to:png");
            Assert.True(code == 0, error);
            Assert.Contains("hero.png", output);
            Bitmap32 png = PngCodec.ReadFile(dir.File("hero.png"));
            Assert.Equal(BmpCodec.ReadFile(bmp).Pixels, png.Pixels);

            File.Delete(bmp);
            (code, _, error) = Run("image", dir.File("hero.png"), "/to:bmp", "/quiet");
            Assert.True(code == 0, error);
            Assert.Equal(png.Pixels, BmpCodec.ReadFile(bmp).Pixels);
        }

        [Fact]
        public void ConvertsToGifWithTheDefaultPalette()
        {
            using TempDir dir = new();
            Assert.Equal(0, Run("image", MakeBmp(dir), "/to:gif", "/quiet").Code);
            GifImage gif = GifCodec.ReadFile(dir.File("hero.gif"));
            Assert.Single(gif.SubImages);
            Assert.Equal(12, gif.ScreenWidth);
            Assert.Equal(9, gif.ScreenHeight);
        }

        [Fact]
        public void ImageExpandsWildcardsAndWritesIntoADestinationFolder()
        {
            using TempDir dir = new();
            MakeBmp(dir, "a.bmp");
            MakeBmp(dir, "b.bmp");
            String folder = dir.File("out") + Path.DirectorySeparatorChar;

            (Int32 code, String output, String error) = Run("image", dir.File("*.bmp"), "/to:png", "/out:" + folder);
            Assert.True(code == 0, error);
            Assert.Contains("2 file(s) processed, 0 error(s).", output);
            Assert.True(File.Exists(System.IO.Path.Combine(dir.File("out"), "a.png")));
            Assert.True(File.Exists(System.IO.Path.Combine(dir.File("out"), "b.png")));
        }

        [Fact]
        public void ImageDestinationMaskTakesTheNameOfTheSourceAndGivesTheFormat()
        {
            using TempDir dir = new();
            String bmp = MakeBmp(dir, "hero.bmp");

            (Int32 code, _, String error) = Run("image", bmp, "/out:" + dir.File("*_copy.png"), "/quiet");
            Assert.True(code == 0, error);
            Assert.True(File.Exists(dir.File("hero_copy.png")));

            // a fixed name works for a single source only
            (code, _, error) = Run("image", bmp, "/out:" + dir.File("single.gif"), "/quiet");
            Assert.True(code == 0, error);
            Assert.True(File.Exists(dir.File("single.gif")));
        }

        [Fact]
        public void AnAnimatedGifGivesOneImagePerSubImage()
        {
            using TempDir dir = new();
            String gif = dir.File("walk.gif");
            File.Copy(System.IO.Path.Combine(AppContext.BaseDirectory, "Golden", "g0.gif"), gif);
            Int32 count = GifCodec.ReadFile(gif).SubImages.Count;
            Assert.True(count > 1);

            (Int32 code, _, String error) = Run("image", gif, "/to:png", "/quiet");
            Assert.True(code == 0, error);
            for (Int32 i = 0; i < count; i++)
                Assert.True(File.Exists(dir.File($"walk_{i}.png")), $"walk_{i}.png");

            // with a fixed name, several images do not fit in a single file
            (code, _, error) = Run("image", gif, "/out:" + dir.File("one.png"), "/quiet");
            Assert.Equal(CliApp.OperationFailed, code);
            Assert.Contains("names a single file", error);
        }

        [Fact]
        public void ImageNeverOverwritesItsOwnSource()
        {
            using TempDir dir = new();
            String bmp = MakeBmp(dir);
            byte[] before = File.ReadAllBytes(bmp);

            (Int32 code, _, String error) = Run("image", bmp, "/to:bmp", "/quiet");
            Assert.Equal(CliApp.OperationFailed, code);
            Assert.Contains("overwrite its own source", error);
            Assert.Equal(before, File.ReadAllBytes(bmp));
        }

        [Fact]
        public void ImageValidatesItsArguments()
        {
            using TempDir dir = new();
            String bmp = MakeBmp(dir);
            Assert.Equal(CliApp.UsageError, Run("image").Code);
            Assert.Equal(CliApp.UsageError, Run("image", bmp).Code);
            Assert.Equal(CliApp.UsageError, Run("image", bmp, "/to:jpeg").Code);
            Assert.Equal(CliApp.UsageError, Run("image", bmp, "/to:png", "/out:" + dir.File("x.gif")).Code);
            Assert.Equal(CliApp.UsageError, Run("image", bmp, "/to:png", "/frames:png").Code);

            MakeBmp(dir, "other.bmp");
            Assert.Equal(CliApp.UsageError, Run("image", dir.File("*.bmp"), "/out:" + dir.File("single.png")).Code);
        }

        [Fact]
        public void ImageReportsMissingAndCorruptFilesAndContinuesOnRequest()
        {
            using TempDir dir = new();
            String good = MakeBmp(dir);
            File.WriteAllBytes(dir.File("bad.png"), [1, 2, 3, 4]);

            (Int32 code, _, String error) = Run("image", dir.File("missing.bmp"), "/to:png", "/quiet");
            Assert.Equal(CliApp.OperationFailed, code);
            Assert.Contains("File not found", error);

            (code, _, error) = Run("image", dir.File("bad.png"), good, "/to:bmp", "/continue", "/quiet");
            Assert.Equal(CliApp.OperationFailed, code);
            Assert.Contains("Unable to convert 'bad.png'", error);
            Assert.True(File.Exists(dir.File("bad.bmp")) == false);

            (code, _, _) = Run("image", dir.File("bad.png"), good, "/to:gif", "/quiet");
            Assert.Equal(CliApp.OperationFailed, code);
            Assert.False(File.Exists(dir.File("hero.gif")));
        }

        // ------------------------------------------------------------------ relative paths

        [Fact]
        public void RelativeWritesTheProjectPathsRelativeToItsFolder()
        {
            using TempDir dir = new();
            String frm = MakeFrm(dir);

            (Int32 code, _, String error) = Run("convert", frm, "/to:fpr", "/relative", "/quiet");
            Assert.True(code == 0, error);

            String text = File.ReadAllText(dir.File("hero.fpr"));
            Assert.Contains("Frame(0,0)=0,0,hero000000.BMP", text);
            Assert.DoesNotContain(dir.Path, text);
        }

        [Fact]
        public void WithoutRelativeThePathsStayAbsoluteLikeTheOriginal()
        {
            using TempDir dir = new();
            String frm = MakeFrm(dir);

            Assert.Equal(0, Run("convert", frm, "/to:fpr", "/quiet").Code);

            Assert.Contains(dir.Path, File.ReadAllText(dir.File("hero.fpr")));
        }

        [Fact]
        public void ScriptAcceptsRelativeToo()
        {
            using TempDir dir = new();
            String frm = MakeFrm(dir);
            String script = dir.File("s.fas");
            File.WriteAllText(script, "SAVE," + dir.File("t.fpr") + ",BMP");

            (Int32 code, _, String error) = Run("script", script, "/open:" + frm, "/relative", "/quiet");
            Assert.True(code == 0, error);
            Assert.DoesNotContain(dir.Path, File.ReadAllText(dir.File("t.fpr")));
        }

        // ------------------------------------------------------------------ diagnostic

        [Fact]
        public void InfoShowsAProjectAndAnFrm()
        {
            using TempDir dir = new();
            String frm = MakeFrm(dir);
            Assert.Equal(0, Run("convert", frm, "/to:fpr", "/quiet").Code);

            (Int32 code, String output, _) = Run("info", frm);
            Assert.Equal(0, code);
            Assert.Contains("frames/dir=2", output);

            (code, output, _) = Run("info", dir.File("hero.fpr"));
            Assert.Equal(0, code);
            Assert.Contains("2 frames", output);
            Assert.Equal(CliApp.OperationFailed, Run("info", dir.File("missing.frm")).Code);
        }
    }
}
