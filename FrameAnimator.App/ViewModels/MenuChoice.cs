namespace FrameAnimator.App.ViewModels
{
    using System;
    using CommunityToolkit.Mvvm.ComponentModel;
    using System.Windows.Input;

    /// <summary>One entry of a "choose one" menu (theme, language): its text, whether it is the current choice, and what picking it does.</summary>
    public sealed partial class MenuChoice(String header, ICommand command, Object? parameter) : ObservableObject
    {
        [ObservableProperty]
        public partial String Header { get; set; } = header;

        [ObservableProperty]
        public partial Boolean IsCurrent { get; set; }

        public ICommand Command { get; } = command;

        public Object? Parameter { get; } = parameter;

        /// <summary>The text, so that accessibility tools and logs show the entry by name.</summary>
        public override String ToString() => Header;
    }
}
