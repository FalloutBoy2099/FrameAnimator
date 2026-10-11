namespace FrameAnimator.Core.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using FrameAnimator.Core.Formats;
    using FrameAnimator.Core.Geometry;
    using FrameAnimator.Core.Imaging;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;
    using FrameAnimator.Core.Services;

    public class ScriptTests
    {
        private static String Script(TempDir dir, params String[] lines)
        {
            String path = dir.File("test.fas");
            File.WriteAllText(path, String.Join("\r\n", lines));
            return path;
        }

        private static void MakeImages(TempDir dir, Int32 n)
        {
            for (Int32 i = 0; i < n; i++)
                BmpCodec.WriteFile(Samples.Rect(20, 20, 0x0000FF, 0x00FF00, 4 + i, 4, 11 + i, 15), dir.File($"img{i}.bmp"));
        }

        [Fact]
        public void FullScriptBuildsProjectAndSavesFpr()
        {
            using var dir = new TempDir();
            MakeImages(dir, 3);
            var p = Samples.NewProject(out var ui, out var svc);
            String script = Script(dir,
                "NEW",
                "set_option,TRANSP_MODE,0",
                "SET_OPTION,AUTOCUTTING,1",
                "SET_OPTION,DITHER_METHOD,1",
                "SET_OPTION,ACTION_FRAME,2",
                "SET_OPTION,FPS,18",
                $"ADD_FRAMES,0,{dir.Path}\\img*.bmp",
                "SET_FRAMES_OFFSET,0,3,-4",
                "SET_FRAME_OFFSET,0,1,5,6,1",
                "SET_FRAME_OFFSET,0,0,2,2",
                "MOVE_FRAME,0,-1,0",
                $"SAVE,{dir.Path}\\out.fpr,PNG");
            var runner = new ScriptRunner(svc) { ChangeCurrentDirectory = false };
            Assert.True(runner.Run(script), String.Join("|", ui.Errors));

            Assert.Equal(3, p.Directions[0].Count);
            Assert.Equal(18, p.Settings.Fps);
            Assert.Equal(2, p.Settings.ActionFrame);
            Assert.Equal(DitherMethod.Advanced, p.Settings.DitherMethod);
            Assert.Equal(2, p.Directions[0].FirstFrameOffsetX);
            Assert.Equal(2, p.Directions[0].FirstFrameOffsetY);
            // MOVE_FRAME,0,-1,0 : the last frame (img2) is moved to the front
            Assert.EndsWith("img2.bmp", p.Directions[0].Frames[0].FileName);
            Assert.True(File.Exists(dir.File("out.fpr")));
            Assert.Equal(dir.File("out.fpr"), p.ProjectFile);
            Assert.Empty(ui.Errors);
        }

        [Fact]
        public void SetFrameOffsetIsRelativeAndInheritPropagates()
        {
            using var dir = new TempDir();
            MakeImages(dir, 4);
            var p = Samples.NewProject(out var ui, out var svc);
            p.Settings.TransparentMode = TransparentMode.Palette; // blue background 0000FF = palette[0]
            foreach (var f in Directory.GetFiles(dir.Path, "img*.bmp").OrderBy(x => x))
                svc.LoadFrameFile(p.Directions[1], f);
            var d = p.Directions[1];
            foreach (var f in d.Frames)
                FrameMath.AutoCut(p, f);
            Int32 before2 = d.Frames[2].OffsetX, before3 = d.Frames[3].OffsetX;
            var script = Script(dir, "SET_FRAME_OFFSET,1,1,10,-3,1");
            // Frame 1 already has an 8x12 rectangle offset by 1 px from frame 0: impose a relative offset of (10,-3)
            Assert.True(new ScriptRunner(svc) { ChangeCurrentDirectory = false }.Run(script), String.Join("|", ui.Errors));
            Assert.Equal((10, -3), FrameMath.Relative(d.Frames[0], d.Frames[1]));
            Assert.Equal(before2 + d.Frames[1].OffsetX, d.Frames[2].OffsetX);   // (frame 1 was 0 before)
            Assert.Equal(before3 + d.Frames[1].OffsetX, d.Frames[3].OffsetX);
        }

        [Fact]
        public void OpenFrmThenSaveWithMask()
        {
            using var dir = new TempDir();
            var p0 = Samples.NewProject(out _, out var svc0);
            p0.Settings.TransparentMode = TransparentMode.Palette;
            Samples.Fill(p0, 0, 2);
            Assert.True(svc0.SaveFrm(dir.File("item.frm")));

            var p = Samples.NewProject(out var ui, out var svc);
            String script = Script(dir,
                $"OPEN,{dir.Path}\\item.frm",
                "SET_OPTION,FPS,9",
                $"SAVE,{dir.Path}\\*.copy.frm");
            // *.copy.frm on item.frm: '*' → item, '.' → '.', literal "copy"…
            Assert.True(new ScriptRunner(svc) { ChangeCurrentDirectory = false }.Run(script), String.Join("|", ui.Errors));
            String expectedName = NameMask.Apply("item.frm", "*.copy.frm");
            Assert.True(File.Exists(dir.File(expectedName)), expectedName);
            var b = File.ReadAllBytes(dir.File(expectedName));
            Assert.Equal(9, (b[4] << 8) | b[5]);
        }

        [Fact]
        public void ErrorsAreReportedWithLineAndParameterAndStopOnNo()
        {
            using var dir = new TempDir();
            var p = Samples.NewProject(out var ui, out var svc);
            String script = Script(dir,
                "SET_OPTION,FPS,10",
                "SET_FRAME_OFFSET,9,0,0,0",
                "SET_OPTION,FPS,11");
            Assert.False(new ScriptRunner(svc) { ChangeCurrentDirectory = false }.Run(script));
            Assert.Equal(["Index '9' out of range\nin line 2, parameter 2"], ui.Errors);
            Assert.Contains("Continue executing script 'test.fas'?", ui.Questions);
            Assert.Equal(10, p.Settings.Fps); // stopped before the 3rd line

            // "Yes" answer: the script continues
            var ui2 = new SilentInteraction(true);
            var p2 = new Project();
            var svc2 = new ProjectService(p2, ui2, "");
            svc2.NewProject();
            Assert.True(new ScriptRunner(svc2) { ChangeCurrentDirectory = false }.Run(script));
            Assert.Equal(11, p2.Settings.Fps);
        }

        [Theory]
        [InlineData("BOGUS,1", "Unknown keyword 'BOGUS'\nin line 1, parameter 1")]
        [InlineData("SET_OPTION", "Parameter is required\nin line 1, parameter 2")]
        [InlineData("SET_OPTION,TRANSP_MODE,2", "Invalid parameter '2'\nin line 1, parameter 3")]
        [InlineData("SET_OPTION,NOPE,1", "Unknown keyword 'NOPE'\nin line 1, parameter 2")]
        [InlineData("SET_SAMPLE_COLOR,16777216", "Invalid parameter '16777216'\nin line 1, parameter 2")]
        [InlineData("SET_OPTION,FPS,abc", "'abc' is not a valid integer value\nin line 1, parameter 3")]
        public void ErrorMessages(String line, String expected)
        {
            using var dir = new TempDir();
            var p = Samples.NewProject(out var ui, out var svc);
            String script = Script(dir, line);
            Assert.False(new ScriptRunner(svc) { ChangeCurrentDirectory = false }.Run(script));
            Assert.Equal(expected, ui.Errors.Single());
        }

        [Fact]
        public void RemoveFramesVariants()
        {
            using var dir = new TempDir();
            var p = Samples.NewProject(out var ui, out var svc);
            for (Int32 d = 0; d < 6; d++)
                Samples.Fill(p, d, 5);
            String script = Script(dir,
                "REMOVE_FRAMES,0,0,-2",   // removes 0..3
                "REMOVE_FRAMES,1,2",      // removes frame 2
                "REMOVE_FRAMES,2",        // empties direction 2
                "SET_SAMPLE_COLOR,0x112233");
            Assert.True(new ScriptRunner(svc) { ChangeCurrentDirectory = false }.Run(script), String.Join("|", ui.Errors));
            Assert.Equal(1, p.Directions[0].Count);
            Assert.Equal(4, p.Directions[1].Count);
            Assert.Equal(0, p.Directions[2].Count);
            Assert.Equal(0x112233, p.Settings.SampleColor);
            Assert.True(new ScriptRunner(svc) { ChangeCurrentDirectory = false }.Run(Script(dir, "REMOVE_FRAMES")));
            Assert.All(p.Directions, d => Assert.Equal(0, d.Count));
        }
    }

    public class AutomationTests
    {
        [Fact]
        public void ConvertsFrmToFprWithMaskAndFrameExtension()
        {
            using var dir = new TempDir();
            var p0 = Samples.NewProject(out _, out var svc0);
            p0.Settings.TransparentMode = TransparentMode.Palette;
            for (Int32 d = 0; d < 6; d++)
                Samples.Fill(p0, d, 2);
            Assert.True(svc0.SaveFrm(dir.File("a.frm")));
            Assert.True(svc0.SaveFrm(dir.File("b.frm")));

            var p = Samples.NewProject(out var ui, out var svc);
            var opt = new AutomationOptions { DestinationMask = "*.FPR", FrameExtension = "PNG" };
            opt.Sources.Add(dir.File("a.frm"));
            opt.Sources.Add(dir.File("b.frm"));
            Assert.True(new AutomationRunner(svc).Run(opt), String.Join("|", ui.Errors));
            Assert.True(File.Exists(dir.File("a.FPR")));
            Assert.True(File.Exists(dir.File("b.FPR")));
            Assert.True(File.Exists(dir.File("a000000.PNG")) || File.Exists(dir.File("a000000.png")));
            Assert.Empty(ui.Errors);
        }

        [Fact]
        public void ConvertsFprBackToFrm()
        {
            using var dir = new TempDir();
            var p0 = Samples.NewProject(out _, out var svc0);
            p0.Settings.TransparentMode = TransparentMode.Palette;
            for (Int32 d = 0; d < 6; d++)
                Samples.Fill(p0, d, 2);
            Assert.True(svc0.SaveFrm(dir.File("src.frm")));
            Assert.True(svc0.LoadFrm(dir.File("src.frm")));
            Assert.True(svc0.SaveProject(dir.File("src.fpr"), dir.File("img.BMP")));

            var p = Samples.NewProject(out var ui, out var svc);
            var opt = new AutomationOptions { Format = AutomationFormat.Frm, DestinationMask = "out_*.FRM" };
            opt.Sources.Add(dir.File("src.fpr"));
            Assert.True(new AutomationRunner(svc).Run(opt), String.Join("|", ui.Errors));
            // "out_" consumes 4 characters of "src.fpr" (REN semantics): we only check that an FRM was produced
            Assert.Single(Directory.GetFiles(dir.Path, "out_*.FRM"));
        }
    }

    public class EditorTests
    {
        private static (Project p, ProjectEditor ed) Setup()
        {
            var p = Samples.NewProject(out _, out _);
            p.Settings.TransparentMode = TransparentMode.Palette;
            Samples.Fill(p, 1, 4);
            foreach (var f in p.Directions[1].Frames)
                FrameMath.AutoCut(p, f);
            return (p, new ProjectEditor(p));
        }

        [Fact]
        public void UndoRestoresEveryKindOfOffsetChange()
        {
            var (p, ed) = Setup();
            var d = p.Directions[1];

            ed.SetFramesOffset(1, 7, 8);
            Assert.Equal((7, 8), FrameMath.GetShift(d));
            ed.SetFrameOffset(1, 0, 3, 4, false);
            Assert.Equal((3, 4), (d.FirstFrameOffsetX, d.FirstFrameOffsetY));
            Int32 ox = d.Frames[1].OffsetX, oy = d.Frames[1].OffsetY, o2 = d.Frames[2].OffsetX;
            ed.SetFrameOffset(1, 1, 20, 5, inherit: true);
            Assert.Equal((20, 5), FrameMath.Relative(d.Frames[0], d.Frames[1]));
            Assert.NotEqual(o2, d.Frames[2].OffsetX);

            Assert.Equal(1, ed.Undo());
            Assert.Equal((ox, oy), (d.Frames[1].OffsetX, d.Frames[1].OffsetY));
            Assert.Equal(o2, d.Frames[2].OffsetX);
            ed.Undo();
            Assert.Equal((0, 0), (d.FirstFrameOffsetX, d.FirstFrameOffsetY));
            ed.Undo();
            Assert.Equal((0, 0), (d.ShiftBaseX, d.ShiftBaseY));
            Assert.False(ed.CanUndo);
        }

        [Fact]
        public void UndoStackIsLimitedToTen()
        {
            var (p, ed) = Setup();
            for (Int32 i = 1; i <= 12; i++)
                ed.SetFramesOffset(1, i, i);
            Int32 n = 0;
            while (ed.CanUndo)
            { ed.Undo(); n++; }
            Assert.Equal(10, n);
        }

        [Fact]
        public void DragMoveEditsBaseForFrame0AndFrameOtherwise()
        {
            var (p, ed) = Setup();
            var d = p.Directions[1];
            ed.BeginDrag();
            ed.DragMove(1, 0, 3, -2, false);
            ed.DragMove(1, 0, 1, 1, false);
            Assert.Equal((4, -1), (d.ShiftBaseX, d.ShiftBaseY));
            ed.Undo();                           // a single undo record for the whole drag
            Assert.Equal((0, 0), (d.ShiftBaseX, d.ShiftBaseY));
            Assert.False(ed.CanUndo);

            p.Settings.LockOffsets = true;
            ed.BeginDrag();
            ed.DragMove(1, 2, 5, 5, true);
            Assert.Equal(0, d.Frames[2].OffsetX);   // locked
            p.Settings.LockOffsets = false;
            ed.BeginDrag();
            ed.DragMove(1, 2, 5, 5, true);
            Assert.Equal((5, 5), (d.Frames[2].OffsetX, d.Frames[2].OffsetY));
            Assert.Equal((5, 5), (d.Frames[3].OffsetX, d.Frames[3].OffsetY));   // Ctrl: propagation
            Assert.Equal((0, 0), (d.Frames[1].OffsetX, d.Frames[1].OffsetY));
        }

        [Fact]
        public void WizardAndConstraintUseDirectionAngles()
        {
            var (p, ed) = Setup();
            // dir 1 = East : angle 0 → Y must cancel the total vertical displacement (zero here)
            Assert.Equal(0, ed.OffsetWizard(1, 5));
            Assert.Equal(100, ProjectEditor.ConstrainY(1, 10, 100, 55));
            Assert.Equal(100 - 34, ProjectEditor.ConstrainY(0, 10, 100, 55)); // tan(-36.9°)·45 = -33.8 → -34
            Assert.Equal(100 + 34, ProjectEditor.ConstrainY(2, 10, 100, 55));
        }
    }
}