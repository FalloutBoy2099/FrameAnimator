namespace FrameAnimator.App
{
    using System;
    using System.Windows;
    using System.Windows.Threading;
    using CommunityToolkit.Mvvm.DependencyInjection;
    using FrameAnimator.App.Services;
    using Infrastructure;
    using Localization;
    using Microsoft.Extensions.DependencyInjection;
    using ViewModels;
    using Views;

    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (_, args) =>
            {
                if (Environment.GetEnvironmentVariable("FRAMEANIMATOR_LOG") is { Length: > 0 } log)
                    System.IO.File.AppendAllText(log, args.Exception + Environment.NewLine);
                Ioc.Default.GetRequiredService<MessageService>().ShowError(args.Exception.Message);
                args.Handled = true;
            };

            ThemeService theme = Ioc.Default.GetRequiredService<ThemeService>();
            Localizer localizer = Ioc.Default.GetRequiredService<Localizer>();
            theme.Initialize();
            Ioc.Default.GetRequiredService<WindowService>().Initialize();
            localizer.Initialize();

            String? snapshot = null;
            String? open = null;
            String? dialog = null;
            for (Int32 i = 0; i < e.Args.Length; i++)
            {
                // development switches: they do not save the preferences
                if (e.Args[i] == "--snapshot" && i + 1 < e.Args.Length)
                    snapshot = e.Args[++i];
                else if (e.Args[i] == "--dialog" && i + 1 < e.Args.Length)
                    dialog = e.Args[++i];
                else if (e.Args[i] == "--theme" && i + 1 < e.Args.Length)
                    theme.Preview(ThemeService.Parse(e.Args[++i]));
                else if (e.Args[i] == "--system-dialogs")
                    Ioc.Default.GetRequiredService<DialogPreferences>().Preview(true);
                else if (e.Args[i] == "--lang" && i + 1 < e.Args.Length)
                    localizer.Apply(e.Args[++i]);
                else if (open is null)
                    open = e.Args[i];
            }

            DialogHost host = Ioc.Default.GetRequiredService<DialogHost>();
            MainWindow window = new();
            MainViewModel vm = Ioc.Default.GetRequiredService<MainViewModel>();
            host.Owner = window;
            Ioc.Default.GetRequiredService<IWindowService>().Attach(window);
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

        private static Window? CreateDialog(String name, MainViewModel vm)
        {
            IMessageService messages = Ioc.Default.GetRequiredService<IMessageService>();
            ILocalizer localizer = Ioc.Default.GetRequiredService<ILocalizer>();
            switch (name)
            {
                case "options":
                    return Bind(new OptionsDialog(), new OptionsDialogViewModel(vm.Project.Settings, 3, messages, localizer));
                case "automate":
                    return Bind(new AutomateDialog(), new AutomateDialogViewModel("Default.act", Ioc.Default.GetRequiredService<DialogService>()));
                case "preview":
                    return Bind(new AnimationPreviewWindow(), Ioc.Default.GetRequiredService<AnimationPreviewViewModel>());
                case "about":
                    return Bind(new AboutDialog(), new AboutDialogViewModel());
                case "open":
                    return Bind(new FileDialogWindow(), Ioc.Default.GetRequiredService<FileDialogs>().CreateViewModel(FileDialogOptions.ForOpen("Open Project",
                        Ioc.Default.GetRequiredService<DialogService>().TranslateFilter("All (*.FPR,*.FRM)|*.FPR;*.FRM|Project (*.FPR)|*.FPR"), multiselect: true)));
                case "color":
                    return Bind(new ColorDialogWindow(), new ColorDialogViewModel(0x80C040));
                case "offset":
                    return Bind(new OffsetDialog(), new OffsetDialogViewModel(new OffsetDialogRequest
                    {
                        Title = "Frame Offset",
                        X = 3,
                        Y = -2,
                        ShowInherit = true,
                        ShowWizard = true,
                        WizardEnabled = true,
                        Wizard = x => x,
                    }, messages, localizer));
                default:
                    return null;
            }
        }

        private static Window Bind(Window window, Object viewModel)
        {
            Ioc.Default.GetRequiredService<IWindowService>().Attach(window);
            window.DataContext = viewModel;
            return window;
        }
    }
}