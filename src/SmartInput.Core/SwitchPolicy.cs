using System;

namespace SmartInput.Core
{
    /// <summary>Pure state machine. Keyboard events alone never count as an input-mode change.</summary>
    public sealed class SwitchPolicy
    {
        private InputContext context;
        private InputMode? manual;
        public bool Paused { get; set; }
        public bool HasManualOverride => manual.HasValue;
        public InputMode Recommended => context?.Recommended ?? InputMode.English;
        public InputMode Desired => manual ?? Recommended;

        public void SetContext(InputContext next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (context == null || context.Key != next.Key) manual = null;
            context = next;
            if (manual == Recommended) manual = null;
        }

        public void ObserveManualChange(InputMode actual)
        {
            if (actual == InputMode.Unknown || context == null || Paused) return;
            manual = actual == Recommended ? (InputMode?)null : actual;
        }

        public InputMode? Request(InputMode actual, bool composing, bool focused)
        {
            if (Paused || composing || !focused || context == null || actual == InputMode.Unknown || actual == Desired) return null;
            return Desired;
        }

        public bool IsRed(InputMode actual) => !Paused && manual.HasValue && actual == manual && actual != Recommended;
        public void Reset() { context = null; manual = null; }
    }
}
