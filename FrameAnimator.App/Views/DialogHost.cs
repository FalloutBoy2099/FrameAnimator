namespace FrameAnimator.App.Views
{
    using System;
    using System.Linq;
    using System.Windows;
    using FrameAnimator.App.ViewModels;

    /// <summary>
    /// Shows modal windows bound to their view model: the window never closes itself, the view model asks for it
    /// (<see cref="DialogViewModelBase.RequestClose"/>) and the host sets the dialog result.
    /// </summary>
    internal static class DialogHost
    {
        /// <summary>The window the user is working in (the active one), used as the owner of message boxes.</summary>
        public static Window? ActiveWindow
            => Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);

        /// <summary>Shows <paramref name="window"/> modally with <paramref name="viewModel"/> as data context; true when accepted.</summary>
        public static Boolean ShowModal(Window window, DialogViewModelBase viewModel, Window? owner)
        {
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