namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.Reflection;
    using CommunityToolkit.Mvvm.Input;

    /// <summary>View model of the "About" dialog.</summary>
    public sealed partial class AboutDialogViewModel : DialogViewModelBase
    {
        public String ProductName => "Frame Animator 2.8";

        public String Author => "by Kain";

        /// <summary>Assembly version: 2026.month.day.build.</summary>
        public String Version { get; } = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "";

        [RelayCommand]
        private void Close() => RequestClose(true);
    }
}
