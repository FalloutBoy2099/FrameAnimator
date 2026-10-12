namespace FrameAnimator.App.Views
{
    using System;
    using System.Linq;
    using System.Windows;
    using FrameAnimator.App.Services;
    using FrameAnimator.App.ViewModels;

    /// <summary>
    /// Shows modal windows bound to their view model: the window never closes itself, the view model asks for it
    /// (<see cref="DialogViewModelBase.RequestClose"/>) and the host sets the dialog result. Every window it shows is registered with
    /// the <see cref="IWindowService"/> first (themed title bar, remembered position and size).
    /// </summary>
    public sealed class DialogHost(IWindowService windows)
    {
        /// <summary>The main window: the owner of dialogs and the place of messages when no window is active.</summary>
        public Window? Owner { get; set; }

        /// <summary>The window the user is working in (the active one), used as the owner of message boxes.</summary>
        public Window? ActiveWindow
            => Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);

        /// <summary>The active window if any, else the main one: where a message or a file dialog appears.</summary>
        public Window? Target => ActiveWindow ?? Owner;

        /// <summary>Shows <paramref name="window"/> without blocking: it is registered like the modal ones and owned by <paramref name="owner"/>.</summary>
        public void Show(Window window, Object viewModel, Window? owner)
        {
            windows.Attach(window);
            window.DataContext = viewModel;
            if (owner is { IsLoaded: true })
                window.Owner = owner;
            window.Show();
        }

        /// <summary>Shows <paramref name="window"/> modally with <paramref name="viewModel"/> as data context; true when accepted.</summary>
        public Boolean ShowModal(Window window, DialogViewModelBase viewModel, Window? owner)
        {
            windows.Attach(window);
            window.DataContext = viewModel;
            if (owner is { IsLoaded: true })
                window.Owner = owner;
            else
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            viewModel.CloseRequested += (_, accepted) => window.DialogResult = accepted;
            return window.ShowDialog() == true;
        }
    }
}
