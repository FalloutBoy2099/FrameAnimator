namespace FrameAnimator.Core.Tests
{
    using System;
    using System.IO;
    using FrameAnimator.Core.Imaging;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;
    using FrameAnimator.Core.Services;

    /// <summary>"Has the project changed since it was loaded or saved": what counts, and what does not.</summary>
    public class ModifiedStateTests
    {
        private static String Image(TempDir dir, String name, Int32 seed = 0)
        {
            String path = dir.File(name);
            BmpCodec.WriteFile(Samples.Rect(12 + seed, 9, 0x102030, 0xE0A040, 2, 2, 8, 6), path);
            return path;
        }

        private static Project TwoFrames(TempDir dir, out SilentInteraction ui, out ProjectService service)
        {
            Project project = Samples.NewProject(out ui, out service);
            service.LoadFrameFile(project.Directions[0], Image(dir, "a.bmp"));
            service.LoadFrameFile(project.Directions[0], Image(dir, "b.bmp", 1));
            service.MarkClean();
            return project;
        }

        [Fact]
        public void ANewProjectIsNotModified()
        {
            Samples.NewProject(out _, out ProjectService service);

            Assert.False(service.IsModified);
        }

        [Fact]
        public void AddingAFrameModifiesTheProject()
        {
            using TempDir dir = new();
            Project project = Samples.NewProject(out _, out ProjectService service);

            service.LoadFrameFile(project.Directions[0], Image(dir, "a.bmp"));

            Assert.True(service.IsModified);
        }

        [Fact]
        public void SavingMakesTheProjectCleanAgain()
        {
            using TempDir dir = new();
            Project project = Samples.NewProject(out SilentInteraction ui, out ProjectService service);
            service.LoadFrameFile(project.Directions[0], Image(dir, "a.bmp"));

            Assert.True(service.SaveProject(dir.File("p.fpr")), String.Join("|", ui.Errors));

            Assert.False(service.IsModified);
        }

        [Fact]
        public void ALoadedProjectIsNotModifiedUntilItIsEdited()
        {
            using TempDir dir = new();
            Project source = TwoFrames(dir, out _, out ProjectService writer);
            Assert.True(writer.SaveProject(dir.File("p.fpr")));

            Project project = Samples.NewProject(out SilentInteraction ui, out ProjectService service);
            Assert.True(service.LoadProject(dir.File("p.fpr")), String.Join("|", ui.Errors));

            Assert.False(service.IsModified);
            new ProjectEditor(project).SetFramesOffset(0, 3, 4);
            Assert.True(service.IsModified);
        }

        [Fact]
        public void OffsetEditsMarkTheProjectAndUndoingThemClearsTheMark()
        {
            using TempDir dir = new();
            Project project = TwoFrames(dir, out _, out ProjectService service);
            ProjectEditor editor = new(project);

            editor.SetFramesOffset(0, 5, -3);
            Assert.True(service.IsModified);

            editor.Undo();
            Assert.False(service.IsModified); // back to what was saved
        }

        [Fact]
        public void MovingAndRemovingFramesMarkTheProject()
        {
            using TempDir dir = new();
            Project project = TwoFrames(dir, out _, out ProjectService service);
            ProjectEditor editor = new(project);

            editor.MoveFrame(0, 0, 1);
            Assert.True(service.IsModified);

            service.MarkClean();
            editor.RemoveFrames(0, [1]);
            Assert.True(service.IsModified);
        }

        [Fact]
        public void PersistedOptionsMarkTheProjectButOnesThatAreNotStoredDoNot()
        {
            using TempDir dir = new();
            Project project = TwoFrames(dir, out _, out ProjectService service);

            project.Settings.LockOffsets = false;
            project.Settings.GhostIncrement = 2;
            Assert.False(service.IsModified); // not part of a project file

            project.Settings.Fps = 12;
            Assert.True(service.IsModified);
        }

        [Fact]
        public void ALoadedPaletteModifiesTheProject()
        {
            using TempDir dir = new();
            Project project = TwoFrames(dir, out SilentInteraction ui, out ProjectService service);
            String palette = dir.File("p.act");
            File.WriteAllBytes(palette, new Byte[768]);

            Assert.True(service.LoadPalette(palette), String.Join("|", ui.Errors));

            Assert.True(service.IsModified);
        }

        [Fact]
        public void AScriptThatEditsTheProjectModifiesIt()
        {
            using TempDir dir = new();
            Project project = TwoFrames(dir, out SilentInteraction ui, out ProjectService service);
            String script = dir.File("s.fas");
            File.WriteAllText(script, "SET_OPTION,FPS,24");

            Assert.True(new ScriptRunner(service) { ChangeCurrentDirectory = false }.Run(script), String.Join("|", ui.Errors));

            Assert.True(service.IsModified);
        }

        [Fact]
        public void AScriptThatSavesLeavesTheProjectClean()
        {
            using TempDir dir = new();
            Project project = TwoFrames(dir, out SilentInteraction ui, out ProjectService service);
            String script = dir.File("s.fas");
            File.WriteAllText(script, "SET_OPTION,FPS,24\r\nSAVE," + dir.File("out.fpr") + ",BMP");

            Assert.True(new ScriptRunner(service) { ChangeCurrentDirectory = false }.Run(script), String.Join("|", ui.Errors));

            Assert.False(service.IsModified);
        }

        [Fact]
        public void TheSignatureIgnoresWhatIsNotStoredInAProject()
        {
            using TempDir dir = new();
            Project project = TwoFrames(dir, out _, out _);
            String before = project.ContentSignature();

            project.CurrentDirection = 3;
            project.Settings.LockOffsets = false;
            project.ProjectFile = dir.File("elsewhere.fpr");

            Assert.Equal(before, project.ContentSignature());
        }

        [Fact]
        public void TheSignatureSeesWhatIsStored()
        {
            using TempDir dir = new();
            Project project = TwoFrames(dir, out _, out _);
            String before = project.ContentSignature();

            project.Directions[0].Frames[1].OffsetY += 1;

            Assert.NotEqual(before, project.ContentSignature());
        }
    }
}
