namespace FrameAnimator.App.Infrastructure
{
    using System;
    using System.Windows.Threading;

    /// <summary>A repeating timer on the UI thread (what the animation preview is paced by); the tests drive it by hand.</summary>
    public interface ITicker
    {
        /// <summary>Raised every <see cref="Interval"/> while the ticker runs.</summary>
        event EventHandler? Tick;

        TimeSpan Interval { get; set; }

        Boolean IsRunning { get; }

        void Start();

        void Stop();
    }

    /// <summary><see cref="ITicker"/> over a <see cref="DispatcherTimer"/>.</summary>
    public sealed class DispatcherTicker : ITicker
    {
        private readonly DispatcherTimer _timer = new(DispatcherPriority.Render);

        public DispatcherTicker() => _timer.Tick += (_, _) => Tick?.Invoke(this, EventArgs.Empty);

        public event EventHandler? Tick;

        public TimeSpan Interval
        {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        public Boolean IsRunning => _timer.IsEnabled;

        public void Start() => _timer.Start();

        public void Stop() => _timer.Stop();
    }
}
