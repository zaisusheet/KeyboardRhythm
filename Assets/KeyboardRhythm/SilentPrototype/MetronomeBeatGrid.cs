using System;

namespace KeyboardRhythm.SilentPrototype
{
    // Constant BPM prototype: events do not change this grid. Negative beats provide count-in clicks.
    public sealed class MetronomeBeatGrid
    {
        public double SecondsPerBeat { get; private set; }
        public MetronomeBeatGrid(double bpm)
        {
            if (double.IsNaN(bpm) || double.IsInfinity(bpm) || bpm <= 0)
                throw new ArgumentOutOfRangeException(nameof(bpm));
            SecondsPerBeat = 60.0 / bpm;
        }
        public double BeatAtOrAfter(double seconds) { return Math.Ceiling(seconds / SecondsPerBeat - 1e-9); }
        public double SecondsAtBeat(double beat) { return beat * SecondsPerBeat; }
        // Four beats per bar is a demo cue, not a new chart-format time-signature rule.
        public bool IsAccent(double beat) { return beat % 4 == 0; }
    }
}
