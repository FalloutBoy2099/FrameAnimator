namespace FrameAnimator.Core.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using FrameAnimator.Core.Imaging;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;
    using FrameAnimator.Core.Services;

    /// <summary>
    /// Tests « golden » : les fichiers attendus ont été produits par l'exécutable ORIGINAL (FrameAnimator.exe 2.70.1) à partir
    /// des mêmes entrées (voir _legacy/RE/RETRO-INGENIERIE.md §10). Le portage doit les reproduire octet pour octet (FRM, BMP) ou
    /// pixel pour pixel (PNG, encodeur différent).
    /// </summary>
    public sealed class GoldenTests : IDisposable
    {
        private readonly TempDir _dir = new();
        private static String GoldenRoot => Path.Combine(AppContext.BaseDirectory, "Golden");
        private String G => _dir.Path;
        private String Out => Path.Combine(G, "out");

        public GoldenTests()
        {
            foreach (var f in Directory.GetFiles(GoldenRoot))
                File.Copy(f, Path.Combine(G, Path.GetFileName(f)));
            foreach (var f in Directory.GetFiles(Path.Combine(GoldenRoot, "expected")))
                File.Copy(f, Path.Combine(G, Path.GetFileName(f)));
            Directory.CreateDirectory(Out);
        }

        public void Dispose() => _dir.Dispose();

        private SilentInteraction Run(String scenario)
        {
            String template = File.ReadAllText(Path.Combine(GoldenRoot, "scripts", scenario + ".fas.tmpl"));
            String script = Path.Combine(G, scenario + ".fas");
            File.WriteAllText(script, template.Replace("{G}", G).Replace("{OUT}", Out));
            var ui = new SilentInteraction();
            var svc = new ProjectService(new Project(), ui, appDirectory: G);
            svc.NewProject();
            Assert.True(new ScriptRunner(svc) { ChangeCurrentDirectory = false }.Run(script), String.Join(" | ", ui.Errors));
            Assert.Empty(ui.Errors);
            return ui;
        }

        [Theory]
        [InlineData("A", "mine_A.frm", "orig_A.frm")]   // 1 direction, auto‑coupe, décalages, inherit
        [InlineData("B", "mine_B.frm", "orig_B.frm")]   // 6 directions, tramage « Advanced »
        [InlineData("C", "mine_C.frm", "orig_C.frm")]   // mode Sample + auto‑coupe
        [InlineData("E", "mine_E.frm", "orig_E.frm")]   // palette Fallout .PAL + tramage « Fallout »
        [InlineData("F", "mine_F.frm", "orig_F.frm")]   // GIF animé (sous‑images décalées) + PNG avec alpha
        [InlineData("G", "mine_G.frm", "orig_G.frm")]   // relecture FRM → réécriture
        [InlineData("H", "mine_H.frm", "orig_H.frm")]   // relecture FR0‑FR5 → FRM
        public void FrmIsByteIdenticalToOriginal(String scenario, String produced, String expected)
        {
            Run(scenario);
            Assert.Equal(File.ReadAllBytes(Path.Combine(G, expected)), File.ReadAllBytes(Path.Combine(G, produced)));
        }

        [Fact]
        public void FrnFilesAreByteIdenticalToOriginal()
        {
            Run("D");
            for (Int32 d = 0; d < 6; d++)
                Assert.Equal(File.ReadAllBytes(Path.Combine(G, $"orig_D.FR{d}")), File.ReadAllBytes(Path.Combine(G, $"mine_D.FR{d}")));
        }

        private String Normalize(String text)
            => Regex.Replace(text.Replace(Out, "<OUT>").Replace(G, "<G>"), "(?m)^Pallete=.*$", "Pallete=<PAL>");

        [Theory]
        [InlineData("S1")]   // ADD_FRAMES (masques, GIF), SET_FRAME_OFFSET/inherit, MOVE_FRAME, REMOVE_FRAMES, options, échantillon
        [InlineData("S2")]   // OPEN FRM, options, SAVE FPR + images PNG, SAVE FRM
        [InlineData("S3")]   // OPEN FR0 + palette, tramage Fallout, SAVE avec masques `*` et `?`, images BMP
        public void ScriptScenariosMatchOriginal(String scenario)
        {
            Run(scenario);
            String expectedDir = Path.Combine(GoldenRoot, "expected_s");
            String prefix = scenario switch { "S1" => "s1", "S2" => "s2", _ => "s3_g_" };
            var expectedFiles = Directory.GetFiles(expectedDir)
                .Where(f => scenario == "S3"
                    ? Path.GetFileName(f).StartsWith("s3_g_", StringComparison.Ordinal) || Path.GetFileName(f) == "orig_D.frm"
                    : Path.GetFileName(f).StartsWith(prefix, StringComparison.Ordinal))
                .ToList();
            Assert.NotEmpty(expectedFiles);

            foreach (String exp in expectedFiles)
            {
                String name = Path.GetFileName(exp);
                String got = Path.Combine(Out, name);
                Assert.True(File.Exists(got), "Fichier manquant : " + name);
                switch (Path.GetExtension(name).ToLowerInvariant())
                {
                    case ".fpr":
                        Assert.Equal(File.ReadAllText(exp, System.Text.Encoding.Latin1), Normalize(File.ReadAllText(got, System.Text.Encoding.Latin1)));
                        break;
                    case ".png":
                        Assert.Equal(PngCodec.ReadFile(exp).Pixels, PngCodec.ReadFile(got).Pixels);
                        break;
                    default: // .BMP, .FRM : octet pour octet
                        Assert.Equal(File.ReadAllBytes(exp), File.ReadAllBytes(got));
                        break;
                }
            }
        }
    }
}