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

    // Replaceable by a scheduled DSP/audio clock later. No Time.deltaTime accumulation.
    internal sealed class SilentChartClock : IChartClock
    {
        private double origin, pausedSeconds;
        private bool running;
        public double Seconds { get { return running ? Time.realtimeSinceStartupAsDouble - origin : pausedSeconds; } }
        public void Start(double initialSeconds)
        {
            pausedSeconds = initialSeconds;
            origin = Time.realtimeSinceStartupAsDouble - initialSeconds;
            running = true;
        }
        public void Pause() { pausedSeconds = Seconds; running = false; }
        public void Resume() { origin = Time.realtimeSinceStartupAsDouble - pausedSeconds; running = true; }
    }

    [DisallowMultipleComponent]
    public sealed class SilentChartPlayer : MonoBehaviour
    {
        [SerializeField] private string chartResourcePath = "Charts/silent_demo";
        [SerializeField] private PrototypeSettings settings = new PrototypeSettings();
        private readonly bool[] newlyPressed = new bool[31];
        private readonly bool[] heldLanes = new bool[11];
        private readonly bool[] heldKeys = new bool[31];
        private readonly IChartClock clock = new SilentChartClock();
        private RhythmSession session;
        private SilentChartView view;
        private ChartData chart;
        private bool started, paused, finished;
        private double travelSeconds, leadInSeconds;

        public void SetChartResourcePath(string path) { chartResourcePath = path; }

        private void Start()
        {
            view = new SilentChartView(transform);
            LoadChart();
        }

        private void LoadChart()
        {
            started = paused = finished = false;
            session = null;
            try
            {
                settings.Validate();
                TextAsset asset = Resources.Load<TextAsset>(chartResourcePath);
                if (asset == null) throw new ArgumentException("Missing Resources/" + chartResourcePath + ".json");
                chart = JsonUtility.FromJson<ChartData>(asset.text);
                session = new RhythmSession(chart, settings);
                travelSeconds = settings.travelSeconds;
                leadInSeconds = settings.leadInSeconds;
                clock.Start(-leadInSeconds);
                clock.Pause();
                view.SetChart(chart, session);
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
            if (keyboard != null && (keyboard.f1Key.wasPressedThisFrame || keyboard.f2Key.wasPressedThisFrame))
            {
                chartResourcePath = keyboard.f2Key.wasPressedThisFrame ? "Charts/long_double_demo" : "Charts/silent_demo";
                LoadChart();
                return;
            }
            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
            {
                LoadChart(); // Recreates all runtime note states and counters.
                return;
            }
            if (session == null) return;
            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame && !finished && (!started || paused))
            {
                if (!started) { clock.Start(-leadInSeconds); started = true; }
                else if (paused) { clock.Resume(); paused = false; }
                Render(keyboard);
                return; // Do not consume game keys on the start/resume frame.
            }
            double now = clock.Seconds;
            if (started && !paused && !finished)
            {
                // All 31 physical keys are inspected, including simultaneous presses.
                // Frame polling is sufficient for this prototype; input-event timestamps come later.
                session.Step(now, newlyPressed, heldKeys);
                if (now >= session.EndSeconds)
                {
                    clock.Pause();
                    finished = true;
                }
            }
            Render(keyboard);
        }

        private void Render(Keyboard keyboard)
        {
            string state = keyboard == null ? "NO KEYBOARD" : finished ? "FINISHED - F5 then Enter to replay" :
                paused ? "PAUSED - Enter to resume" : !started ? "READY - click Game view, then Enter" :
                clock.Seconds < 0 ? "COUNTDOWN " + Math.Ceiling(-clock.Seconds) : "PLAYING";
            view.Render(session, clock.Seconds, travelSeconds, heldLanes, state);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && started && !paused && !finished)
            {
                clock.Pause();
                paused = true;
            }
        }

        private void OnDestroy() { if (view != null) view.Dispose(); }
    }
}
