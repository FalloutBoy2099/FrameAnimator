namespace FrameAnimator.App.Infrastructure
{
    using System;
    using System.Windows.Threading;
    using FrameAnimator.Core.Services;

    /// <summary>
    /// <see cref="IUserInteraction"/> de l'application : messages, questions et boîtes de fichiers, exécutables depuis
    /// n'importe quel thread (les appels sont renvoyés sur le thread d'interface).
    /// </summary>
    public sealed class WpfInteraction(Dispatcher dispatcher, IDialogService dialogs) : IUserInteraction
    {
        private volatile Boolean _cancelRequested;

        /// <summary>Notifié (sur le thread d'interface) de l'avancement : <c>done</c> sur <c>total</c>, <c>0,0</c> = fin.</summary>
        public event Action<Int32, Int32>? ProgressChanged;

        public Boolean CancelRequested
        {
            get => _cancelRequested;
            set => _cancelRequested = value;
        }

        private T OnUi<T>(Func<T> action)
        {
            if (dispatcher.CheckAccess())
                return action();
            return dispatcher.Invoke(action);
        }

        public void ShowError(String message) => OnUi(() =>
        {
            dialogs.ShowWarning(message);
            return 0;
        });

        public Boolean Confirm(String message) => OnUi(() => dialogs.AskYesNo(message));

        public String? PromptSaveFramesAs(String suggestedName) => OnUi(() =>
            dialogs.SaveFile("Save Frames As", "BMP (*.BMP)|*.BMP|PNG (*.PNG)|*.PNG|CompuServe GIF (*.GIF)|*.GIF", 1,
                suggestedName, "BMP"));

        public String? PromptPalette() => OnUi(() =>
        {
            var files = dialogs.OpenFiles("Load Palette",
                "All (*.ACT, *.PAL)|*.ACT;*.PAL|Palette (*.ACT)|*.ACT|Fallout Palette (*.PAL)|*.PAL", false);
            return files.Length > 0 ? files[0] : null;
        });

        public void Progress(Int32 done, Int32 total)
        {
            var handler = ProgressChanged;
            if (handler is null)
                return;
            if (dispatcher.CheckAccess())
                handler(done, total);
            else
                dispatcher.BeginInvoke(() => handler(done, total), DispatcherPriority.Background);
        }
    }
}