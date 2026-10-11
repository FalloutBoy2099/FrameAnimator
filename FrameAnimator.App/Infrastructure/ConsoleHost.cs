namespace FrameAnimator.App.Infrastructure
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Windows;
    using FrameAnimator.Core.Cli;

    /// <summary>
    /// Mode console de l'exécutable graphique. Le programme est une application fenêtrée (sous-système Windows) : il se rattache à la
    /// console du processus parent (<c>AttachConsole</c>) pour y écrire, ou utilise la sortie redirigée (<c>&gt; fichier</c>, tube).
    /// Sans console ni redirection (lancement depuis l'explorateur), le texte est affiché dans une boîte de message.
    /// </summary>
    internal static partial class ConsoleHost
    {
        private const UInt32 AttachParentProcess = 0xFFFFFFFF;
        private const Int32 StdOutput = -11;
        private const Int32 StdError = -12;
        private const UInt32 GenericReadWrite = 0xC0000000;
        private const UInt32 ShareReadWrite = 3;
        private const UInt32 OpenExisting = 3;
        private const UInt32 FileTypeChar = 2;
        private const UInt32 Utf8CodePage = 65001;

        [System.Runtime.InteropServices.LibraryImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static partial Boolean AttachConsole(UInt32 processId);

        [System.Runtime.InteropServices.LibraryImport("kernel32.dll")]
        private static partial IntPtr GetStdHandle(Int32 handle);

        [System.Runtime.InteropServices.LibraryImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static partial Boolean SetStdHandle(Int32 handle, IntPtr value);

        [System.Runtime.InteropServices.LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true,
            StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf16)]
        private static partial IntPtr CreateFile(String name, UInt32 access, UInt32 share, IntPtr security, UInt32 disposition, UInt32 flags, IntPtr template);

        [System.Runtime.InteropServices.LibraryImport("kernel32.dll")]
        private static partial UInt32 GetFileType(IntPtr handle);

        [System.Runtime.InteropServices.LibraryImport("kernel32.dll")]
        private static partial UInt32 GetConsoleOutputCP();

        [System.Runtime.InteropServices.LibraryImport("kernel32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static partial Boolean SetConsoleOutputCP(UInt32 codePage);

        private static Boolean IsValid(IntPtr handle) => handle != IntPtr.Zero && handle != new IntPtr(-1);

        /// <summary>Exécute la ligne de commande et retourne le code de sortie du processus.</summary>
        public static Int32 Run(String[] args)
        {
            (Boolean outputValid, Boolean errorValid) = ConnectToConsole();
            Boolean haveOutput = outputValid || errorValid;

            // la console peut être en page de code 850 / 1252 : on écrit en UTF-8 et on remet la page de code d'origine ensuite
            UInt32 codePage = 0;
            if (outputValid && GetFileType(GetStdHandle(StdOutput)) == FileTypeChar
                || errorValid && GetFileType(GetStdHandle(StdError)) == FileTypeChar)
            {
                codePage = GetConsoleOutputCP();
                SetConsoleOutputCP(Utf8CodePage);
            }

            // chaque flux utilise sa sortie standard, à défaut l'autre, à défaut une mémoire (affichée si besoin dans une boîte de message)
            TextWriter? standardOutput = outputValid ? new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true } : null;
            TextWriter? standardError = errorValid ? new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true } : null;
            StringWriter memory = new StringWriter();
            TextWriter output = standardOutput ?? standardError ?? memory;
            TextWriter error = standardError ?? standardOutput ?? memory;

            Int32 code;
            try
            {
                code = new CliApp(output, error, GetLanguage(), GetProgramName()).Execute(args);
            }
            finally
            {
                output.Flush();
                error.Flush();
                if (codePage != 0)
                    SetConsoleOutputCP(codePage);
            }

            // ni console ni redirection : l'aide et les erreurs de ligne de commande s'affichent dans une boîte de message
            // (jamais le résultat d'un traitement : un lot lancé sans console, par exemple par le planificateur, ne doit pas se bloquer)
            if (!haveOutput && (code == CliApp.UsageError || CliApp.IsHelpRequest(args)))
            {
                String text = memory.ToString().Trim();
                if (text.Length > 0)
                {
                    MessageBox.Show(text, "Frame Animator", MessageBoxButton.OK,
                        code == CliApp.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
                }
            }
            return code;
        }

        /// <summary>
        /// Rend la sortie standard utilisable. Une sortie déjà redirigée est conservée ; sinon on se rattache à la console du parent
        /// et on relie les sorties standard à <c>CONOUT$</c>. Indique pour chaque flux s'il est utilisable.
        /// </summary>
        private static (Boolean Output, Boolean Error) ConnectToConsole()
        {
            Boolean outputValid = IsValid(GetStdHandle(StdOutput));
            Boolean errorValid = IsValid(GetStdHandle(StdError));
            if (outputValid && errorValid)
                return (true, true);

            if (AttachConsole(AttachParentProcess))
            {
                IntPtr console = CreateFile("CONOUT$", GenericReadWrite, ShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
                if (IsValid(console))
                {
                    if (!outputValid)
                        SetStdHandle(StdOutput, console);
                    if (!errorValid)
                        SetStdHandle(StdError, console);
                }
            }
            return (IsValid(GetStdHandle(StdOutput)), IsValid(GetStdHandle(StdError)));
        }

        /// <summary>Langue de l'aide : celle choisie dans l'application, sinon celle de Windows.</summary>
        private static String GetLanguage()
            => UserSettings.Get("language") ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        private static String GetProgramName()
            => Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "FrameAnimator";
    }
}
