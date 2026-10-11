namespace FrameAnimator.App.Views
{
    using System;
    using System.IO;
    using System.Windows;
    using Microsoft.Win32;
	using Infrastructure;
	using Localization;

	/// <summary>
	/// Point d'entrée unique des boîtes de fichiers : la boîte de l'application (<see cref="FileDialogWindow"/>) ou, selon
	/// <see cref="DialogPreferences.UseSystemDialogs"/>, la boîte « legacy » de Windows. Les deux retiennent le dernier dossier
	/// utilisé, séparément pour l'ouverture et pour l'enregistrement ; seule la boîte de l'application a des favoris.
	/// </summary>
	public static class FileDialogs
    {
        /// <summary>Boîte d'ouverture ; tableau vide si annulée.</summary>
        public static String[] ShowOpen(Window? owner, String title, String filter, Boolean multiselect)
        {
            if (!DialogPreferences.UseSystemDialogs)
                return FileDialogWindow.ShowOpen(owner, title, filter, multiselect);

            OpenFileDialog dialog = new()
			{
                Title = Localizer.T(title),
                Filter = filter,
                Multiselect = multiselect,
                InitialDirectory = FileDialogMemory.GetLastDirectory(save: false) ?? "",
            };
            if (dialog.ShowDialog(owner) != true)
                return [];
            Remember(save: false, dialog.FileName);
            return dialog.FileNames;
        }

        /// <summary>Boîte d'enregistrement ; <c>null</c> si annulée.</summary>
        public static String? ShowSave(Window? owner, String title, String filter, Int32 filterIndex, String? fileName,
            String defaultExt, Boolean overwritePrompt = true)
        {
            if (!DialogPreferences.UseSystemDialogs)
                return FileDialogWindow.ShowSave(owner, title, filter, filterIndex, fileName, defaultExt, overwritePrompt);

            String initialDirectory = FileDialogMemory.GetLastDirectory(save: true) ?? "";
            String name = fileName ?? "";
            if (Path.GetDirectoryName(name) is { Length: > 0 } directory && Directory.Exists(directory))
            {
                initialDirectory = directory;
                name = Path.GetFileName(name);
            }

            SaveFileDialog dialog = new()
			{
                Title = Localizer.T(title),
                Filter = filter,
                FilterIndex = filterIndex,
                FileName = name,
                InitialDirectory = initialDirectory,
                DefaultExt = defaultExt,
                AddExtension = defaultExt.Length > 0,
                OverwritePrompt = overwritePrompt,
                CheckFileExists = false,
            };
            if (dialog.ShowDialog(owner) != true)
                return null;
            Remember(save: true, dialog.FileName);
            return dialog.FileName;
        }

        private static void Remember(Boolean save, String file)
        {
            if (Path.GetDirectoryName(file) is { Length: > 0 } directory && Directory.Exists(directory))
                FileDialogMemory.SetLastDirectory(save, directory);
        }
    }
}