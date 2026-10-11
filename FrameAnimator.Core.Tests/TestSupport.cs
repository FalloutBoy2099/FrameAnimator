namespace FrameAnimator.Core.Tests
{
    using System;
    using System.IO;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Services;

    internal sealed class TempDir : IDisposable
    {
        public String Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fa_tests_" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public String File(String name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            try
            { Directory.Delete(Path, recursive: true); }
            catch { /* ignore */ }
        }
    }

    internal static class Samples
    {
        /// <summary>w×h bitmap filled with <paramref name="bg"/> with a rectangle of color <paramref name="fg"/>.</summary>
        public static Bitmap32 Rect(Int32 w, Int32 h, Int32 bg, Int32 fg, Int32 x0, Int32 y0, Int32 x1, Int32 y1)
        {
            var b = new Bitmap32(w, h);
            Array.Fill(b.Pixels, bg);
            for (Int32 y = y0; y <= y1; y++)
                for (Int32 x = x0; x <= x1; x++)
                    b[x, y] = fg;
            return b;
        }

        public static Project NewProject(out SilentInteraction ui, out ProjectService svc)
        {
            var project = new Project();
            ui = new SilentInteraction();
            svc = new ProjectService(project, ui, appDirectory: "");
            svc.NewProject();
            return project;
        }

        /// <summary>Fills a direction with <paramref name="count"/> 16×16 frames (background = palette[0], moving square).</summary>
        public static void Fill(Project p, Int32 dir, Int32 count, Int32 fg = 0x00FF00)
        {
            Int32 bg = p.Palette[0];
            for (Int32 i = 0; i < count; i++)
            {
                var f = new Frame(Rect(16, 16, bg, fg, 2 + i, 3, 9 + i, 12)) { FileName = $"dummy{dir}_{i}.bmp" };
                p.Directions[dir].Frames.Add(f);
            }
        }
    }
}