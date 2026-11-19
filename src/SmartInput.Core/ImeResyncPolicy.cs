using System;

namespace SmartInput.Core
{
    /// <summary>
    /// Decides when the editor must write the desired state even though a read already reports
    /// that same state.
    ///
    /// Right after the user switches keyboards (for example Microsoft Pinyin -> Sogou Pinyin)
    /// the TSF / IMM32 state channels can still report the previous input method's Chinese/English
    /// mode (or the newly activated method can ignore a write issued before it finished
    /// activating). A single write can therefore be confirmed by a stale read while the actual
    /// input method keeps its old mode. After every input-method change this policy asks for a
    /// short burst of re-synchronisation writes (three editor ticks, ~0.3-0.6s on the normal poll
    /// interval) so the newly activated method really receives the target state; the writes are
    /// idempotent, and the normal confirmation read still decides whether a switch succeeded.
    /// </summary>
    public sealed class ImeResyncPolicy
    {
        private const int BurstWrites = 3;
        private int remaining;

        public bool IsPending => remaining > 0;

        public void NoteActiveInputMethodChanged() { remaining = BurstWrites; }

        public void Reset() { remaining = 0; }

        /// <summary>Returns true while the burst has writes left for a known actual/desired mode.</summary>
        public bool ShouldForceWrite(InputMode actual, InputMode desired)
        {
            if (remaining <= 0) return false;
            if (actual == InputMode.Unknown || desired == InputMode.Unknown) return false;
            remaining--;
            return true;
        }
    }
}
