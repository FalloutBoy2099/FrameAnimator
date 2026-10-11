namespace FrameAnimator.App.Views
{
    using System;
    using System.Globalization;
    using System.Windows;
    using Infrastructure;

    /// <summary>
    /// Remembers the position and size of windows (main window and dialogs) from one session to the next, according to
    /// two user options. A window registers here with <see cref="Attach"/> in its constructor; each window type has
    /// its own entry in <see cref="UserSettings"/>, in the format "left,top,width,height,maximized".
    /// </summary>
    public static class WindowMemory
    {
        private const String PositionKey = "savePosition";
        private const String SizeKey = "saveSize";

        private static Boolean? _savePosition;
        private static Boolean? _saveSize;

        /// <summary>Remember the position of all windows.</summary>
        public static Boolean SavePosition
        {
            get => _savePosition ??= UserSettings.Get(PositionKey) == "1";
            set
            {
                _savePosition = value;
                UserSettings.Set(PositionKey, value ? "1" : "0");
            }
        }

        /// <summary>Remember the size of all windows (and the "maximized" state).</summary>
        public static Boolean SaveSize
        {
            get => _saveSize ??= UserSettings.Get(SizeKey) == "1";
            set
            {
                _saveSize = value;
                UserSettings.Set(SizeKey, value ? "1" : "0");
            }
        }

        public static void Attach(Window window)
        {
            // message boxes are transient: they stay centered on the window that calls them
            if (window is MessageDialog)
                return;

            String key = "window." + window.GetType().Name;
            Restore(window, key);
            window.Closing += (_, _) => Save(window, key);
        }

        private static Boolean CanResize(Window window)
            => window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip && window.SizeToContent == SizeToContent.Manual;

        private static String[]? Read(String key)
        {
            String? text = UserSettings.Get(key);
            String[]? parts = text?.Split(',');
            return parts is { Length: 5 } ? parts : null;
        }

        private static Boolean TryParse(String text, out Double value)
            => Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Double.IsFinite(value);

        private static void Restore(Window window, String key)
        {
            String[]? parts = Read(key);
            if (parts is null)
                return;

            if (SaveSize && CanResize(window) && TryParse(parts[2], out Double width) && TryParse(parts[3], out Double height))
            {
                window.Width = Math.Clamp(width, Math.Max(window.MinWidth, 100), Math.Max(window.MinWidth, SystemParameters.VirtualScreenWidth));
                window.Height = Math.Clamp(height, Math.Max(window.MinHeight, 100), Math.Max(window.MinHeight, SystemParameters.VirtualScreenHeight));
                if (parts[4] == "1")
                    window.WindowState = WindowState.Maximized;
            }

            if (SavePosition && TryParse(parts[0], out Double left) && TryParse(parts[1], out Double top) && IsReachable(left, top))
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = left;
                window.Top = top;
            }
        }

        /// <summary>The remembered position must still leave the title bar within reach (a monitor may have been unplugged).</summary>
        private static Boolean IsReachable(Double left, Double top)
            => left > SystemParameters.VirtualScreenLeft - 200
               && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 60
               && top >= SystemParameters.VirtualScreenTop
               && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60;

        private static void Save(Window window, String key)
        {
            if (!SavePosition && !SaveSize)
                return;

            Rect bounds = window.WindowState == WindowState.Normal
                ? new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight)
                : window.RestoreBounds;
            if (!Double.IsFinite(bounds.Left) || !Double.IsFinite(bounds.Top) || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            // only the values whose option is enabled are updated; the others keep their previous value
            String[] parts = Read(key) ?? ["", "", "", "", "0"];
            if (SavePosition)
            {
                parts[0] = bounds.Left.ToString("0.##", CultureInfo.InvariantCulture);
                parts[1] = bounds.Top.ToString("0.##", CultureInfo.InvariantCulture);
            }
            if (SaveSize && CanResize(window))
            {
                parts[2] = bounds.Width.ToString("0.##", CultureInfo.InvariantCulture);
                parts[3] = bounds.Height.ToString("0.##", CultureInfo.InvariantCulture);
                parts[4] = window.WindowState == WindowState.Maximized ? "1" : "0";
            }
            UserSettings.Set(key, String.Join(',', parts));
        }
    }
}