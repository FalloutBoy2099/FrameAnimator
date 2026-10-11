namespace FrameAnimator.App.Views
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
	using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using Infrastructure;
    using Localization;
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
            // la frame courante (ItemIndex de la VCL) est celle qui a le focus, pas forcément la première sélectionnée
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
            // pendant l'automatisation, la fenêtre ne peut pas se fermer
            if (Vm is { IsBusy: true })
                e.Cancel = true;
            base.OnClosing(e);
        }

        // ------------------------------------------------------------------ barre de titre

        private void OnStateChanged(Object? sender, EventArgs e)
        {
            // une fenêtre agrandie à cadre personnalisé déborde de l'écran de la largeur de sa bordure
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

        private void OnThemeButtonClick(Object sender, RoutedEventArgs e)
        {
            ContextMenu menu = CreateMenu(ThemeButton);
            foreach (ThemePreference preference in Enum.GetValues<ThemePreference>())
            {
                MenuItem item = CreateMenuItem(Localizer.T(preference.ToString()), preference == ThemeManager.Preference);
                item.Click += (_, _) => ThemeManager.SetPreference(preference);
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        }

        private void OnLanguageButtonClick(Object sender, RoutedEventArgs e)
        {
            ContextMenu menu = CreateMenu(LanguageButton);
            foreach (LanguageInfo language in Localizer.Languages)
            {
                MenuItem item = CreateMenuItem(language.NativeName, language.Code == Localizer.Instance.Language);
                item.Click += (_, _) => Localizer.Instance.SetLanguage(language.Code);
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        }

        private ContextMenu CreateMenu(Button target)
        {
            // le menu hérite de la police de sa cible (police de symboles du bouton) : lui donner celle de la fenêtre
            return new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom, FontFamily = FontFamily, FontSize = FontSize };
        }

        private MenuItem CreateMenuItem(String header, Boolean isCurrent)
        {
            // une coche (glyphe de la police de symboles) indique le choix en cours
            return new MenuItem
            {
                Header = header,
                Icon = isCurrent ? new TextBlock { Text = "", FontFamily = (System.Windows.Media.FontFamily)FindResource("SymbolThemeFontFamily") } : null,
            };
        }

        /// <summary>Commutateur de développement <c>--snapshot</c> : enregistre la fenêtre dans un PNG puis quitte.</summary>
        public void SaveSnapshot(String path)
        {
            DevSnapshot.Save(this, path);
            Application.Current.Shutdown();
        }
    }
}