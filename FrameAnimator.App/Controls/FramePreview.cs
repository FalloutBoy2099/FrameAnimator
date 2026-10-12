namespace FrameAnimator.App.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Media;
    using CommunityToolkit.Mvvm.DependencyInjection;
    using CommunityToolkit.Mvvm.Messaging;
    using FrameAnimator.App.Infrastructure;
    using FrameAnimator.App.Services;
    using FrameAnimator.App.ViewModels;
    using FrameAnimator.Core.Geometry;
    using FrameAnimator.Core.Model;

    /// <summary>
    /// Draws the frame the <see cref="AnimationPreviewViewModel"/> is showing, centered like in the main drawing area (frames keep their
    /// offsets, so the animation looks like it will in the game). It only draws: the view model owns the timing.
    /// </summary>
    public sealed class FramePreview : FrameworkElement
    {
        private static readonly Brush LightBackground = CreateBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
        private static readonly Brush DarkBackground = CreateBrush(Color.FromRgb(0x20, 0x20, 0x20));

        private AnimationPreviewViewModel? _vm;

        public FramePreview()
        {
            ClipToBounds = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            DataContextChanged += (_, _) => Attach();
            Loaded += (_, _) =>
            {
                WeakReferenceMessenger.Default.Register<FramePreview, ThemeChangedMessage>(this, static (preview, _) => preview.InvalidateVisual());
                Attach();
            };
            Unloaded += (_, _) =>
            {
                WeakReferenceMessenger.Default.Unregister<ThemeChangedMessage>(this);
                Detach();
            };
        }

        private static Brush CreateBrush(Color color)
        {
            SolidColorBrush brush = new(color);
            brush.Freeze();
            return brush;
        }

        private void Attach()
        {
            Detach();
            _vm = DataContext as AnimationPreviewViewModel;
            if (_vm is not null)
                _vm.RenderRequested += OnRenderRequested;
            InvalidateVisual();
        }

        private void Detach()
        {
            if (_vm is not null)
                _vm.RenderRequested -= OnRenderRequested;
            _vm = null;
        }

        private void OnRenderRequested(Object? sender, EventArgs e) => InvalidateVisual();

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            Boolean dark = Ioc.Default.GetService<IThemeService>()?.IsDark ?? false;
            dc.DrawRectangle(dark ? DarkBackground : LightBackground, null, new Rect(0, 0, ActualWidth, ActualHeight));
            // a script is modifying the project from another thread: do not read it
            if (_vm is null || _vm.IsBusy)
                return;

            Project project = _vm.Project;
            Direction dir = project.Directions[project.CurrentDirection];
            Int32 index = _vm.FrameIndex;
            if (dir.Count == 0 || index < 0 || index >= dir.Count)
                return;

            Int32 zoom = _vm.Zoom;
            Int32 cx = (Int32)(ActualWidth / zoom) / 2;
            Int32 cy = (Int32)(ActualHeight / zoom) / 2;
            Frame frame = dir.Frames[index];
            (Int32 x, Int32 y) = FrameMath.DisplayPosition(dir, index, cx, cy);

            ScaleTransform scale = new(zoom, zoom);
            scale.Freeze();
            dc.PushTransform(scale);
            try
            {
                dc.DrawImage(FrameBitmapCache.Shared.Get(frame.Bitmap, FrameMath.TransparentColor(project, frame)),
                    new Rect(x, y, frame.Bitmap.Width, frame.Bitmap.Height));
            }
            finally
            {
                dc.Pop();
            }
        }
    }
}
