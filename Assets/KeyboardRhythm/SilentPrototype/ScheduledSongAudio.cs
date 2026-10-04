using System;
using UnityEngine;

namespace KeyboardRhythm.SilentPrototype
{
    internal sealed class ScheduledSongAudio : IDisposable
    {
        private readonly GameObject root;
        private readonly AudioSource source;
        private double zeroAtAudio;
        public bool HasClip => source.clip != null;
        public double EndChartSeconds => HasClip ? (double)source.clip.samples / source.clip.frequency - zeroAtAudio : double.NegativeInfinity;

        public ScheduledSongAudio(Transform owner)
        {
            root = new GameObject("ScheduledSongAudio");
            root.transform.SetParent(owner, false);
            source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.loop = false;
        }

        public void Configure(string chartPath, AudioData audio)
        {
            Stop();
            source.clip = null;
            zeroAtAudio = audio.chartZeroAtAudioSeconds;
            string path = MusicTiming.ResourcePath(chartPath, audio.path);
            if (path == "") return;
            source.clip = Resources.Load<AudioClip>(path);
            if (!HasClip) throw new ArgumentException("Missing audio: Resources/" + path + " (" + audio.path + ")");
            if (source.clip.loadState == AudioDataLoadState.Unloaded && !source.clip.LoadAudioData())
                throw new ArgumentException("Could not load audio: " + audio.path);
            if (source.clip.loadState == AudioDataLoadState.Failed) throw new ArgumentException("Audio import failed: " + audio.path);
        }

        public void Schedule(double origin, double rawChartSeconds)
        {
            Stop();
            if (!HasClip || rawChartSeconds >= EndChartSeconds) return;
            double position = Math.Max(0, MusicTiming.AudioSeconds(rawChartSeconds, zeroAtAudio));
            source.timeSamples = (int)Math.Min(source.clip.samples - 1, Math.Floor(position * source.clip.frequency));
            source.PlayScheduled(MusicTiming.ScheduledStart(origin, rawChartSeconds, zeroAtAudio));
        }

        public void Stop() { source.Stop(); }
        public void Dispose() { Stop(); UnityEngine.Object.Destroy(root); }
    }
}
