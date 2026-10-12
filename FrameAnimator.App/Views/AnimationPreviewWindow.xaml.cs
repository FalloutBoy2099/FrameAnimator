namespace FrameAnimator.App.Views
{
    using System;
    using System.Windows;
    using System.Windows.Input;
    using FrameAnimator.App.ViewModels;

    /// <summary>
    /// Non-modal window that plays the current direction (see <see cref="AnimationPreviewViewModel"/>). It starts the animation when it opens and
    /// stops it when it closes; Ctrl + wheel zooms.
    /// </summary>
    public partial class AnimationPreviewWindow : Window
    {
        public AnimationPreviewWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => ViewModel?.Open();
            Closed += (_, _) => ViewModel?.Close();
        }

        private AnimationPreviewViewModel? ViewModel => DataContext as AnimationPreviewViewModel;

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (ViewModel is { } viewModel && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                viewModel.Zoom += e.Delta > 0 ? 1 : -1;
                e.Handled = true;
            }
        }
    }
}
