namespace FrameAnimator.App
{
    using System;
    using System.Windows;
    using System.Windows.Threading;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Localization;
    using FrameAnimator.App.ViewModels;
    using FrameAnimator.App.Views;

    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (_, args) =>
            {
                if (Environment.GetEnvironmentVariable("FRAMEANIMATOR_LOG") is { Length: > 0 } log)
                    System.IO.File.AppendAllText(log, args.Exception + Environment.NewLine);
                MessageDialog.Show(MainWindow, args.Exception.Message, "Frame Animator", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            ThemeManager.Initialize();
            TitleBarTheme.ApplyToAllWindows();
            Localizer.Instance.Initialize();

            String? snapshot = null;
            String? open = null;
            String? dialog = null;
            for (Int32 i = 0; i < e.Args.Length; i++)
            {
                // commutateurs de développement : n'enregistrent pas les préférences
                if (e.Args[i] == "--snapshot" && i + 1 < e.Args.Length)
                    snapshot = e.Args[++i];
                else if (e.Args[i] == "--dialog" && i + 1 < e.Args.Length)
                    dialog = e.Args[++i];
                else if (e.Args[i] == "--theme" && i + 1 < e.Args.Length)
                    ThemeManager.Preview(ThemeManager.Parse(e.Args[++i]));
                else if (e.Args[i] == "--system-dialogs")
                    DialogPreferences.Preview(true);
                else if (e.Args[i] == "--lang" && i + 1 < e.Args.Length)
                    Localizer.Instance.Apply(e.Args[++i]);
                else if (open is null)
                    open = e.Args[i];
            }

            DialogService dialogs = new();
            MainWindow window = new();
            MainViewModel vm = new(Dispatcher, dialogs, AppContext.BaseDirectory);
            dialogs.Owner = window;
            window.DataContext = vm;
            MainWindow = window;
            window.Show();

            if (open is not null)
                vm.OpenPath(open);

            if (dialog is not null)
            {
                Window? shown = CreateDialog(dialog, vm);
                if (shown is not null)
                {
                    shown.Owner = window;
                    shown.Show();
                    if (snapshot is not null)
                    {
                        String path = snapshot;
                        shown.Dispatcher.BeginInvoke(() =>
                        {
                            DevSnapshot.Save(shown, path);
                            Shutdown();
                        }, DispatcherPriority.ApplicationIdle);
                    }
                }
            }
            else if (snapshot is not null)
            {
                window.Dispatcher.BeginInvoke(() => window.SaveSnapshot(snapshot), DispatcherPriority.ApplicationIdle);
            }
        }

        private static Window? CreateDialog(String name, MainViewModel vm) => name switch
        {
            "options" => new OptionsDialog(vm.Project.Settings, 3),
            "automate" => new AutomateDialog("Default.act"),
            "about" => new AboutDialog(),
            "open" => FileDialogWindow.CreateForPreview(),
            "color" => new ColorDialogWindow(0x80C040),
            "offset" => new OffsetDialog(new OffsetDialogRequest
            {
                Title = "Frame Offset",
                X = 3,
                Y = -2,
                ShowInherit = true,
                ShowWizard = true,
                WizardEnabled = true,
                Wizard = x => x,
            }),
            _ => null,
        };
    }
}
