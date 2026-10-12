namespace FrameAnimator.Core.Scripting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Formats;
    using Services;

    /// <summary>Destination format of the batch processing ("Destination format" drop-down list).</summary>
    public enum AutomationFormat
    {
        FprProject = 0,
        Frm = 1,
        /// <summary>"FR0‑FR5": the mask ends with <c>.FR0</c>.</summary>
        FrN = 2,
    }

    /// <summary>Parameters of the "Automate" dialog (specification §6.4).</summary>
    public sealed class AutomationOptions
    {
        public List<String> Sources { get; } = [];
        public Boolean UsePalette { get; set; }
        public String PalettePath { get; set; } = "";
        public Boolean UseScript { get; set; }
        public String ScriptPath { get; set; } = "";
        public AutomationFormat Format { get; set; } = AutomationFormat.FprProject;
        /// <summary>Destination mask (default <c>*.FPR</c>). The extension normally follows <see cref="Format"/>.</summary>
        public String DestinationMask { get; set; } = "*.FPR";
        /// <summary>Format of the frame images saved with a project: <c>BMP</c>, <c>PNG</c> or <c>GIF</c>.</summary>
        public String FrameExtension { get; set; } = "BMP";

        /// <summary>Replaces the mask's extension according to the chosen format (0x41227c).</summary>
        public void ApplyFormatToMask()
        {
            String ext = Format switch { AutomationFormat.FprProject => ".FPR", AutomationFormat.Frm => ".FRM", _ => ".FR0" };
            DestinationMask = PathHelpers.ChangeFileExt(DestinationMask, ext);
        }
    }

    /// <summary>Batch processing ("File → Automate", 0x40d35c).</summary>
    public sealed class AutomationRunner(ProjectService service)
    {
        public ProjectService Service { get; } = service;
        private IUserInteraction UI => Service.UI;

        /// <summary>Returns <c>true</c> if all sources were processed without a stop request.</summary>
        public Boolean Run(AutomationOptions options)
        {
            Boolean completed = true;
            foreach (String src in options.Sources)
            {
                if (!Service.NewProject() && StopRequested())
                { completed = false; break; }

                String ext = PathHelpers.ExtractFileExt(src).ToUpperInvariant();
                if (ext == ".FPR")
                {
                    if (!Service.LoadProject(src) && StopRequested())
                    { completed = false; break; }
                }
                else if (ext.Length >= 3 && ext[..3] == ".FR")
                {
                    Boolean paletteFailed = false;
                    if (options.UsePalette)
                        paletteFailed = !Service.LoadPalette(options.PalettePath);
                    else
                    {
                        String sibling = PathHelpers.ChangeFileExt(src, ".PAL");
                        if (File.Exists(sibling))
                            paletteFailed = !Service.LoadPalette(sibling);
                    }
                    if (paletteFailed && StopRequested())
                    { completed = false; break; }
                    if (!Service.LoadFrm(src) && StopRequested())
                    { completed = false; break; }
                }

                if (UI.CancelRequested)
                { completed = false; break; }

                if (options.UseScript)
                {
                    if (!new ScriptRunner(Service).Run(options.ScriptPath) && StopRequested())
                    { completed = false; break; }
                    if (UI.CancelRequested)
                    { completed = false; break; }
                }

                String dest = options.DestinationMask;
                if (dest.Length > 0)
                {
                    String name = PathHelpers.ExtractFileName(dest);
                    String path = PathHelpers.ExtractFilePath(dest);
                    if (path.Length == 0)
                        path = PathHelpers.ExtractFilePath(src);
                    if (NameMask.HasWildcards(name))
                        name = NameMask.Apply(PathHelpers.ExtractFileName(src), name);
                    dest = path + name;

                    String destExt = PathHelpers.ExtractFileExt(dest).ToUpperInvariant();
                    if (destExt == ".FPR")
                    {
                        String baseName = (dest.Length >= 4 ? dest[..^4] : dest) + "." + options.FrameExtension;
                        if (!Service.SaveProject(dest, baseName) && StopRequested())
                        { completed = false; break; }
                    }
                    else if (destExt is ".FRM" or ".FR0")
                    {
                        if (!Service.SaveFrm(dest) && StopRequested())
                        { completed = false; break; }
                    }
                }

                if (UI.CancelRequested)
                { completed = false; break; }
            }
            Service.NewProject();
            return completed;
        }

        private Boolean StopRequested() => UI.Confirm(CoreMessages.StopAutomatedRoutine);
    }
}