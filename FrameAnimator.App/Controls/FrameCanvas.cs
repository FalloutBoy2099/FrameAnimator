namespace FrameAnimator.App.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using FrameAnimator.Core.Geometry;
    using FrameAnimator.Core.Model;
	using Infrastructure;
	using ViewModels;
	using Views;

	/// <summary>
	/// Drawing area (<c>pbxFramePaint</c>, specification §6.2): background, "hexes" tile, ghost frame, then the selected frame,
	/// all centered on the middle of the control. Handles the Move and Pick tools (§6.3).
	/// </summary>
	public sealed class FrameCanvas : FrameworkElement
    {
        private MainViewModel? _vm;
        private BitmapSource? _hexes;
        private Bitmap32? _backgroundSource;
        private BitmapSource? _backgroundImage;

        public FrameCanvas()
        {
            ClipToBounds = true;
            Focusable = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            DataContextChanged += (_, _) => Attach();
            Loaded += (_, _) => Attach();
        }

        private void Attach()
        {
            if (_vm is not null)
                _vm.RenderRequested -= OnRenderRequested;
            _vm = DataContext as MainViewModel;
            if (_vm is not null)
                _vm.RenderRequested += OnRenderRequested;
            InvalidateVisual();
        }

        private void OnRenderRequested(Object? sender, EventArgs e) => InvalidateVisual();

        private BitmapSource? Hexes()
        {
            if (_hexes is not null)
                return _hexes;
            try
            {
                var decoder = new PngBitmapDecoder(new Uri("pack://application:,,,/Assets/hexes.png"),
                    BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var bgra = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
                var width = bgra.PixelWidth;
                var height = bgra.PixelHeight;
                var data = new Byte[width * height * 4];
                bgra.CopyPixels(data, width * 4, 0);
                for (var i = 0; i < data.Length; i += 4)
                {
                    // clSilver is the tile's transparent color
                    if (data[i] == 0xC0 && data[i + 1] == 0xC0 && data[i + 2] == 0xC0)
                        data[i] = data[i + 1] = data[i + 2] = data[i + 3] = 0;
                }
                _hexes = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, data, width * 4);
                _hexes.Freeze();
            }
            catch (Exception)
            {
                _hexes = null;
            }
            return _hexes;
        }

        protected override void OnRender(DrawingContext dc)
        {
            var w = ActualWidth;
            var h = ActualHeight;
            dc.DrawRectangle(new SolidColorBrush(ThemeManager.IsDark() ? Color.FromRgb(0x20, 0x20, 0x20) : Color.FromRgb(0xF3, 0xF3, 0xF3)), null, new Rect(0, 0, w, h));
            // during Automate, the project is modified by another thread: do not read it
            if (_vm is null || _vm.IsBusy)
                return;

            var zoom = _vm.Zoom;
            var cx = (Int32)(w / zoom) / 2;
            var cy = (Int32)(h / zoom) / 2;
            var ox = cx;
            var oy = cy;

            dc.PushTransform(new ScaleTransform(zoom, zoom));
            try
            {
                var project = _vm.Project;
                if (_vm.ShowBackground && project.Background is { } bg)
                {
                    if (!ReferenceEquals(_backgroundSource, bg))
                    {
                        _backgroundSource = bg;
                        _backgroundImage = ImageConversion.ToBitmapSource(bg, -1);
                    }
                    dc.DrawImage(_backgroundImage!, new Rect(ox - bg.Width / 2, oy - bg.Height / 2, bg.Width, bg.Height));
                }

                if (_vm.ShowHexes && Hexes() is { } hexes)
                {
                    dc.DrawImage(hexes, new Rect(ox - hexes.PixelWidth / 2, oy - hexes.PixelHeight / 2,
                        hexes.PixelWidth, hexes.PixelHeight));
                }

                var dir = project.Directions[project.CurrentDirection];
                var sel = _vm.SelectedFrameIndex;
                if (dir.Count == 0 || sel < 0 || sel >= dir.Count)
                    return;

                var ghost = _vm.ShowGhost ? project.Settings.GhostIncrement : 0;
                var gi = sel + ghost;
                if (ghost != 0 && gi >= 0 && gi < dir.Count)
                {
                    var gf = dir.Frames[gi];
                    var gt = FrameMath.TransparentColor(project, gf);
                    var (gx, gy) = FrameMath.DisplayPosition(dir, gi, cx, cy);
                    dc.DrawImage(ImageConversion.ToBitmapSource(gf.Bitmap, gt, 0xFF0000),
                        new Rect(gx, gy, gf.Bitmap.Width, gf.Bitmap.Height));
                }

                var frame = dir.Frames[sel];
                var transp = FrameMath.TransparentColor(project, frame);
                var (fx, fy) = FrameMath.DisplayPosition(dir, sel, cx, cy);
                dc.DrawImage(ImageConversion.ToBitmapSource(frame.Bitmap, transp),
                    new Rect(fx, fy, frame.Bitmap.Width, frame.Bitmap.Height));
            }
            finally
            {
                dc.Pop();
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            InvalidateVisual();
        }

        // ------------------------------------------------------------------ mouse

        private (Int32 X, Int32 Y) ToLocal(MouseEventArgs e)
        {
            var p = e.GetPosition(this);
            var zoom = _vm?.Zoom ?? 1;
            var cx = (Int32)(ActualWidth / zoom) / 2;
            var cy = (Int32)(ActualHeight / zoom) / 2;
            return ((Int32)Math.Floor(p.X / zoom) - cx, (Int32)Math.Floor(p.Y / zoom) - cy);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (_vm is null)
                return;
            Focus();
            var (x, y) = ToLocal(e);
            _vm.CanvasMouseDown(x, y);
            if (_vm.IsMoveTool)
                CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_vm is null || e.LeftButton != MouseButtonState.Pressed)
                return;
            var (x, y) = ToLocal(e);
            var mods = Keyboard.Modifiers;
            _vm.CanvasMouseMove(x, y, (mods & ModifierKeys.Shift) != 0, (mods & ModifierKeys.Control) != 0);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _vm?.CanvasMouseUp();
            ReleaseMouseCapture();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_vm is null || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
                return;
            _vm.Zoom += e.Delta > 0 ? 1 : -1;
            e.Handled = true;
        }
    }
}