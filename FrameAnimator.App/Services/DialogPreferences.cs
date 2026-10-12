namespace FrameAnimator.App.Services
{
    using System;

    /// <summary>
    /// The user's choice between the application's own dialogs (themed, with favorites) and the "legacy" Windows dialogs
    /// (file open / save, message boxes).
    /// </summary>
    public interface IDialogPreferences
    {
        /// <summary><c>true</c>: Windows dialogs (not themed); <c>false</c> (default): the application's dialogs.</summary>
        Boolean UseSystemDialogs { get; set; }
    }

    /// <summary><see cref="IDialogPreferences"/> stored in an <see cref="ISettingsStore"/>.</summary>
    public sealed class DialogPreferences(ISettingsStore settings) : IDialogPreferences
    {
        private const String Key = "systemDialogs";

        private Boolean? _useSystemDialogs;

        public Boolean UseSystemDialogs
        {
            get => _useSystemDialogs ??= settings.Get(Key) == "1";
            set
            {
                _useSystemDialogs = value;
                settings.Set(Key, value ? "1" : "0");
            }
        }

        /// <summary>Used by the development switches: changes the choice without storing it.</summary>
        public void Preview(Boolean useSystemDialogs) => _useSystemDialogs = useSystemDialogs;
    }
}
