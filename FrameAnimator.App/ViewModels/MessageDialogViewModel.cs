namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using CommunityToolkit.Mvvm.Input;
    using FrameAnimator.App.Localization;

    /// <summary>A button of a message dialog.</summary>
    public sealed record MessageButton(String Caption, MessageBoxResult Result, Boolean IsDefault, Boolean IsCancel);

    /// <summary>View model of the message box using the application's theme.</summary>
    public sealed partial class MessageDialogViewModel : DialogViewModelBase
    {
        private readonly ILocalizer _localizer;

        public MessageDialogViewModel(ILocalizer localizer, String message, String caption, MessageBoxButton buttons, MessageBoxImage image)
        {
            _localizer = localizer;
            Message = message;
            Caption = caption;
            Glyph = GetGlyph(image);
            Buttons = buttons switch
            {
                MessageBoxButton.OK => [CreateButton(MessageBoxResult.OK, isDefault: true, isCancel: true)],
                MessageBoxButton.YesNo =>
                [
                    CreateButton(MessageBoxResult.Yes, isDefault: true, isCancel: false),
                    CreateButton(MessageBoxResult.No, isDefault: false, isCancel: true),
                ],
                MessageBoxButton.YesNoCancel =>
                [
                    CreateButton(MessageBoxResult.Yes, isDefault: true, isCancel: false),
                    CreateButton(MessageBoxResult.No, isDefault: false, isCancel: false),
                    CreateButton(MessageBoxResult.Cancel, isDefault: false, isCancel: true),
                ],
                _ =>
                [
                    CreateButton(MessageBoxResult.OK, isDefault: true, isCancel: true),
                    CreateButton(MessageBoxResult.Cancel, isDefault: false, isCancel: true),
                ],
            };
        }

        public String Message { get; }

        public String Caption { get; }

        /// <summary>Symbol font glyph of the icon; empty when there is none.</summary>
        public String Glyph { get; }

        public Boolean HasGlyph => Glyph.Length > 0;

        public IReadOnlyList<MessageButton> Buttons { get; }

        /// <summary>The clicked button; <see cref="MessageBoxResult.None"/> if the window was closed otherwise.</summary>
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private static String GetGlyph(MessageBoxImage image) => image switch
        {
            MessageBoxImage.Information => "",
            MessageBoxImage.Warning => "",
            MessageBoxImage.Error => "",
            MessageBoxImage.Question => "",
            _ => String.Empty,
        };

        private MessageButton CreateButton(MessageBoxResult result, Boolean isDefault, Boolean isCancel)
            => new(GetCaption(result), result, isDefault, isCancel);

        private String GetCaption(MessageBoxResult result) => _localizer.T(result switch
        {
            MessageBoxResult.Yes => "Yes",
            MessageBoxResult.No => "No",
            MessageBoxResult.Cancel => "Cancel",
            _ => "OK",
        });

        /// <summary>Stores the clicked button and closes the dialog.</summary>
        [RelayCommand]
        private void Choose(MessageBoxResult result)
        {
            Result = result;
            RequestClose(true);
        }
    }
}
