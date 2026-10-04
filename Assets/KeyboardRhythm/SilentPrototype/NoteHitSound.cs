using System;
using UnityEngine;

namespace KeyboardRhythm.SilentPrototype
{
    internal sealed class NoteHitSound : IDisposable
    {
        public const string PreferenceKey = "KeyboardRhythm.HitSoundEnabled.v1";
        private readonly GameObject root;
        private readonly AudioSource source;
        private readonly AudioClip clip;

        public NoteHitSound(Transform owner, float volume)
        {
            root = new GameObject("GeneratedNoteHitSound");
            root.transform.SetParent(owner, false);
            source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = Mathf.Clamp(volume, 0, 0.2f);
            clip = AudioClip.Create("NoteHitClick", HitSoundWaveform.SampleCount, 1, HitSoundWaveform.SampleRate, false);
            clip.SetData(HitSoundWaveform.Create(), 0);
        }

        // One quiet click for a same-frame chord; do not stack its notes into a loud burst.
        public void Play() { source.PlayOneShot(clip); }
        public void Stop() { source.Stop(); }
        public void Dispose()
        {
            Stop();
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(clip);
        }
    }
}
