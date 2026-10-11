namespace FrameAnimator.App
{
    using System;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.Core.Cli;

    /// <summary>
    /// Point d'entrée unique du programme : une commande en ligne (<c>convert</c>, <c>script</c>, <c>automate</c>, <c>/?</c>…) lance le
    /// mode console sans créer de fenêtre ; sinon (aucun argument, ou un fichier à ouvrir) l'interface graphique démarre.
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
