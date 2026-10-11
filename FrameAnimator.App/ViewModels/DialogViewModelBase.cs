namespace FrameAnimator.App.ViewModels
{
    using System;
    using CommunityToolkit.Mvvm.ComponentModel;

    /// <summary>
    /// Base of the view models of modal dialogs. A view model asks for its dialog to close with <see cref="RequestClose"/>;
    /// the dialog service (which created the window) closes it and returns the answer to the caller.
    /// </summary>
    public abstract class DialogViewModelBase : ObservableObject
    {
        /// <summary>Raised when the dialog must close; the argument is <c>true</c> when the user accepted.</summary>
        public event EventHandler<Boolean>? CloseRequested;

        protected void RequestClose(Boolean accepted) => CloseRequested?.Invoke(this, accepted);
    }
}
