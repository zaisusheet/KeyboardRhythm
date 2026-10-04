using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KeyboardRhythm.SilentPrototype
{
    public interface IChartClock
    {
        double Seconds { get; }
        void Start(double initialSeconds);
        void Pause();
        void Resume();
    }

    // Song, judgement, drawing and scheduled clicks share the DSP clock.
    internal sealed class SilentChartClock : IChartClock
    {
        private double origin, pausedSeconds;
        private bool running;
        public double DspOrigin { get { return origin; } }
        public double Seconds { get { return running ? Math.Max(pausedSeconds, AudioSettings.dspTime - origin) : pausedSeconds; } }
        public void Start(double initialSeconds)
        {
            pausedSeconds = initialSeconds;
            origin = AudioSettings.dspTime + 0.15 - initialSeconds;
            running = true;
        }
        public void Pause() { pausedSeconds = Seconds; running = false; }
        public void Resume() { origin = AudioSettings.dspTime + 0.15 - pausedSeconds; running = true; }
    }

    [DisallowMultipleComponent]
    public sealed class SilentChartPlayer : MonoBehaviour
    {
        [SerializeField] private string chartResourcePath = "Charts/silent_demo";
        [SerializeField] private PrototypeSettings settings = new PrototypeSettings();
        [SerializeField] private bool metronomeEnabled = true;
        [SerializeField, Range(0f, 0.2f)] private float hitSoundVolume = 0.08f;
        private readonly bool[] newlyPressed = new bool[31];
        private readonly bool[] heldLanes = new bool[11];
        private readonly bool[] heldKeys = new bool[31];
        private readonly SilentChartClock clock = new SilentChartClock();
        private NoteTimingSettings noteTiming;
        private ScrollSpeedSettings scrollSpeed;
        private NoteHitSound hitSound;
        private bool hitSoundEnabled;
        private BeatMetronome metronome;
        private ScheduledSongAudio song;
        private bool metronomeWithMusic;
        private bool ClicksEnabled => song.HasClip ? metronomeWithMusic : metronomeEnabled;
        private double InitialSeconds => song.HasClip ? MusicTiming.InitialSeconds(leadInSeconds, chart.audio.chartZeroAtAudioSeconds) : -leadInSeconds;
        private RhythmSession session;
        private SilentChartView view;
        private ChartData chart;
        private bool started, paused, finished;
        private double travelSeconds, leadInSeconds;

        public void SetChartResourcePath(string path) { chartResourcePath = path; }

        private void Start()
        {
            noteTiming = new NoteTimingSettings(PlayerPrefs.GetInt(NoteTimingSettings.PreferenceKey, 0));
            scrollSpeed = new ScrollSpeedSettings(PlayerPrefs.GetInt(ScrollSpeedSettings.PreferenceKey, ScrollSpeedSettings.DefaultTenths));
            hitSoundEnabled = PlayerPrefs.GetInt(NoteHitSound.PreferenceKey, 1) != 0;
            view = new SilentChartView(transform);
            metronome = new BeatMetronome(transform);
            song = new ScheduledSongAudio(transform);
            hitSound = new NoteHitSound(transform, hitSoundVolume);
            var paths = ChartLibrary.LoadPaths(chartResourcePath);
            view.SetChoices(paths.ConvertAll(ChartLibrary.Title), paths.IndexOf(chartResourcePath), index =>
            {
                chartResourcePath = paths[index];
                LoadChart();
            });
            view.SetDemoControls(scrollSpeed, hitSoundEnabled, SetScrollSpeed, SetHitSoundEnabled);
            LoadChart();
        }

        private void LoadChart()
        {
            metronome.Stop();
            song.Stop();
            hitSound.Stop();
            noteTiming.ResetPlayback();
            started = paused = finished = false;
            session = null;
            try
            {
                settings.Validate();
                TextAsset asset = Resources.Load<TextAsset>(chartResourcePath);
                if (asset == null) throw new ArgumentException("Missing Resources/" + chartResourcePath + ".json");
                chart = JsonUtility.FromJson<ChartData>(asset.text);
                session = new RhythmSession(chart, settings);
                song.Configure(chartResourcePath, chart.audio);
                travelSeconds = settings.travelSeconds;
                leadInSeconds = settings.leadInSeconds;
                clock.Start(InitialSeconds);
                clock.Pause();
                view.SetChart(chart, session);
                metronome.Configure(chart.timing.initialBpm, session.EndSeconds);
                Debug.Log("Silent chart loaded: " + chart.chartId + ", " + session.Notes.Count +
                    " notes. " + chart.events.Length + " events retained but not executed.", this);
            }
            catch (Exception ex)
            {
                session = null;
                view.ShowError(ex.Message);
                Debug.LogError("Silent chart: " + ex.Message, this);
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            KeyboardLaneInput.Read(keyboard, newlyPressed, heldLanes, heldKeys);
            UpdateTimingPreference(keyboard);
            UpdateDemoPreferences(keyboard);
            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
            {
                LoadChart(); // Recreates all runtime note states and counters.
                return;
            }
            if (session == null) return;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
            {
                if (song.HasClip) metronomeWithMusic = !metronomeWithMusic;
                else metronomeEnabled = !metronomeEnabled;
                metronome.Stop();
                if (ClicksEnabled && started && !paused && !finished) metronome.Begin(clock.Seconds);
            }
            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame && !view.IsChartMenuOpen && !finished && (!started || paused))
            {
                if (!started) { noteTiming.BeginPlayback(); clock.Start(InitialSeconds); started = true; }
                else if (paused) { clock.Resume(); paused = false; }
                if (ClicksEnabled) { metronome.Begin(clock.Seconds); metronome.Schedule(clock.DspOrigin); }
                song.Schedule(clock.DspOrigin, clock.Seconds);
                Render(keyboard);
                return; // Do not consume game keys on the start/resume frame.
            }
            double now = clock.Seconds;
            if (started && !paused && !finished)
            {
                if (ClicksEnabled) metronome.Schedule(clock.DspOrigin);
                // All 31 physical keys are inspected, including simultaneous presses.
                // Frame polling is sufficient for this prototype; input-event timestamps come later.
                int pressesBefore = session.SuccessfulPressCount;
                session.Step(noteTiming.ChartSeconds(now), newlyPressed, heldKeys);
                if (hitSoundEnabled && session.SuccessfulPressCount > pressesBefore) hitSound.Play();
                if (noteTiming.HasFinished(now, session.EndSeconds))
                {
                    clock.Pause();
                    metronome.Stop();
                    song.Stop();
                    hitSound.Stop();
                    finished = true;
                }
            }
            Render(keyboard);
        }

        private void UpdateTimingPreference(Keyboard keyboard)
        {
            if (keyboard == null) return;
            int step = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ?
                NoteTimingSettings.FineStepMilliseconds : NoteTimingSettings.NormalStepMilliseconds;
            int next = noteTiming.SavedMilliseconds;
            if (keyboard.f6Key.wasPressedThisFrame) next -= step;
            if (keyboard.f7Key.wasPressedThisFrame) next += step;
            if (keyboard.f8Key.wasPressedThisFrame) next = 0;
            if (!noteTiming.SetMilliseconds(next)) return;
            PlayerPrefs.SetInt(NoteTimingSettings.PreferenceKey, noteTiming.SavedMilliseconds);
            PlayerPrefs.Save();
        }

        private void UpdateDemoPreferences(Keyboard keyboard)
        {
            if (keyboard == null) return;
            int next = scrollSpeed.Tenths;
            if (keyboard.f9Key.wasPressedThisFrame) next--;
            if (keyboard.f10Key.wasPressedThisFrame) next++;
            if (keyboard.f11Key.wasPressedThisFrame) next = ScrollSpeedSettings.DefaultTenths;
            SetScrollSpeed(next);
            if (keyboard.f4Key.wasPressedThisFrame) SetHitSoundEnabled(!hitSoundEnabled);
        }

        private void SetScrollSpeed(int tenths)
        {
            if (!scrollSpeed.SetTenths(tenths)) return;
            PlayerPrefs.SetInt(ScrollSpeedSettings.PreferenceKey, scrollSpeed.Tenths);
            PlayerPrefs.Save();
            view.UpdateDemoControls(scrollSpeed, hitSoundEnabled);
        }

        private void SetHitSoundEnabled(bool enabled)
        {
            if (hitSoundEnabled == enabled) return;
            hitSoundEnabled = enabled;
            if (!enabled) hitSound.Stop();
            PlayerPrefs.SetInt(NoteHitSound.PreferenceKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            view.UpdateDemoControls(scrollSpeed, hitSoundEnabled);
        }

        private void Render(Keyboard keyboard)
        {
            string state = keyboard == null ? "NO KEYBOARD" : finished ? "FINISHED\nF5 then Enter to replay" :
                paused ? "PAUSED\nEnter to resume" : !started ? "READY\nClick Game view, then Enter" :
                clock.Seconds < 0 ? "COUNTDOWN " + Math.Ceiling(-clock.Seconds) : "PLAYING";
            double audioSeconds = clock.Seconds;
            view.Render(session, noteTiming.ChartSeconds(audioSeconds), scrollSpeed.TravelSeconds(travelSeconds), heldLanes, state,
                ClicksEnabled, audioSeconds, noteTiming, song.HasClip, chart.audio.chartZeroAtAudioSeconds);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && started && !paused && !finished)
            {
                clock.Pause();
                metronome.Stop();
                song.Stop();
                hitSound.Stop();
                paused = true;
            }
        }

        private void OnDisable() { OnApplicationFocus(false); }
        private void OnDestroy() { if (hitSound != null) hitSound.Dispose(); if (song != null) song.Dispose(); if (metronome != null) metronome.Dispose(); if (view != null) view.Dispose(); }
    }
}
