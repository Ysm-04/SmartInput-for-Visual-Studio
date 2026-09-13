using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace SmartInput.VisualStudio
{
    internal sealed class CaretBlinkTimer : ICaretBlinkTimer
    {
        private readonly DispatcherTimer timer;
        public event EventHandler Tick;

        public CaretBlinkTimer(Dispatcher dispatcher)
        {
            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
            timer.Tick += OnTick;
        }

        public void Restart()
        {
            timer.Stop();
            uint interval = GetCaretBlinkTime();
            // Respect the system's non-blinking caret option; never change that option.
            if (interval == 0 || interval == uint.MaxValue) return;
            timer.Interval = TimeSpan.FromMilliseconds(interval);
            timer.Start();
        }
        public void Stop() { timer.Stop(); }
        private void OnTick(object sender, EventArgs e) { Tick?.Invoke(this, e); }
        public void Dispose() { timer.Stop(); timer.Tick -= OnTick; }
        [DllImport("user32.dll")] private static extern uint GetCaretBlinkTime();
    }
}
