namespace FrameAnimator.App.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Windows.Input;

    /// <summary>Base class for observable objects.</summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] String? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected Boolean SetField<T>(ref T field, T value, [CallerMemberName] String? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    /// <summary>Simple command; <see cref="RaiseCanExecuteChanged"/> forces re-evaluation.</summary>
    public sealed class RelayCommand(Action execute, Func<Boolean>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public Boolean CanExecute(Object? parameter) => canExecute?.Invoke() ?? true;

        public void Execute(Object? parameter) => execute();

        public static void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }
}