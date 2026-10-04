#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace KeyboardRhythm.SilentPrototype.Editor
{
    public static class SilentChartChecks
    {
        [MenuItem("Tools/KeyboardRhythm/Run Silent Chart Checks")]
        public static void Run()
        {
            try
            {
                int count = GameplayCoreChecks.Run();
                foreach (string path in new[] { "Charts/silent_demo", "Charts/long_double_demo" })
                {
                    TextAsset asset = Resources.Load<TextAsset>(path);
                    if (asset == null) throw new Exception("Missing " + path);
                    ChartData chart = JsonUtility.FromJson<ChartData>(asset.text);
                    ChartValidation.ValidateSilentPlayer(chart);
                    new RhythmSession(chart, new PrototypeSettings());
                    count++;
                }
                var tagged = new ChartData { schemaVersion = 1, chartId = "event-check", title = "Event check",
                    timing = new TimingData { initialBpm = 120 }, audio = new AudioData { path = "" },
                    notes = new[] { new NoteData { id = "h", type = "LONG", beat = 2, lane = 1, width = 2, durationBeats = 4 } },
                    events = new[] { new EventData { id = "e", beat = 3, tag = "custom.example",
                        parameters = new[] { new EventParameter { name = "payload", type = "json", value = "{\"keep\":true}" } } } } };
                var roundTrip = JsonUtility.FromJson<ChartData>(JsonUtility.ToJson(tagged));
                if (roundTrip.events[0].tag != tagged.events[0].tag || roundTrip.events[0].parameters[0].value != tagged.events[0].parameters[0].value)
                    throw new Exception("Event JSON round trip lost payload.");
                count++;
                var before = new RhythmSession(tagged, new PrototypeSettings());
                tagged.events = new EventData[0];
                var after = new RhythmSession(tagged, new PrototypeSettings());
                var pressed = new bool[31]; var held = new bool[31]; pressed[0] = held[0] = true;
                before.Step(1, pressed, held); after.Step(1, pressed, held);
                pressed[0] = false;
                before.Step(3, pressed, held); after.Step(3, pressed, held);
                if (before.EndSeconds != after.EndSeconds || before.JudgementCount != after.JudgementCount || before.Combo != after.Combo)
                    throw new Exception("Inert events affected hold timing or judgement.");
                count++;
                Debug.Log("Silent Chart checks passed: " + count + ". Includes LONG/DOUBLE. Play Mode still requires manual confirmation.");
            }
            catch (Exception ex)
            {
                Debug.LogError("Silent Chart checks FAILED: " + ex);
                throw;
            }
        }
    }
}
#endif
