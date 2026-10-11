namespace FrameAnimator.App.Infrastructure
{
    using System;

    /// <summary>
    /// The user's choice between the application's own dialogs (themed, with favorites) and the "legacy" Windows dialogs
    /// (file open / save, message boxes). Stored in <see cref="UserSettings"/>.
    /// </summary>
    public static class DialogPreferences
    {
        private const String Key = "systemDialogs";

        private static Boolean? _useSystemDialogs;

        /// <summary><c>true</c>: Windows dialogs (not themed); <c>false</c> (default): the application's dialogs.</summary>
        public static Boolean UseSystemDialogs
        {
            get => _useSystemDialogs ??= UserSettings.Get(Key) == "1";
            set
            {
                _useSystemDialogs = value;
                UserSettings.Set(Key, value ? "1" : "0");
            }
        }

        /// <summary>Used by the development switches: changes the choice without storing it.</summary>
        internal static void Preview(Boolean useSystemDialogs) => _useSystemDialogs = useSystemDialogs;
    }
}