namespace FrameAnimator.Core.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using FrameAnimator.Core.Formats;
    using FrameAnimator.Core.Geometry;
    using FrameAnimator.Core.Imaging;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Services;

    public class TextAndMaskTests
    {
        [Theory]
        [InlineData("abc.fpr", "*.*", "abc.fpr")]
        [InlineData("x.fpr", "??????ab.fpr", "xab.fpr")]
        [InlineData("hero.frm", "*.FPR", "hero.FPR")]
        [InlineData("hero.frm", "new_*.fpr", "new_.fpr")] // les littéraux consomment la source en parallèle (sémantique REN)
        [InlineData("abc.def", "?b?.*", "abc.def")]
        [InlineData("name.ext", "fixed.txt", "fixed.txt")]
        public void MaskReproducesRenSemantics(String source, String mask, String expected)
            => Assert.Equal(expected, NameMask.Apply(source, mask));

        [Theory]
        [InlineData("a,b,c", 0, "a")]
        [InlineData("a, b ,c", 1, "b")]
        [InlineData("a,b,c", 2, "c")]
        [InlineData("a,b,c", 3, "")]
        [InlineData("", 0, "")]
        [InlineData("1,2,C:\\x\\y.gif,3", 2, "C:\\x\\y.gif")]
        public void CsvField(String s, Int32 n, String expected) => Assert.Equal(expected, Csv.Field(s, n));

        [Fact]
        public void KeyValueListIsCaseInsensitiveAndFirstWins()
        {
            var kv = KeyValueList.Parse("Fps=10\r\nfps=20\r\nName=a=b");
            Assert.Equal("10", kv["FPS"]);
            Assert.Equal("a=b", kv["name"]);
            Assert.Equal("", kv["missing"]);
        }

        [Fact]
        public void DelphiPathHelpers()
        {
            Assert.Equal("C:\\a\\", PathHelpers.ExtractFilePath("C:\\a\\b.txt"));
            Assert.Equal("b.txt", PathHelpers.ExtractFileName("C:\\a\\b.txt"));
            Assert.Equal(".txt", PathHelpers.ExtractFileExt("C:\\a.b\\b.txt"));
            Assert.Equal("", PathHelpers.ExtractFileExt("C:\\a.b\\b"));
            Assert.Equal("C:\\a\\b.FPR", PathHelpers.ChangeFileExt("C:\\a\\b.frm", ".FPR"));
            Assert.Equal("C:\\a\\b.FR3", PathHelpers.ChangeFileExt("C:\\a\\b", ".FR3"));
        }
    }

    public class PaletteTests
    {
        [Fact]
        public void DefaultPaletteMatchesOriginalFile()
        {
            var p = Palette.LoadDefault();
            Assert.Equal(0x0000FF, p[0]);          // fond bleu pur
            Assert.Equal(0xECECEC, p[1]);          // 2e entrée du fichier d'origine : EC EC EC
            Assert.Null(p.FalloutTable);
            var fromDisk = Palette.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Default.act"));
            Assert.Equal(p.Colors, fromDisk.Colors);
        }

        [Fact]
        public void PalLoaderScalesSixBitComponentsAndKeepsTable()
        {
            using var ms = new MemoryStream();
            for (Int32 i = 0; i < 256; i++)
            { ms.WriteByte((Byte)(i == 0 ? 0x10 : 63)); ms.WriteByte(0); ms.WriteByte(1); }
            ms.Write(new Byte[Palette.FalloutTableSize].Select((_, i) => (Byte)(i & 0xFF)).ToArray());
            ms.Position = 0;
            var p = Palette.LoadPal(ms);
            Assert.Equal((0x10 << 2) << 16 | (0 << 8) | (1 << 2), p[0]);
            Assert.Equal((252 << 16) | (0 << 8) | 4, p[1]);
            Assert.NotNull(p.FalloutTable);
            Assert.Equal(5, p.FalloutTable![5]);
        }

        [Fact]
        public void UnknownExtensionThrowsWithOriginalCase()
        {
            var e = Assert.Throws<UnknownPaletteTypeException>(() => Palette.LoadFromFile("x.Abc"));
            Assert.Equal("Unknown palette type '*.Abc'!", e.Message);
        }
    }

    public class GeometryTests
    {
        [Fact]
        public void RelativeAndSetRelativeAreInverse()
        {
            var p = Samples.NewProject(out _, out _);
            Samples.Fill(p, 0, 3);
            var d = p.Directions[0];
            foreach (var f in d.Frames)
                FrameMath.AutoCut(p, f);
            FrameMath.SetRelative(d.Frames[0], d.Frames[1], 5, -3);
            Assert.Equal((5, -3), FrameMath.Relative(d.Frames[0], d.Frames[1]));
            FrameMath.SetRelative(d.Frames[1], d.Frames[2], -2, 7);
            Assert.Equal((-2, 7), FrameMath.Relative(d.Frames[1], d.Frames[2]));
        }

        [Fact]
        public void ShiftRoundTrip()
        {
            var p = Samples.NewProject(out _, out _);
            Samples.Fill(p, 2, 2);
            var d = p.Directions[2];
            FrameMath.AutoCut(p, d.Frames[0]);
            FrameMath.SetShift(d, 12, -9);
            Assert.Equal((12, -9), FrameMath.GetShift(d));
            // l'auto‑coupe recadre : la base interne compense le centre du rectangle utile
            Assert.Equal(12 - FrameMath.CenterX(d.Frames[0]), d.ShiftBaseX);
        }

        [Fact]
        public void AutoCutFindsBoundingBoxAndHonoursTransparentMode()
        {
            var p = Samples.NewProject(out _, out _);
            p.Settings.TransparentMode = TransparentMode.Palette;
            var f = new Frame(Samples.Rect(20, 10, p.Palette[0], 0xFF0000, 4, 2, 11, 7));
            FrameMath.AutoCut(p, f);
            Assert.Equal((4, 2, 8, 6), (f.Left, f.Top, f.Width, f.Height));

            p.Settings.AutoCutting = false;
            FrameMath.AutoCut(p, f);
            Assert.Equal((0, 0, 20, 10), (f.Left, f.Top, f.Width, f.Height));

            p.Settings.AutoCutting = true;
            p.Settings.TransparentMode = TransparentMode.Sample;
            p.Settings.SampleColor = 0xFF0000; // le carré devient « transparent » : le fond bleu est le contenu
            FrameMath.AutoCut(p, f);
            Assert.Equal((0, 0, 20, 10), (f.Left, f.Top, f.Width, f.Height));
        }

        [Fact]
        public void FullyTransparentFrameKeepsLegacyDegenerateRectangle()
        {
            var p = Samples.NewProject(out _, out _);
            var f = new Frame(Samples.Rect(8, 5, 0x123456, 0x123456, 0, 0, 0, 0));
            FrameMath.AutoCut(p, f); // mode Auto : pixel (0,0) = transparent → plus rien d'opaque
            Assert.Equal((8, 5, 1 - 8, 1 - 5), (f.Left, f.Top, f.Width, f.Height));
        }

        [Fact]
        public void DisplayPositionAnchorsOnCenterAndBottom()
        {
            var p = Samples.NewProject(out _, out _);
            Samples.Fill(p, 0, 2);
            var d = p.Directions[0];
            var (x, y) = FrameMath.DisplayPosition(d, 0, 100, 100);
            Assert.Equal((100 - 8, 100 - 16), (x, y));
            d.ShiftBaseX = 3;
            d.ShiftBaseY = -4;
            d.FirstFrameOffsetX = 1;
            d.FirstFrameOffsetY = 2;
            d.Frames[1].OffsetX = 5;
            var (x1, y1) = FrameMath.DisplayPosition(d, 1, 100, 100);
            Assert.Equal((100 - 8 + 5 + 3 + 1, 100 - 16 - 4 + 2), (x1, y1));
        }
    }

    public class ColorIndexerTests
    {
        [Fact]
        public void ExactMatchesAndTransparencyAndNearest()
        {
            var pal = Palette.LoadDefault();
            var idx = new ColorIndexer(pal, DitherMethod.Default, pal[0]);
            Assert.Equal(0, idx.Index(pal[0]));                 // transparent
            Assert.Equal(5, idx.Index(pal[5]));                 // correspondance exacte
            Assert.Equal(1, idx.Index(0xFFFFFF) is var v && v >= 1 ? 1 : 0);
            // proche d'une entrée connue : doit retomber sur elle
            Int32 c = pal[40];
            Int32 near = Bitmap32.Pack(Math.Min(255, Bitmap32.Red(c) + 1), Bitmap32.Green(c), Bitmap32.Blue(c));
            if (near != c)
                Assert.Equal(idx.Index(c), idx.Index(near));
        }

        [Fact]
        public void AdvancedUsesLumaWeights()
        {
            // deux candidats : (R+10) ou (B+10) ; la métrique pondérée préfère l'erreur sur B (poids 11 < 30)
            var pal = new Palette();
            pal.Colors[1] = 0x6464 + 0x0A0000 + 0x5000 * 0; // R=0x0A … peu importe
            Array.Fill(pal.Colors, 0x000000);
            pal.Colors[1] = Bitmap32.Pack(100 + 10, 100, 100);
            pal.Colors[2] = Bitmap32.Pack(100, 100, 100 + 10);
            Int32 target = Bitmap32.Pack(100, 100, 100);
            Assert.Equal(1, new ColorIndexer(pal, DitherMethod.Default, -1, legacy: false).Index(target));   // égalité de distance → 1er
            Assert.Equal(2, new ColorIndexer(pal, DitherMethod.Advanced, -1, legacy: false).Index(target));  // 30*100 > 11*100
        }

        [Fact]
        public void LegacyMatchingReproducesUnsignedDifferenceBugOfTheOriginal()
        {
            // pal[2] est « presque exact » (R+1) mais l'original convertit la différence −1 en 4294967295 : il préfère pal[1], plus sombre.
            var pal = new Palette();
            Array.Fill(pal.Colors, 0x000000);
            pal.Colors[1] = Bitmap32.Pack(60, 60, 60);
            pal.Colors[2] = Bitmap32.Pack(101, 100, 100);
            Int32 target = Bitmap32.Pack(100, 100, 100);
            Assert.Equal(1, new ColorIndexer(pal, DitherMethod.Default, -1, legacy: true).Index(target));
            Assert.Equal(2, new ColorIndexer(pal, DitherMethod.Default, -1, legacy: false).Index(target));
            // une couleur exacte reste exacte dans les deux modes
            pal.Colors[3] = target;
            Assert.Equal(3, new ColorIndexer(pal, DitherMethod.Default, -1, legacy: true).Index(target));
        }

        [Fact]
        public void LegacyDistanceArithmetic()
        {
            Assert.Equal(0.0, ColorIndexer.LegacyDistance(0, 0, 0, false));
            Assert.Equal(25.0, ColorIndexer.LegacyDistance(3, 4, 0, false));
            Assert.Equal(30.0 * 9 + 59.0 * 16, ColorIndexer.LegacyDistance(3, 4, 0, true));
            Assert.Equal((Double)18446744065119617025UL, ColorIndexer.LegacyDistance(-1, 0, 0, false)); // (2^32−1)²
        }

        [Fact]
        public void FalloutUsesRgb555Table()
        {
            var pal = new Palette();
            using var ms = new MemoryStream();
            for (Int32 i = 0; i < 768; i++)
                ms.WriteByte(0);
            var table = new Byte[Palette.FalloutTableSize];
            table[(31 << 10) | (0 << 5) | 0] = 77;
            ms.Write(table);
            ms.Position = 0;
            var fallout = Palette.LoadPal(ms);
            var idx = new ColorIndexer(fallout, DitherMethod.Fallout, 0x123456);
            Assert.Equal(77, idx.Index(Bitmap32.Pack(255, 0, 0)));
            Assert.Equal(0, idx.Index(0x123456));
            _ = pal;
        }
    }

    public class FrmRoundTripTests
    {
        [Fact]
        public void WritesSpecCompliantHeaderAndReadsBack()
        {
            using var dir = new TempDir();
            var p = Samples.NewProject(out var ui, out var svc);
            p.Settings.TransparentMode = TransparentMode.Palette;
            p.Settings.AutoCutting = true;
            p.Settings.Fps = 12;
            p.Settings.ActionFrame = 2;
            for (Int32 d = 0; d < 6; d++)
                Samples.Fill(p, d, 3, fg: p.Palette[10 + d]);
            p.Directions[1].ShiftBaseX = 4;
            p.Directions[1].ShiftBaseY = -2;
            p.Directions[0].FirstFrameOffsetX = 7;
            p.Directions[0].FirstFrameOffsetY = -1;
            p.Directions[0].Frames[2].OffsetX = 3;

            String path = dir.File("hero.frm");
            Assert.True(svc.SaveFrm(path), String.Join("|", ui.Errors));

            Byte[] b = File.ReadAllBytes(path);
            // en‑tête (grand‑boutiste)
            Assert.Equal(4, BitConverter.ToInt32(b[..4].Reverse().ToArray()));
            Assert.Equal(12, (b[4] << 8) | b[5]);
            Assert.Equal(2, (b[6] << 8) | b[7]);
            Assert.Equal(3, (b[8] << 8) | b[9]);
            // frame area size = 6 directions × 3 frames × (12 + 8×10)
            Int32 expectedArea = 6 * 3 * (12 + 8 * 10);
            Assert.Equal(expectedArea, (b[58] << 24) | (b[59] << 16) | (b[60] << 8) | b[61]);
            Assert.Equal(62 + expectedArea, b.Length);
            // dataOffset[1] = taille d'une direction
            Int32 off1 = (b[34 + 4] << 24) | (b[34 + 5] << 16) | (b[34 + 6] << 8) | b[34 + 7];
            Assert.Equal(3 * (12 + 8 * 10), off1);
            // 1re frame : w=8, h=10, size=80, offset = first frame offset (7,-1)
            Assert.Equal(8, (b[62] << 8) | b[63]);
            Assert.Equal(10, (b[64] << 8) | b[65]);
            Assert.Equal(80, (b[66] << 24) | (b[67] << 16) | (b[68] << 8) | b[69]);
            Assert.Equal(7, (Int16)((b[70] << 8) | b[71]));
            Assert.Equal(-1, (Int16)((b[72] << 8) | b[73]));

            // relecture
            var p2 = Samples.NewProject(out _, out var svc2);
            Assert.True(svc2.LoadFrm(path));
            Assert.Equal(12, p2.Settings.Fps);
            Assert.Equal(2, p2.Settings.ActionFrame);
            Assert.Equal(TransparentMode.Palette, p2.Settings.TransparentMode);
            Assert.False(p2.Settings.AutoCutting);
            for (Int32 d = 0; d < 6; d++)
                Assert.Equal(3, p2.Directions[d].Count);
            Assert.Equal(7, p2.Directions[0].FirstFrameOffsetX);
            Assert.Equal(-1, p2.Directions[0].FirstFrameOffsetY);

            // décalage de direction restitué
            var layoutShift = FrameMath.GetShift(p.Directions[1]);
            Assert.Equal(layoutShift, FrameMath.GetShift(p2.Directions[1]));
            // relatif conservé
            foreach (var f in p.Directions[0].Frames)
                FrameMath.AutoCut(p, f);
            Assert.Equal(FrameMath.Relative(p.Directions[0].Frames[1], p.Directions[0].Frames[2]),
                         FrameMath.Relative(p2.Directions[0].Frames[1], p2.Directions[0].Frames[2]));
            // pixels : la frame est devenue un bitmap 8×10 recadré rempli de la couleur du carré
            var f0 = p2.Directions[0].Frames[0];
            Assert.Equal((8, 10), (f0.Bitmap.Width, f0.Bitmap.Height));
            Assert.All(f0.Bitmap.Pixels, px => Assert.Equal(p.Palette[10], px));
            Assert.Equal("hero000000", f0.FileName);
            Assert.Equal("hero050002", p2.Directions[5].Frames[2].FileName);
        }

        [Fact]
        public void SingleDirectionFrmAndIncompleteSets()
        {
            using var dir = new TempDir();
            var p = Samples.NewProject(out var ui, out var svc);
            p.Settings.TransparentMode = TransparentMode.Palette;
            Samples.Fill(p, 0, 2);
            String path = dir.File("one.frm");
            Assert.True(svc.SaveFrm(path));
            var b = File.ReadAllBytes(path);
            // toutes les directions autres que 0 ont un dataOffset 0
            for (Int32 d = 0; d < 6; d++)
                Assert.Equal(0, (b[34 + 4 * d] << 24) | (b[35 + 4 * d] << 16) | (b[36 + 4 * d] << 8) | b[37 + 4 * d]);

            Samples.Fill(p, 3, 2); // incohérent : dir 3 remplie mais pas 1,2,4,5
            Assert.False(svc.SaveFrm(dir.File("bad.frm")));
            Assert.Contains("Incomplete frames set for 'bad.frm'!", ui.Errors);
            Assert.False(svc.SaveFrm(dir.File("bad.xyz")));
            Assert.Contains("Unknown FRM file type '*.xyz'!", ui.Errors);
        }

        [Fact]
        public void FrNFilesRoundTrip()
        {
            using var dir = new TempDir();
            var p = Samples.NewProject(out var ui, out var svc);
            p.Settings.TransparentMode = TransparentMode.Palette;
            for (Int32 d = 0; d < 6; d++)
                Samples.Fill(p, d, 2);
            for (Int32 d = 0; d < 6; d++)
            { p.Directions[d].ShiftBaseX = d; p.Directions[d].ShiftBaseY = -d; }
            String path = dir.File("walk.fr0");
            Assert.True(svc.SaveFrm(path), String.Join("|", ui.Errors));
            for (Int32 d = 0; d < 6; d++)
                Assert.True(File.Exists(dir.File($"walk.FR{d}")));

            var p2 = Samples.NewProject(out _, out var svc2);
            Assert.True(svc2.LoadFrm(path.Replace(".fr0", ".FR3")));
            for (Int32 d = 0; d < 6; d++)
            {
                Assert.Equal(2, p2.Directions[d].Count);
                Assert.Equal(FrameMath.GetShift(p.Directions[d]), FrameMath.GetShift(p2.Directions[d]));
            }
            // FrameAreaSize contient le total des 6 directions dans chaque fichier .FRn
            var b = File.ReadAllBytes(dir.File("walk.FR2"));
            Int32 area = (b[58] << 24) | (b[59] << 16) | (b[60] << 8) | b[61];
            Assert.Equal(6 * 2 * (12 + 8 * 10), area);
            Assert.Equal(62 + 2 * (12 + 8 * 10), b.Length);
        }

        [Fact]
        public void FalloutDitheringNeedsPalTable()
        {
            using var dir = new TempDir();
            var p = Samples.NewProject(out var ui, out var svc);
            Samples.Fill(p, 0, 1);
            p.Settings.DitherMethod = DitherMethod.Fallout;
            Assert.False(svc.SaveFrm(dir.File("f.frm")));
            Assert.Contains("Invalid palette type for dithering method 'Fallout'\nUnable to write FRM 'f.frm'!", ui.Errors);
        }
    }

    public class ProjectFileTests
    {
        [Fact]
        public void FprRoundTripWithImagesOnDisk()
        {
            using var dir = new TempDir();
            var p = Samples.NewProject(out var ui, out var svc);
            for (Int32 i = 0; i < 2; i++)
            {
                String f = dir.File($"img{i}.bmp");
                BmpCodec.WriteFile(Samples.Rect(12, 9, 0x0000FF, 0xFFFF00, 1 + i, 1, 6 + i, 7), f);
                Assert.NotNull(svc.LoadFrameFile(p.Directions[0], f));
            }
            p.Directions[0].Frames[1].OffsetX = 3;
            p.Directions[0].Frames[1].OffsetY = -2;
            p.Directions[0].ShiftBaseX = 5;
            p.Directions[0].FirstFrameOffsetY = 9;
            p.Settings.AutoCutting = false;
            p.Settings.TransparentMode = TransparentMode.Sample;
            p.Settings.SampleColor = 0x0000FF;
            p.Settings.Fps = 14;
            p.Settings.ActionFrame = 1;
            p.Settings.DitherMethod = DitherMethod.Advanced;

            String fpr = dir.File("proj.fpr");
            Assert.True(svc.SaveProject(fpr), String.Join("|", ui.Errors));
            String text = File.ReadAllText(fpr);
            Assert.StartsWith("TransparentColor=255\r\nTransparentColorMode=3\r\nAutoCutting=0\r\nDitheringMethod=1\r\nFPS=14\r\nActionFrame=1\r\nPallete=", text);
            Assert.Contains("\r\nOffset(0)=5,0,0,9\r\n", text);
            Assert.Contains($"Frame(0,1)=3,-2,{dir.File("img1.bmp")}\r\n", text);

            var p2 = Samples.NewProject(out var ui2, out var svc2);
            Assert.True(svc2.LoadProject(fpr), String.Join("|", ui2.Errors));
            Assert.Equal(2, p2.Directions[0].Count);
            Assert.Equal(3, p2.Directions[0].Frames[1].OffsetX);
            Assert.Equal(-2, p2.Directions[0].Frames[1].OffsetY);
            Assert.Equal(5, p2.Directions[0].ShiftBaseX);
            Assert.Equal(9, p2.Directions[0].FirstFrameOffsetY);
            Assert.Equal(TransparentMode.Sample, p2.Settings.TransparentMode);
            Assert.Equal(14, p2.Settings.Fps);
            Assert.Equal(fpr, p2.ProjectFile);
        }

        [Fact]
        public void ProjectFromFrmWritesFrameImagesBeforeFpr()
        {
            using var dir = new TempDir();
            var p = Samples.NewProject(out var ui, out var svc);
            p.Settings.TransparentMode = TransparentMode.Palette;
            for (Int32 d = 0; d < 6; d++)
                Samples.Fill(p, d, 2);
            String frm = dir.File("mob.frm");
            Assert.True(svc.SaveFrm(frm));

            var p2 = Samples.NewProject(out var ui2, out var svc2);
            Assert.True(svc2.LoadFrm(frm));
            String fpr = dir.File("mob.fpr");
            Assert.True(svc2.SaveProject(fpr, dir.File("mob.PNG")), String.Join("|", ui2.Errors));
            Assert.True(File.Exists(dir.File("mob000000.PNG")));
            Assert.True(File.Exists(dir.File("mob050001.PNG")));
            String text = File.ReadAllText(fpr);
            Assert.Contains("mob000001.PNG", text);

            var p3 = Samples.NewProject(out _, out var svc3);
            Assert.True(svc3.LoadProject(fpr));
            Assert.Equal(2, p3.Directions[5].Count);
            Assert.Equal(p2.Directions[5].Frames[1].Bitmap.Pixels, p3.Directions[5].Frames[1].Bitmap.Pixels);
        }

        [Fact]
        public void LoadFailureAsksToContinue()
        {
            using var dir = new TempDir();
            String fpr = dir.File("broken.fpr");
            File.WriteAllText(fpr, "TransparentColor=0\r\nTransparentColorMode=0\r\nAutoCutting=1\r\nDitheringMethod=0\r\nFPS=0\r\nActionFrame=0\r\nPallete=\r\nBackground=\r\nFRM=\r\n" +
                "Offset(0)=0,0,0,0\r\nFrame(0,0)=0,0,Z:\\no\\such.bmp\r\n" +
                String.Concat(Enumerable.Range(1, 5).Select(d => $"Offset({d})=0,0,0,0\r\n")));
            var p = Samples.NewProject(out var ui, out var svc);
            Assert.False(svc.LoadProject(fpr));          // réponse « Non » : abandon silencieux
            Assert.Contains("Unable to load frame 'such.bmp'!", ui.Errors);
            Assert.Contains("Continue loading project 'broken.fpr'?", ui.Questions);

            var ui2 = new SilentInteraction(confirmAnswer: true);
            var p2 = new Project();
            var svc2 = new ProjectService(p2, ui2, "");
            svc2.NewProject();
            Assert.True(svc2.LoadProject(fpr));          // « Oui » : on continue
        }
    }

    public class ImagingTests
    {
        private static Bitmap32 Pattern(Int32 w, Int32 h)
        {
            var b = new Bitmap32(w, h);
            var rnd = new Random(1234);
            for (Int32 i = 0; i < b.Pixels.Length; i++)
                b.Pixels[i] = rnd.Next(0x1000000);
            return b;
        }

        [Fact]
        public void BmpRoundTrip()
        {
            var b = Pattern(13, 7);
            using var ms = new MemoryStream();
            BmpCodec.Write(b, ms);
            var back = BmpCodec.Read(ms.ToArray());
            Assert.Equal(b.Pixels, back.Pixels);
        }

        [Fact]
        public void PngRoundTrip()
        {
            var b = Pattern(31, 17);
            using var ms = new MemoryStream();
            PngCodec.Write(b, ms);
            Assert.Equal(b.Pixels, PngCodec.Read(ms.ToArray()).Pixels);
        }

        [Fact]
        public void GifRoundTripWithFewColors()
        {
            var b = new Bitmap32(40, 30);
            var rnd = new Random(7);
            Int32[] colors = [0x0000FF, 0xFF0000, 0x00FF00, 0x123456, 0xFFFFFF];
            for (Int32 i = 0; i < b.Pixels.Length; i++)
                b.Pixels[i] = colors[rnd.Next(colors.Length)];
            using var ms = new MemoryStream();
            GifCodec.Write(b, ms);
            var gif = GifCodec.Read(ms.ToArray());
            Assert.Single(gif.SubImages);
            Assert.Equal((40, 30), (gif.ScreenWidth, gif.ScreenHeight));
            Assert.Equal(b.Pixels, gif.SubImages[0].Bitmap.Pixels);
        }

        [Fact]
        public void GifWithManyColorsMapsToPalette()
        {
            var b = Pattern(33, 21);
            using var ms = new MemoryStream();
            GifCodec.Write(b, ms, Palette.LoadDefault());
            var gif = GifCodec.Read(ms.ToArray());
            var pal = Palette.LoadDefault();
            Assert.All(gif.SubImages[0].Bitmap.Pixels, px => Assert.Contains(px, pal.Colors));
        }
    }
}