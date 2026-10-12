namespace FrameAnimator.Core.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using FrameAnimator.Core.Imaging;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Services;
    using Xunit;

    /// <summary>Portable projects: relative paths on request, and projects that are moved or copied still load.</summary>
    public class PortabilityTests
    {
        /// <summary>A project folder with two BMP frames, a background and a palette copy, loaded in a service.</summary>
        private static ProjectService CreateProject(TempDir dir, out String projectFolder)
        {
            projectFolder = dir.File("work");
            Directory.CreateDirectory(Path.Combine(projectFolder, "img"));
            String folder = projectFolder;
            Project project = Samples.NewProject(out SilentInteraction ui, out ProjectService service);
            for (Int32 i = 0; i < 2; i++)
            {
                String file = Path.Combine(folder, "img", $"f{i}.bmp");
                BmpCodec.WriteFile(Samples.Rect(10 + i, 8, 0x102030, 0xE0A040, 2, 2, 6, 5), file);
                Assert.NotNull(service.LoadFrameFile(project.Directions[0], file));
            }
            String background = Path.Combine(folder, "back.bmp");
            BmpCodec.WriteFile(Samples.Rect(20, 20, 0x303030, 0x404040, 1, 1, 4, 4), background);
            Assert.True(service.LoadBackground(background), String.Join("|", ui.Errors));
            return service;
        }

        private static void CopyFolder(String from, String to)
        {
            foreach (String directory in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(directory.Replace(from, to));
            Directory.CreateDirectory(to);
            foreach (String file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                File.Copy(file, file.Replace(from, to));
        }

        [Fact]
        public void AbsolutePathsAreTheDefaultLikeTheOriginal()
        {
            using TempDir dir = new();
            ProjectService service = CreateProject(dir, out String folder);
            String fpr = Path.Combine(folder, "p.fpr");

            Assert.True(service.SaveProject(fpr));

            String text = File.ReadAllText(fpr);
            Assert.Contains(Path.Combine(folder, "img", "f0.bmp"), text);
            Assert.Contains("Background=" + Path.Combine(folder, "back.bmp"), text);
        }

        [Fact]
        public void RelativePathsAreWrittenWhenRequested()
        {
            using TempDir dir = new();
            ProjectService service = CreateProject(dir, out String folder);
            service.SaveRelativePaths = true;
            String fpr = Path.Combine(folder, "p.fpr");

            Assert.True(service.SaveProject(fpr));

            String text = File.ReadAllText(fpr);
            Assert.Contains("Frame(0,0)=0,0," + Path.Combine("img", "f0.bmp"), text);
            Assert.Contains("Frame(0,1)=0,0," + Path.Combine("img", "f1.bmp"), text);
            Assert.Contains("Background=back.bmp", text);
            Assert.DoesNotContain(dir.Path, text);
            // the project in memory keeps absolute paths: later saves elsewhere still work
            Assert.True(Path.IsPathRooted(service.Project.Directions[0].Frames[0].FileName));
        }

        [Fact]
        public void ARelativeProjectLoadsAfterTheFolderWasMoved()
        {
            using TempDir dir = new();
            ProjectService service = CreateProject(dir, out String folder);
            service.SaveRelativePaths = true;
            Assert.True(service.SaveProject(Path.Combine(folder, "p.fpr")));
            String moved = dir.File("elsewhere");
            CopyFolder(folder, moved);
            Directory.Delete(folder, recursive: true);

            Project project = Samples.NewProject(out SilentInteraction ui, out ProjectService loader);
            Assert.True(loader.LoadProject(Path.Combine(moved, "p.fpr")), String.Join("|", ui.Errors));

            Assert.Equal(2, project.Directions[0].Count);
            Assert.Equal(Path.Combine(moved, "img", "f1.bmp"), project.Directions[0].Frames[1].FileName);
            Assert.Equal(Path.Combine(moved, "back.bmp"), project.BackgroundFile);
            Assert.NotNull(project.Background);
            Assert.Empty(ui.Errors);
        }

        [Fact]
        public void AnAbsoluteProjectWhosePathsBrokeIsRepairedByLookingNextToIt()
        {
            using TempDir dir = new();
            ProjectService service = CreateProject(dir, out String folder);
            String fpr = Path.Combine(folder, "p.fpr");
            Assert.True(service.SaveProject(fpr)); // absolute paths of the old folder

            // the images are now next to the project file (flat), the old folder is gone
            String moved = dir.File("flat");
            Directory.CreateDirectory(moved);
            File.Copy(fpr, Path.Combine(moved, "p.fpr"));
            File.Copy(Path.Combine(folder, "img", "f0.bmp"), Path.Combine(moved, "f0.bmp"));
            File.Copy(Path.Combine(folder, "img", "f1.bmp"), Path.Combine(moved, "f1.bmp"));
            File.Copy(Path.Combine(folder, "back.bmp"), Path.Combine(moved, "back.bmp"));
            Directory.Delete(folder, recursive: true);

            Project project = Samples.NewProject(out SilentInteraction ui, out ProjectService loader);
            Assert.True(loader.LoadProject(Path.Combine(moved, "p.fpr")), String.Join("|", ui.Errors));

            Assert.Equal(2, project.Directions[0].Count);
            Assert.Equal(Path.Combine(moved, "f0.bmp"), project.Directions[0].Frames[0].FileName);
            Assert.Empty(ui.Errors);
        }

        [Fact]
        public void AMissingFileIsStillReportedWithItsName()
        {
            using TempDir dir = new();
            ProjectService service = CreateProject(dir, out String folder);
            service.SaveRelativePaths = true;
            String fpr = Path.Combine(folder, "p.fpr");
            Assert.True(service.SaveProject(fpr));
            File.Delete(Path.Combine(folder, "img", "f1.bmp"));

            Samples.NewProject(out SilentInteraction ui, out ProjectService loader);
            loader.LoadProject(fpr);

            Assert.Contains("Unable to load frame 'f1.bmp'!", ui.Errors);
            Assert.Contains("Continue loading project 'p.fpr'?", ui.Questions);
        }

        [Fact]
        public void ResolveStoredPathHandlesEmptyRelativeAndMissingPaths()
        {
            using TempDir dir = new();
            File.WriteAllText(dir.File("a.txt"), "x");

            Assert.Equal("", ProjectService.ResolveStoredPath("", dir.Path));
            Assert.Equal(dir.File("a.txt"), ProjectService.ResolveStoredPath("a.txt", dir.Path));
            Assert.Equal(dir.File("a.txt"), ProjectService.ResolveStoredPath(Path.Combine("sub", "..", "a.txt"), dir.Path));
            Assert.Equal(dir.File("a.txt"), ProjectService.ResolveStoredPath(Path.Combine(dir.Path, "gone", "a.txt"), dir.Path));
            String missing = Path.Combine(dir.Path, "gone", "zzz.txt");
            Assert.Equal(missing, ProjectService.ResolveStoredPath(missing, dir.Path));
            Assert.Equal("a.txt", ProjectService.ResolveStoredPath("a.txt", ""));
        }

        [Fact]
        public void FramesFromAnFrmAreWrittenNextToTheProjectAndStayRelative()
        {
            using TempDir dir = new();
            Project source = Samples.NewProject(out _, out ProjectService writer);
            source.Settings.TransparentMode = TransparentMode.Palette;
            Samples.Fill(source, 0, 2);
            String frm = dir.File("hero.frm");
            Assert.True(writer.SaveFrm(frm));

            Project project = Samples.NewProject(out SilentInteraction ui, out ProjectService service);
            service.SaveRelativePaths = true;
            Assert.True(service.LoadFrm(frm));
            String fpr = dir.File("hero.fpr");
            Assert.True(service.SaveProject(fpr, dir.File("hero.PNG")), String.Join("|", ui.Errors));

            String text = File.ReadAllText(fpr);
            Assert.Contains("Frame(0,0)=0,0,hero000000.PNG", text);
            Assert.Equal(2, project.Directions[0].Frames.Count(frame => File.Exists(frame.FileName)));
        }
    }
}
