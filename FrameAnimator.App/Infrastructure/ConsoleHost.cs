namespace FrameAnimator.App.Infrastructure
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Windows;
    using FrameAnimator.App.Services;
    using FrameAnimator.Core.Cli;

    /// <summary>
    /// Console mode of the graphical executable. The program is a windowed application (Windows subsystem): it attaches to the
    /// parent process's console (<c>AttachConsole</c>) to write to it, or uses the redirected output (<c>&gt; file</c>, pipe).
    /// With neither a console nor a redirection (launched from Explorer), the text is shown in a message box.
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

        /// <summary>Runs the command line and returns the process exit code.</summary>
        public static Int32 Run(String[] args)
        {
            (Boolean outputValid, Boolean errorValid) = ConnectToConsole();
            Boolean haveOutput = outputValid || errorValid;

            // the console may use code page 850 / 1252: we write in UTF-8 and restore the original code page afterwards
            UInt32 codePage = 0;
            if (outputValid && GetFileType(GetStdHandle(StdOutput)) == FileTypeChar
                || errorValid && GetFileType(GetStdHandle(StdError)) == FileTypeChar)
            {
                codePage = GetConsoleOutputCP();
                SetConsoleOutputCP(Utf8CodePage);
            }

            // each stream uses its own standard output, otherwise the other one, otherwise a memory buffer (shown in a message box if needed)
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

            // neither console nor redirection: help and command-line errors are shown in a message box
            // (never the result of a processing run: a batch started without a console, for example by the scheduler, must not block)
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
        /// Makes the standard output usable. An already redirected output is kept; otherwise we attach to the parent's console
        /// and bind the standard outputs to <c>CONOUT$</c>. Reports for each stream whether it is usable.
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

        /// <summary>Help language: the one chosen in the application, otherwise the Windows one.</summary>
        private static String GetLanguage()
            => new FileSettingsStore().Get("language") ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        private static String GetProgramName()
            => Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "FrameAnimator";
    }
}