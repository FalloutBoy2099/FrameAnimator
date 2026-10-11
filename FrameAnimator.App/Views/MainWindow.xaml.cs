namespace FrameAnimator.App.Views
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
	using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using Infrastructure;
    using ViewModels;

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            TitleBarTheme.Attach(this);
            WindowMemory.Attach(this);
            Loaded += (_, _) => OnStateChanged(null, EventArgs.Empty);
            DataContextChanged += (_, e) =>
            {
                if (e.OldValue is MainViewModel old)
                    old.PropertyChanged -= OnViewModelPropertyChanged;
                if (e.NewValue is MainViewModel vm)
                    vm.PropertyChanged += OnViewModelPropertyChanged;
            };
        }

        private Boolean _syncing;

        private void OnViewModelPropertyChanged(Object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MainViewModel.SelectedFrameIndex) || Vm is not { } vm)
                return;
            // the current frame (the VCL's ItemIndex) is the one that has focus, not necessarily the first one selected
            Int32 wanted = vm.SelectedFrameIndex;
            if (FrameList.SelectedIndex == wanted || (wanted >= 0 && wanted < FrameList.Items.Count && FrameList.SelectedItems.Contains(FrameList.Items[wanted])))
                return;
            _syncing = true;
            try
            {
                FrameList.SelectedIndex = vm.SelectedFrameIndex;
            }
            finally
            {
                _syncing = false;
            }
        }

        private MainViewModel? Vm => DataContext as MainViewModel;

        private void OnFrameSelectionChanged(Object sender, SelectionChangedEventArgs e)
        {
            if (_syncing || Vm is not { } vm)
                return;
            var indices = new List<Int32>();
            foreach (var item in FrameList.SelectedItems)
            {
                var i = FrameList.Items.IndexOf(item);
                if (i >= 0)
                    indices.Add(i);
            }
            indices.Sort();
            Int32 current = FrameList.SelectedIndex;
            if (Keyboard.FocusedElement is ListBoxItem focused)
            {
                Int32 focusedIndex = FrameList.ItemContainerGenerator.IndexFromContainer(focused);
                if (indices.Contains(focusedIndex))
                    current = focusedIndex;
            }
            vm.SetSelectedIndices(indices);
            vm.SelectedFrameIndex = current;
        }

        private void OnSampleClick(Object sender, MouseButtonEventArgs e)
        {
            if (Vm is { } vm && vm.PickSampleColorCommand.CanExecute(null))
                vm.PickSampleColorCommand.Execute(null);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // the window cannot be closed during automation
            if (Vm is { IsBusy: true })
                e.Cancel = true;
            base.OnClosing(e);
        }

        // ------------------------------------------------------------------ title bar

        private void OnStateChanged(Object? sender, EventArgs e)
        {
            // a maximized window with a custom frame overflows the screen by the width of its border
            Boolean maximized = WindowState == WindowState.Maximized;
            Frame.Margin = maximized
                ? new Thickness(SystemParameters.WindowResizeBorderThickness.Left + SystemParameters.FixedFrameVerticalBorderWidth)
                : new Thickness(0);
            MaximizeButton.Content = maximized ? "" : "";
        }

        private void OnMinimizeClick(Object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

        private void OnMaximizeClick(Object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        private void OnCloseClick(Object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

        /// <summary>Opens the context menu of a title-bar button under it (its entries come from the view model).</summary>
        private void OnMenuButtonClick(Object sender, RoutedEventArgs e)
        {
            if (sender is Button { ContextMenu: { } menu } button)
            {
                menu.PlacementTarget = button;
                menu.IsOpen = true;
            }
        }

        /// <summary>Development switch <c>--snapshot</c>: saves the window to a PNG, then exits.</summary>
        public void SaveSnapshot(String path)
        {
            DevSnapshot.Save(this, path);
            Application.Current.Shutdown();
        }
    }
}