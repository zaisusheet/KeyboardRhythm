using System;
using UnityEngine;

namespace KeyboardRhythm.SilentPrototype
{
    internal sealed class BeatMetronome : IDisposable
    {
        private readonly GameObject root;
        private readonly AudioSource[] sources = new AudioSource[16];
        private readonly double[] availableAt = new double[16];
        private readonly AudioClip click, accent;
        private readonly AudioListener generatedListener;
        private MetronomeBeatGrid grid;
        private double nextBeat, endSeconds;
        private bool running;

        public BeatMetronome(Transform owner)
        {
            root = new GameObject("GeneratedBeatMetronome");
            root.transform.SetParent(owner, false);
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
                generatedListener = root.AddComponent<AudioListener>();
            click = CreateClick("BeatClick", 1000);
            accent = CreateClick("BarClick", 1500);
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = root.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0;
                sources[i].volume = 0.22f;
            }
        }

        public void Configure(double bpm, double end) { Stop(); grid = new MetronomeBeatGrid(bpm); endSeconds = end; }
        public void Begin(double chartSeconds) { Stop(); nextBeat = grid.BeatAtOrAfter(chartSeconds); running = true; }

        public void Schedule(double dspOrigin)
        {
            if (!running) return;
            double now = AudioSettings.dspTime;
            // Drop stale beats after a stalled frame. Do not burst all missed clicks at once.
            nextBeat = Math.Max(nextBeat, grid.BeatAtOrAfter(now + 0.02 - dspOrigin));
            for (int scheduled = 0; scheduled < sources.Length; scheduled++)
            {
                double seconds = grid.SecondsAtBeat(nextBeat), dsp = dspOrigin + seconds;
                if (seconds >= endSeconds || dsp > now + 0.5) return;
                int free = -1;
                for (int i = 0; i < sources.Length; i++)
                    if (availableAt[i] <= now) { free = i; break; }
                if (free < 0) return;
                AudioSource source = sources[free];
                source.clip = grid.IsAccent(nextBeat) ? accent : click;
                source.PlayScheduled(dsp);
                availableAt[free] = dsp + source.clip.length + 0.02;
                nextBeat++;
            }
        }

        public void Stop()
        {
            running = false;
            for (int i = 0; i < sources.Length; i++) { sources[i].Stop(); availableAt[i] = 0; }
        }

        private static AudioClip CreateClick(string name, double frequency)
        {
            const int rate = 48000;
            var samples = new float[1440];
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / rate;
                samples[i] = (float)(Math.Sin(2 * Math.PI * frequency * t) * Math.Exp(-150 * t) *
                    Math.Min(1, t * 1500) * (1.0 - (double)i / samples.Length));
            }
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void Dispose()
        {
            Stop();
            if (generatedListener != null) generatedListener.enabled = false;
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(click);
            UnityEngine.Object.Destroy(accent);
        }
    }
}
