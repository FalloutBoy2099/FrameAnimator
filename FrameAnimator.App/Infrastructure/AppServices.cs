namespace FrameAnimator.App.Infrastructure
{
    using System;
    using System.Windows.Threading;
    using CommunityToolkit.Mvvm.DependencyInjection;
    using FrameAnimator.App.Localization;
    using FrameAnimator.App.Services;
    using FrameAnimator.App.ViewModels;
    using FrameAnimator.App.Views;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Composition root of the graphical application: the services the windows and view models depend on are declared here, once.
    /// Everything is a singleton; the few places that cannot receive their dependencies by constructor (the XAML markup extension,
    /// the drawing control) reach them through <see cref="Ioc.Default"/>.
    /// </summary>
    internal static class AppServices
    {
        /// <summary>Builds the services and publishes them as <see cref="Ioc.Default"/>.</summary>
        /// <param name="settingsDirectory">Folder of the preferences file; the default one when <c>null</c>.</param>
        public static IServiceProvider Configure(String? settingsDirectory = null)
        {
            ServiceCollection services = new();
            Register(services, settingsDirectory, Dispatcher.CurrentDispatcher, AppContext.BaseDirectory);
            ServiceProvider provider = services.BuildServiceProvider();
            Ioc.Default.ConfigureServices(provider);
            return provider;
        }

        /// <summary>Declares the services (also used by the tests, which replace some of them).</summary>
        /// <param name="services">Receives the declarations.</param>
        /// <param name="settingsDirectory">Folder of the preferences file; the default one when <c>null</c>.</param>
        /// <param name="dispatcher">Thread of the user interface, where the project session raises its notifications.</param>
        /// <param name="appDirectory">Folder of the application (default palette).</param>
        internal static void Register(IServiceCollection services, String? settingsDirectory, Dispatcher dispatcher, String appDirectory)
        {
            services.AddSingleton<ISettingsStore>(_ => new FileSettingsStore(settingsDirectory));

            // the concrete types stay reachable for what is not part of their interface (initialization, development switches)
            services.AddSingleton<ThemeService>();
            services.AddSingleton<IThemeService>(provider => provider.GetRequiredService<ThemeService>());
            services.AddSingleton<Localizer>();
            services.AddSingleton<ILocalizer>(provider => provider.GetRequiredService<Localizer>());
            services.AddSingleton<DialogPreferences>();
            services.AddSingleton<IDialogPreferences>(provider => provider.GetRequiredService<DialogPreferences>());
            services.AddSingleton<WindowService>();
            services.AddSingleton<IWindowService>(provider => provider.GetRequiredService<WindowService>());
            services.AddSingleton<IFileDialogMemory, FileDialogMemory>();
            services.AddSingleton<IProjectPreferences, ProjectPreferences>();

            services.AddSingleton<DialogHost>();
            services.AddSingleton<MessageService>();
            services.AddSingleton<IMessageService>(provider => provider.GetRequiredService<MessageService>());
            services.AddSingleton<FileDialogs>();
            services.AddSingleton<DialogService>();
            services.AddSingleton<IDialogService>(provider => provider.GetRequiredService<DialogService>());

            // the main window: one session shared by the view models of its parts
            services.AddSingleton(provider => new ProjectSession(dispatcher, provider.GetRequiredService<IDialogService>(),
                provider.GetRequiredService<ILocalizer>(), appDirectory));
            services.AddSingleton<FrameListViewModel>();
            services.AddSingleton<CanvasViewModel>();
            services.AddSingleton<StatusBarViewModel>();
            services.AddSingleton<AppearanceViewModel>();
            services.AddSingleton<ITicker, DispatcherTicker>();
            services.AddSingleton<AnimationPreviewViewModel>();
            services.AddSingleton<MainViewModel>();
        }
    }
}
