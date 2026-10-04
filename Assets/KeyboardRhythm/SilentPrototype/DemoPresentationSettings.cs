using System;

namespace KeyboardRhythm.SilentPrototype
{
    // Demo defaults (SPEC U10). These affect presentation, never the chart clock.
    public sealed class ScrollSpeedSettings
    {
        public const int MinimumTenths = 5, MaximumTenths = 30, DefaultTenths = 10;
        public const string PreferenceKey = "KeyboardRhythm.ScrollSpeedTenths.v1";
        public int Tenths { get; private set; }
        public double Multiplier => Tenths / 10.0;

        public ScrollSpeedSettings(int tenths) { SetTenths(tenths); }
        public bool SetTenths(int tenths)
        {
            int bounded = Math.Max(MinimumTenths, Math.Min(MaximumTenths, tenths));
            bool changed = bounded != Tenths;
            Tenths = bounded;
            return changed;
        }
        public double TravelSeconds(double baseTravelSeconds)
        {
            if (double.IsNaN(baseTravelSeconds) || double.IsInfinity(baseTravelSeconds) || baseTravelSeconds <= 0)
                throw new ArgumentException("Travel time must be finite and positive.");
            return baseTravelSeconds / Multiplier;
        }
    }

    public static class HitSoundWaveform
    {
        public const int SampleRate = 48000, SampleCount = 1440;
        public static float[] Create()
        {
            var samples = new float[SampleCount];
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / SampleRate;
                double envelope = Math.Min(1, t * 1500) * Math.Exp(-140 * t) *
                    (1.0 - (double)i / (samples.Length - 1));
                samples[i] = (float)(0.65 * Math.Sin(2 * Math.PI * 1200 * t) * envelope);
            }
            return samples;
        }
    }
}
