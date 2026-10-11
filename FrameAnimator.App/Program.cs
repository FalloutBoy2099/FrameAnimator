namespace FrameAnimator.App
{
    using System;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.Core.Cli;

    /// <summary>
    /// Single entry point of the program: a command line (<c>convert</c>, <c>script</c>, <c>automate</c>, <c>/?</c>…) starts
    /// console mode without creating a window; otherwise (no arguments, or a file to open) the graphical interface starts.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        public static Int32 Main(String[] args)
        {
            if (CliApp.IsConsoleInvocation(args))
                return ConsoleHost.Run(args);

            App app = new App();
            app.InitializeComponent();
            return app.Run();
        }
    }
}
