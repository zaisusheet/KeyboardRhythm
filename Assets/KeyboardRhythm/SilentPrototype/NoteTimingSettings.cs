using System;

namespace KeyboardRhythm.SilentPrototype
{
    // Device preference, independent of chart/audio offsets and judgement window sizes.
    public sealed class NoteTimingSettings
    {
        // Demo UI limits/steps (SPEC U10), not changes to note judgement rules.
        public const int LimitMilliseconds = 500;
        public const int NormalStepMilliseconds = 10, FineStepMilliseconds = 1;
        public const string PreferenceKey = "KeyboardRhythm.NoteTimingOffsetMs.v1";
        private bool playbackStarted;
        public int SavedMilliseconds { get; private set; }
        public int ActiveMilliseconds { get; private set; }
        public bool HasPendingChange { get { return SavedMilliseconds != ActiveMilliseconds; } }

        public NoteTimingSettings(int savedMilliseconds)
        {
            SetMilliseconds(savedMilliseconds);
        }

        public bool SetMilliseconds(int milliseconds)
        {
            int bounded = Math.Max(-LimitMilliseconds, Math.Min(LimitMilliseconds, milliseconds));
            bool changed = SavedMilliseconds != bounded;
            SavedMilliseconds = bounded;
            if (!playbackStarted) ActiveMilliseconds = bounded;
            return changed;
        }

        // Pause/resume retains the active value. A fresh session applies pending changes.
        public void BeginPlayback() { playbackStarted = true; }
        public void ResetPlayback() { playbackStarted = false; ActiveMilliseconds = SavedMilliseconds; }

        // Positive offset delays all note positions AND judgement relative to the unmodified audio clock.
        public double ChartSeconds(double audioChartSeconds)
        {
            return audioChartSeconds - ActiveMilliseconds / 1000.0;
        }

        // Let both the audio timeline and delayed notes finish; an early offset never cuts audio short.
        public bool HasFinished(double audioChartSeconds, double chartEndSeconds)
        {
            return audioChartSeconds >= chartEndSeconds && ChartSeconds(audioChartSeconds) >= chartEndSeconds;
        }
    }
}
