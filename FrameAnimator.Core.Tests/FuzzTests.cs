namespace FrameAnimator.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using FrameAnimator.Core.Imaging;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Scripting;
    using FrameAnimator.Core.Services;

    /// <summary>
    /// Deterministic mutation fuzzing of every file reader (fixed seeds: a failure is reproducible from the seed and the
    /// iteration number printed in the message). The iteration count per target is overridable with the
    /// <c>FA_FUZZ_ITERATIONS</c> environment variable for longer local runs.
    /// </summary>
    public sealed class FuzzTests : IDisposable
    {
        private readonly TempDir _dir = new();

        private static String GoldenRoot => Path.Combine(AppContext.BaseDirectory, "Golden");

        public void Dispose() => _dir.Dispose();

        private static Byte[] Golden(String name) => File.ReadAllBytes(Path.Combine(GoldenRoot, name));

        private static Byte[] Golden(String sub, String name) => File.ReadAllBytes(Path.Combine(GoldenRoot, sub, name));

        // ------------------------------------------------------------------ image codecs

        private static void FuzzCodec(String name, Int32 seed, Byte[][] seeds, Func<Byte[], Object> decode, Int32 defaultIterations)
        {
            Int32 iterations = FuzzEngine.Iterations(defaultIterations);
            var rnd = new Random(seed);
            Int32 decoded = 0, rejected = 0;
            for (Int32 i = 0; i < iterations; i++)
            {
                Byte[] source = seeds[i % seeds.Length];
                Byte[] mutated = FuzzEngine.Mutate(source, rnd, out String how);
                String label = $"{name} seed={seed} iteration={i} ({how}, {mutated.Length} bytes)";
                Exception? e = FuzzEngine.RunBounded(label, () => decode(mutated));
                FuzzEngine.AssertAllowed(e, label, typeof(ImageFormatException), typeof(IOException));
                if (e is null)
                    decoded++;
                else
                    rejected++;
            }
            Console.Error.WriteLine($"{name}: {decoded} decoded, {rejected} rejected");
            Assert.True(rejected > 0, $"{name}: no mutation was rejected, the mutator is ineffective");
        }

        [Fact]
        public void BmpMutationsOnlyThrowImageFormatException()
            => FuzzCodec("BMP", 1001, [Golden("a0.bmp"), Golden("b0.bmp"), Golden("a2.bmp")], BmpCodec.Read, 4000);

        [Fact]
        public void BmpOtherBitDepthsAndRleMutationsOnlyThrowImageFormatException()
        {
            // seeds that exercise the paletted, 16/32 bit and RLE paths, which the golden 24-bit files do not reach
            var rnd = new Random(1002);
            var seeds = new List<Byte[]>();
            foreach (Int32 bpp in new[] { 1, 4, 8, 16, 32 })
            {
                Byte[] s = CraftedFiles.Bmp(9, 7, bpp, payload: 2048);
                for (Int32 i = 54; i < s.Length; i++)
                    s[i] = (Byte)rnd.Next(256);
                seeds.Add(s);
            }
            Byte[] rle8 = CraftedFiles.Bmp(9, 7, 8, compression: 1, payload: 1200, pixelOffset: 54 + 1024);
            for (Int32 i = 54 + 1024; i + 1 < rle8.Length; i += 2)
            {
                rle8[i] = (Byte)rnd.Next(0, 6);
                rle8[i + 1] = (Byte)rnd.Next(0, 6);
            }
            seeds.Add(rle8);
            Byte[] rle4 = CraftedFiles.Bmp(9, 7, 4, compression: 2, payload: 1200, pixelOffset: 54 + 64);
            for (Int32 i = 54 + 64; i + 1 < rle4.Length; i += 2)
            {
                rle4[i] = (Byte)rnd.Next(0, 6);
                rle4[i + 1] = (Byte)rnd.Next(0, 6);
            }
            seeds.Add(rle4);
            FuzzCodec("BMP-variants", 1003, [.. seeds], BmpCodec.Read, 4000);
        }

        [Fact]
        public void PngMutationsOnlyThrowImageFormatException()
            => FuzzCodec("PNG", 2001, [Golden("p0.png")], PngCodec.Read, 4000);

        [Fact]
        public void PngMutationsWithRepairedCrcOnlyThrowImageFormatException()
        {
            // The CRC check rejects almost every random mutation before the decoder sees it: mutate the chunk bodies
            // and recompute the CRC to reach the IHDR / PLTE / filter / pixel decoding code.
            Byte[] png = Golden("p0.png");
            Int32 iterations = FuzzEngine.Iterations(4000);
            var rnd = new Random(2002);
            Int32 decoded = 0;
            for (Int32 i = 0; i < iterations; i++)
            {
                Byte[] mutated = FuzzEngine.Mutate(png, rnd, out String how);
                RepairPngCrcs(mutated);
                String label = $"PNG-crc seed=2002 iteration={i} ({how}, {mutated.Length} bytes)";
                Exception? e = FuzzEngine.RunBounded(label, () => PngCodec.Read(mutated));
                FuzzEngine.AssertAllowed(e, label, typeof(ImageFormatException), typeof(IOException));
                if (e is null)
                    decoded++;
            }
            Console.Error.WriteLine($"PNG-crc: {decoded} decoded, {iterations - decoded} rejected");
            Assert.True(decoded > 0, "no mutated PNG decoded: the mutations never reach the pixel decoder");
        }

        /// <summary>Recomputes the CRC of every chunk that fits in the buffer (best effort, ignores a broken structure).</summary>
        private static void RepairPngCrcs(Byte[] d)
        {
            Int32 pos = 8;
            while (pos + 12 <= d.Length)
            {
                UInt32 len = (UInt32)((d[pos] << 24) | (d[pos + 1] << 16) | (d[pos + 2] << 8) | d[pos + 3]);
                if (len > d.Length || pos + 12L + len > d.Length)
                    return;
                Int32 l = (Int32)len;
                UInt32 crc = CraftedFiles.Crc(d.AsSpan(pos + 4, 4 + l));
                Int32 c = pos + 8 + l;
                d[c] = (Byte)(crc >> 24);
                d[c + 1] = (Byte)(crc >> 16);
                d[c + 2] = (Byte)(crc >> 8);
                d[c + 3] = (Byte)crc;
                pos += 12 + l;
            }
        }

        [Fact]
        public void GifMutationsOnlyThrowImageFormatException()
            => FuzzCodec("GIF", 3001, [Golden("g0.gif")], GifCodec.Read, 4000);

        // ------------------------------------------------------------------ FRM

        private void RunLoad(String label, Func<ProjectService, Boolean> load, Boolean confirm)
        {
            var project = new Project();
            var ui = new SilentInteraction(confirm);
            var svc = new ProjectService(project, ui, appDirectory: _dir.Path);
            svc.NewProject();
            Exception? e = FuzzEngine.RunBounded(label, () => load(svc));
            FuzzEngine.AssertAllowed(e, label);   // nothing may escape: failures are reported through the user interaction
        }

        [Fact]
        public void FrmMutationsNeverEscapeLoadFrm()
        {
            Byte[][] seeds = [Golden("expected", "orig_A.frm"), Golden("expected", "orig_B.frm"), Golden("expected", "orig_F.frm"), CraftedFiles.Frm(8, 8, 64)];
            Int32 iterations = FuzzEngine.Iterations(400);
            var rnd = new Random(4001);
            String path = _dir.File("fuzz.frm");
            for (Int32 i = 0; i < iterations; i++)
            {
                Byte[] mutated = FuzzEngine.Mutate(seeds[i % seeds.Length], rnd, out String how);
                File.WriteAllBytes(path, mutated);
                RunLoad($"FRM seed=4001 iteration={i} ({how}, {mutated.Length} bytes)", s => s.LoadFrm(path), confirm: i % 2 == 0);
            }
        }

        [Fact]
        public void FrmDirectionSetMutationsNeverEscapeLoadFrm()
        {
            // .FR0 loads .FR0 ... .FR5 (one file per direction)
            Int32 iterations = FuzzEngine.Iterations(90);
            var rnd = new Random(4002);
            Byte[] seed = Golden("expected", "orig_D.FR0");
            for (Int32 i = 0; i < iterations; i++)
            {
                String how = "";
                for (Int32 d = 0; d < 6; d++)
                {
                    Byte[] bytes = seed;
                    if (d == i % 6 || rnd.Next(4) == 0)
                        bytes = FuzzEngine.Mutate(seed, rnd, out how);
                    File.WriteAllBytes(_dir.File($"set.FR{d}"), bytes);
                }
                String path = _dir.File("set.FR0");
                RunLoad($"FR0-5 seed=4002 iteration={i} ({how})", s => s.LoadFrm(path), confirm: false);
            }
        }

        // ------------------------------------------------------------------ palettes

        [Fact]
        public void PaletteMutationsNeverEscapeLoadPalette()
        {
            Byte[] pal = Golden("fo.pal");
            Byte[] act = new Byte[768];
            new Random(5000).NextBytes(act);
            Int32 iterations = FuzzEngine.Iterations(150);
            var rnd = new Random(5001);
            for (Int32 i = 0; i < iterations; i++)
            {
                Boolean isPal = i % 2 == 0;
                Byte[] mutated = FuzzEngine.Mutate(isPal ? pal : act, rnd, out String how);
                String path = _dir.File(isPal ? "fuzz.pal" : "fuzz.act");
                File.WriteAllBytes(path, mutated);
                RunLoad($"{(isPal ? "PAL" : "ACT")} seed=5001 iteration={i} ({how}, {mutated.Length} bytes)", s => s.LoadPalette(path), confirm: false);
            }
        }

        // ------------------------------------------------------------------ FPR

        /// <summary>Creates a real project file (with its PNG frames) by running a golden script, and returns the FPR text.</summary>
        private String CreateFprSeed()
        {
            foreach (String f in Directory.GetFiles(GoldenRoot))
                File.Copy(f, Path.Combine(_dir.Path, Path.GetFileName(f)), overwrite: true);
            foreach (String f in Directory.GetFiles(Path.Combine(GoldenRoot, "expected")))
                File.Copy(f, Path.Combine(_dir.Path, Path.GetFileName(f)), overwrite: true);
            String outDir = Path.Combine(_dir.Path, "out");
            Directory.CreateDirectory(outDir);
            String script = Path.Combine(_dir.Path, "S2.fas");
            File.WriteAllText(script, File.ReadAllText(Path.Combine(GoldenRoot, "scripts", "S2.fas.tmpl")).Replace("{G}", _dir.Path).Replace("{OUT}", outDir));
            var ui = new SilentInteraction();
            var svc = new ProjectService(new Project(), ui, appDirectory: _dir.Path);
            svc.NewProject();
            Assert.True(new ScriptRunner(svc) { ChangeCurrentDirectory = false }.Run(script), String.Join(" | ", ui.Errors));
            return File.ReadAllText(Path.Combine(outDir, "s2.fpr"), System.Text.Encoding.Latin1);
        }

        [Fact]
        public void FprMutationsNeverEscapeLoadProject()
        {
            String fpr = CreateFprSeed();
            String[] lines = fpr.Replace("\r", "").Split('\n');
            Assert.True(lines.Length > 10);
            Int32 iterations = FuzzEngine.Iterations(150);
            var rnd = new Random(6001);
            String path = Path.Combine(_dir.Path, "out", "fuzz.fpr");
            for (Int32 i = 0; i < iterations; i++)
            {
                String how;
                String text;
                if (i % 4 == 0)
                {
                    // byte-level mutation of the whole file
                    Byte[] bytes = FuzzEngine.Mutate(System.Text.Encoding.Latin1.GetBytes(fpr), rnd, out how);
                    text = System.Text.Encoding.Latin1.GetString(bytes);
                }
                else
                {
                    // text-level mutation of one or two lines
                    var copy = (String[])lines.Clone();
                    for (Int32 k = 0; k < 1 + rnd.Next(2); k++)
                    {
                        Int32 li = rnd.Next(copy.Length);
                        copy[li] = FuzzEngine.MutateText(copy[li], rnd);
                    }
                    how = "text";
                    text = String.Join("\r\n", copy);
                }
                if (text.Contains("\\\\") || text.Contains("//"))
                    continue;   // never let a mutation point at a UNC / network path
                File.WriteAllText(path, text, System.Text.Encoding.Latin1);
                RunLoad($"FPR seed=6001 iteration={i} ({how})", s => s.LoadProject(path), confirm: i % 2 == 0);
            }
        }

        // ------------------------------------------------------------------ FAS scripts

        [Fact]
        public void ScriptLineMutationsNeverEscapeScriptRunner()
        {
            foreach (String f in Directory.GetFiles(GoldenRoot))
                File.Copy(f, Path.Combine(_dir.Path, Path.GetFileName(f)), overwrite: true);
            foreach (String f in Directory.GetFiles(Path.Combine(GoldenRoot, "expected")))
                File.Copy(f, Path.Combine(_dir.Path, Path.GetFileName(f)), overwrite: true);
            String outDir = Path.Combine(_dir.Path, "out");
            Directory.CreateDirectory(outDir);

            String[] names = ["A", "B", "C", "D", "E", "F", "S1", "S2", "S3"];
            var templates = names.Select(n => File.ReadAllText(Path.Combine(GoldenRoot, "scripts", n + ".fas.tmpl"))
                .Replace("{G}", _dir.Path).Replace("{OUT}", outDir)
                .Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0).ToArray()).ToArray();

            Int32 iterations = FuzzEngine.Iterations(100);
            var rnd = new Random(7001);
            String scriptPath = Path.Combine(_dir.Path, "fuzz.fas");
            String root = Path.GetFullPath(_dir.Path);
            var unexpected = new List<String>();

            for (Int32 i = 0; i < iterations; i++)
            {
                String[] lines = templates[i % templates.Length];
                Int32 target = rnd.Next(lines.Length);
                String mutatedLine = i % 5 == 4
                    ? new String(Enumerable.Range(0, rnd.Next(1, 60)).Select(_ => (Char)rnd.Next(32, 127)).ToArray())   // random garbage
                    : FuzzEngine.MutateText(lines[target], rnd);

                if (!IsSafeScriptLine(mutatedLine, root))
                    mutatedLine = lines[target];

                // realistic state: the lines before the mutated one run unchanged (without the file-writing SAVE lines, to stay fast)
                var script = lines.Take(target).Where(l => !l.StartsWith("SAVE", StringComparison.OrdinalIgnoreCase)).Append(mutatedLine).ToList();
                if (rnd.Next(2) == 0 && target + 1 < lines.Length)
                    script.Add(lines[target + 1]);   // and one following line, to see the effect of a corrupted state
                File.WriteAllText(scriptPath, String.Join("\r\n", script));

                String label = $"FAS seed=7001 iteration={i} script={names[i % names.Length]} line {target}: {mutatedLine}";
                Boolean confirm = i % 2 == 0;
                var project = new Project();
                var ui = new SilentInteraction(confirm);
                var svc = new ProjectService(project, ui, appDirectory: _dir.Path);
                svc.NewProject();
                var runner = new ScriptRunner(svc) { ChangeCurrentDirectory = false, UnexpectedException = e => { lock (unexpected) unexpected.Add($"{label}: {e.GetType().Name}: {e.Message}"); } };
                Exception? ex = FuzzEngine.RunBounded(label, () => runner.Run(scriptPath));
                FuzzEngine.AssertAllowed(ex, label);
            }

            // The runner converts any exception into "Unable to execute script", but a NullReference, DivideByZero or
            // InvalidCast reaching it would be a defect in a command implementation.
            Assert.DoesNotContain(unexpected, u => u.Contains("NullReferenceException") || u.Contains("DivideByZeroException") || u.Contains("InvalidCastException"));
        }

        /// <summary>A mutated script line must not write outside the temp directory nor start another script.</summary>
        private static Boolean IsSafeScriptLine(String line, String root)
        {
            if (line.Contains("\\\\") || line.Contains("//"))
                return false;
            String[] fields = line.Split(',');
            String keyword = fields[0].Trim().ToUpperInvariant();
            if (keyword == "RUN_SCRIPT")
                return false;
            if (keyword == "SAVE")
            {
                if (fields.Length < 2)
                    return true;
                try
                { return Path.GetFullPath(fields[1].Trim()).StartsWith(root, StringComparison.OrdinalIgnoreCase); }
                catch (Exception)
                { return false; }
            }
            return true;
        }
    }
}
