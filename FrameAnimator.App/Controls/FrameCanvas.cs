namespace FrameAnimator.App.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.DependencyInjection;
    using CommunityToolkit.Mvvm.Messaging;
    using Microsoft.Win32;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using FrameAnimator.Core.Geometry;
    using FrameAnimator.Core.Model;
	using Infrastructure;
	using Services;
	using ViewModels;

	/// <summary>
	/// Drawing area (<c>pbxFramePaint</c>, specification §6.2): background, "hexes" tile, ghost frame, then the selected frame,
	/// all centered on the middle of the control. Handles the Move and Pick tools (§6.3).
	/// </summary>
	public sealed class FrameCanvas : FrameworkElement
    {
        private static readonly Brush LightBackground = CreateBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
        private static readonly Brush DarkBackground = CreateBrush(Color.FromRgb(0x20, 0x20, 0x20));

        // the "hexes" tile is the same for every canvas: decoded once
        private static BitmapSource? s_hexes;
        private static Boolean s_hexesLoaded;

        private CanvasViewModel? _vm;
        private Boolean _themeKnown;
        private Boolean _dark;
        private Boolean _themeSubscribed;
        private Transform? _scale;
        private Int32 _scaleZoom;

        public FrameCanvas()
        {
            ClipToBounds = true;
            Focusable = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            DataContextChanged += (_, _) => Attach();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static Brush CreateBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private void OnLoaded(Object sender, RoutedEventArgs e)
        {
            SubscribeTheme();
            Attach();
        }

        private void OnUnloaded(Object sender, RoutedEventArgs e)
        {
            UnsubscribeTheme();
            if (_vm is not null)
            {
                _vm.RenderRequested -= OnRenderRequested;
                _vm = null;
            }
        }

        private void Attach()
        {
            if (_vm is not null)
                _vm.RenderRequested -= OnRenderRequested;
            _vm = DataContext as CanvasViewModel;
            if (_vm is not null)
                _vm.RenderRequested += OnRenderRequested;
            InvalidateVisual();
        }

        private void OnRenderRequested(Object? sender, EventArgs e) => InvalidateVisual();

        // ------------------------------------------------------------------ theme

        private void SubscribeTheme()
        {
            if (_themeSubscribed)
                return;
            _themeSubscribed = true;
            WeakReferenceMessenger.Default.Register<FrameCanvas, ThemeChangedMessage>(this, static (canvas, _) => canvas.RefreshTheme());
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            // the preference may have changed while the canvas was not loaded
            RefreshTheme();
        }

        private void UnsubscribeTheme()
        {
            if (!_themeSubscribed)
                return;
            _themeSubscribed = false;
            WeakReferenceMessenger.Default.Unregister<ThemeChangedMessage>(this);
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }

        // raised on the SystemEvents thread
        private void OnUserPreferenceChanged(Object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
                Dispatcher.BeginInvoke(RefreshTheme);
        }

        /// <summary>The theme comes from the application's service (a canvas is created from XAML, it cannot receive it by constructor).</summary>
        private static Boolean IsDarkTheme() => Ioc.Default.GetService<IThemeService>()?.IsDark ?? false;

        /// <summary>Reads the theme once (registry access when the preference is Automatic) and redraws if it changed.</summary>
        private void RefreshTheme()
        {
            var dark = IsDarkTheme();
            if (_themeKnown && dark == _dark)
                return;
            _themeKnown = true;
            _dark = dark;
            InvalidateVisual();
        }

        // ------------------------------------------------------------------ drawing

        private static BitmapSource? Hexes()
        {
            if (s_hexesLoaded)
                return s_hexes;
            s_hexesLoaded = true;
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
                var hexes = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, data, width * 4);
                hexes.Freeze();
                s_hexes = hexes;
            }
            catch (Exception)
            {
                s_hexes = null;
            }
            return s_hexes;
        }

        /// <summary>Frozen zoom transform, rebuilt only when the zoom changes.</summary>
        private Transform ScaleFor(Int32 zoom)
        {
            if (_scale is null || _scaleZoom != zoom)
            {
                var scale = new ScaleTransform(zoom, zoom);
                scale.Freeze();
                _scale = scale;
                _scaleZoom = zoom;
            }
            return _scale;
        }

        protected override void OnRender(DrawingContext dc)
        {
            var w = ActualWidth;
            var h = ActualHeight;
            if (!_themeKnown)
            {
                _dark = IsDarkTheme();
                _themeKnown = true;
            }
            dc.DrawRectangle(_dark ? DarkBackground : LightBackground, null, new Rect(0, 0, w, h));
            // during Automate, the project is modified by another thread: do not read it
            if (_vm is null || _vm.IsBusy)
                return;

            var zoom = _vm.Zoom;
            var cx = (Int32)(w / zoom) / 2;
            var cy = (Int32)(h / zoom) / 2;
            var ox = cx;
            var oy = cy;

            var cache = FrameBitmapCache.Shared;
            dc.PushTransform(ScaleFor(zoom));
            try
            {
                var project = _vm.Project;
                if (_vm.ShowBackground && project.Background is { } bg)
                {
                    dc.DrawImage(cache.Get(bg, -1), new Rect(ox - bg.Width / 2, oy - bg.Height / 2, bg.Width, bg.Height));
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
                    dc.DrawImage(cache.Get(gf.Bitmap, gt, 0xFF0000),
                        new Rect(gx, gy, gf.Bitmap.Width, gf.Bitmap.Height));
                }

                var frame = dir.Frames[sel];
                var transp = FrameMath.TransparentColor(project, frame);
                var (fx, fy) = FrameMath.DisplayPosition(dir, sel, cx, cy);
                dc.DrawImage(cache.Get(frame.Bitmap, transp),
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

        // ------------------------------------------------------------------ keyboard

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_vm is null)
                return;
            (Int32 dx, Int32 dy) = e.Key switch
            {
                Key.Left => (-1, 0),
                Key.Right => (1, 0),
                Key.Up => (0, -1),
                Key.Down => (0, 1),
                _ => (0, 0),
            };
            if (dx == 0 && dy == 0)
                return;
            ModifierKeys modifiers = Keyboard.Modifiers;
            Int32 step = (modifiers & ModifierKeys.Shift) != 0 ? 10 : 1;
            if (_vm.Nudge(dx * step, dy * step, (modifiers & ModifierKeys.Control) != 0))
                e.Handled = true;
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