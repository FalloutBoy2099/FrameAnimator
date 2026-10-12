namespace FrameAnimator.App.ViewModels
{
    using System;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using FrameAnimator.App.Localization;

    /// <summary>View model of the small dialog that asks for a line of text (for example the name of a new folder).</summary>
    public sealed partial class TextPromptDialogViewModel : DialogViewModelBase
    {
        public TextPromptDialogViewModel(ILocalizer localizer, String title, String prompt, String initial)
        {
            Title = localizer.T(title);
            Prompt = localizer.T(prompt);
            Text = initial;
        }

        public String Title { get; }

        public String Prompt { get; }

        [ObservableProperty]
        public partial String Text { get; set; }

        /// <summary>The trimmed text, set when the dialog was accepted.</summary>
        public String? Result { get; private set; }

        /// <summary>Accepts the trimmed text; an empty text does nothing.</summary>
        [RelayCommand]
        private void Ok()
        {
            String text = Text.Trim();
            if (text.Length == 0)
                return;
            Result = text;
            RequestClose(true);
        }
    }
}
