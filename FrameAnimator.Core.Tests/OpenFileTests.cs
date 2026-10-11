namespace FrameAnimator.Core.Tests
{
    using System;
    using System.IO;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Services;
    using Xunit;

    public class OpenFileTests
    {
        private static String MakeFrm(TempDir dir)
        {
            var p = Samples.NewProject(out _, out var svc);
            p.Settings.TransparentMode = TransparentMode.Palette;
            Samples.Fill(p, 0, 2);
            String frm = dir.File("hero.frm");
            Assert.True(svc.SaveFrm(frm));
            return frm;
        }

        [Fact]
        public void OpensFrmAfterAskingForPaletteAndKeepsCurrentOneOnYes()
        {
            using var dir = new TempDir();
            String frm = MakeFrm(dir);

            var ui = new SilentInteraction(confirmAnswer: true);
            var svc = new ProjectService(new Project(), ui, appDirectory: "");
            Assert.True(svc.OpenFile(frm));
            Assert.Equal(2, svc.Project.Directions[0].Count);
            Assert.Contains("Use palette 'Default.act'?", ui.Questions);
            Assert.Equal(frm, svc.Project.ProjectFile);
        }

        [Fact]
        public void OpensFrmWithoutQuestionWhenConfirmationIsDisabled()
        {
            using var dir = new TempDir();
            String frm = MakeFrm(dir);

            var ui = new SilentInteraction(confirmAnswer: false);
            var svc = new ProjectService(new Project(), ui, appDirectory: "") { ConfirmPaletteOnOpen = false };
            Assert.True(svc.OpenFile(frm));
            Assert.Equal(2, svc.Project.Directions[0].Count);
            Assert.Empty(ui.Questions);
        }

        [Fact]
        public void NoThenCancelledPaletteDialogAbortsOpening()
        {
            using var dir = new TempDir();
            String frm = MakeFrm(dir);

            var ui = new SilentInteraction(confirmAnswer: false) { PaletteHandler = () => null };
            var svc = new ProjectService(new Project(), ui, appDirectory: "");
            Assert.False(svc.OpenFile(frm));
            Assert.Equal(0, svc.Project.Directions[0].Count);
        }

        [Fact]
        public void SiblingPalIsLoadedWithoutQuestion()
        {
            using var dir = new TempDir();
            String frm = MakeFrm(dir);
            // .PAL homonyme : 768 octets (6 bits) + table de 32 768 octets
            var pal = new Byte[768 + Palette.FalloutTableSize];
            File.WriteAllBytes(Path.ChangeExtension(frm, ".PAL"), pal);

            var ui = new SilentInteraction(confirmAnswer: false);
            var svc = new ProjectService(new Project(), ui, appDirectory: "");
            Assert.True(svc.OpenFile(frm));
            Assert.Empty(ui.Questions);
            Assert.NotNull(svc.Project.Palette.FalloutTable);
        }

        [Fact]
        public void UnknownExtensionIsReported()
        {
            var ui = new SilentInteraction();
            var svc = new ProjectService(new Project(), ui, appDirectory: "");
            Assert.False(svc.OpenFile("x.abc"));
            Assert.Contains("Unknown file type '*.abc'!", ui.Errors);
        }
    }
}
