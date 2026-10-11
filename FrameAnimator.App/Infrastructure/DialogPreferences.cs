namespace FrameAnimator.App.Infrastructure
{
    using System;

    /// <summary>
    /// Choix de l'utilisateur entre les boîtes de dialogue de l'application (au thème, avec favoris) et les boîtes « legacy »
    /// de Windows (ouverture / enregistrement de fichiers, boîtes de message). Mémorisé dans <see cref="UserSettings"/>.
    /// </summary>
    public static class DialogPreferences
    {
        private const String Key = "systemDialogs";

        private static Boolean? _useSystemDialogs;

        /// <summary><c>true</c> : boîtes de Windows (non thémées) ; <c>false</c> (défaut) : boîtes de l'application.</summary>
        public static Boolean UseSystemDialogs
        {
            get => _useSystemDialogs ??= UserSettings.Get(Key) == "1";
            set
            {
                _useSystemDialogs = value;
                UserSettings.Set(Key, value ? "1" : "0");
            }
        }

        /// <summary>Utilisé par les commutateurs de développement : change le choix sans le mémoriser.</summary>
        internal static void Preview(Boolean useSystemDialogs) => _useSystemDialogs = useSystemDialogs;
    }
}