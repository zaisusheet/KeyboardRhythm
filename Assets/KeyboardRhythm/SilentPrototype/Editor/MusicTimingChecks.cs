using System;

namespace KeyboardRhythm.SilentPrototype.Editor
{
    public static class MusicTimingChecks
    {
        private static int count;
        public static int Run()
        {
            count = 0;
            foreach (double zero in new[] { -4.0, 0, 2, 8 })
            {
                double initial = MusicTiming.InitialSeconds(3, zero);
                Check(initial <= -3 && MusicTiming.AudioSeconds(initial, zero) <= 0, "lead-in preserves whole intro");
                foreach (double raw in new[] { initial, -1.0, 0, 5 })
                {
                    double origin = 100;
                    double scheduled = MusicTiming.ScheduledStart(origin, raw, zero);
                    double seek = Math.Max(0, MusicTiming.AudioSeconds(raw, zero));
                    Check(scheduled >= origin + raw, "never schedules in the past on resume");
                    Check(Math.Abs((scheduled - origin) - (seek - zero)) < 1e-9, "audio seek and DSP schedule agree on chart time");
                }
                foreach (int offset in new[] { -200, 0, 200 })
                {
                    var timing = new NoteTimingSettings(offset);
                    var chart = new ChartData { schemaVersion = 1, chartId = "music", title = "Music test",
                        audio = new AudioData { path = "../Music/demo.wav", chartZeroAtAudioSeconds = zero },
                        timing = new TimingData { initialBpm = 120 }, events = new EventData[0],
                        notes = new[] { new NoteData { id = "n", type = "TOUCH", beat = 4, lane = 1, width = 2 } } };
                    var session = new RhythmSession(chart, new PrototypeSettings());
                    double audioAtHit = 2 + zero + offset / 1000.0;
                    var keys = new bool[31]; keys[0] = true;
                    session.Step(timing.ChartSeconds(audioAtHit - zero), keys, keys);
                    Check(session.PerfectCount == 1 && Math.Abs(session.LastJudgement.ErrorSeconds) < 1e-9,
                        "song offset plus device offset gives perfect at intended audio position");
                }
            }
            Check(MusicTiming.ResourcePath("Charts/demo", "../Music/a.wav") == "Music/a", "shared audio path");
            Check(MusicTiming.ResourcePath("Charts/song/hard", "../../Music/a.ogg") == "Music/a", "nested chart path");
            Check(MusicTiming.ResourcePath("Charts/demo", "./song.mp3") == "Charts/song", "same folder");
            Check(MusicTiming.ResourcePath("Charts/demo", "") == "", "silent legacy chart");
            foreach (string path in new[] { "../../escape.wav", "C:/music.wav", "/music.wav", "https://test/a.wav" })
            {
                bool rejected = false;
                try { MusicTiming.ResourcePath("Charts/demo", path); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "reject non-resource path");
            }
            Check(MusicTiming.TimingLabel(PlayJudgement(-.075)) == "FAST 75.0 ms", "FAST sign");
            Check(MusicTiming.TimingLabel(PlayJudgement(.125)) == "LATE 125.0 ms", "LATE sign");
            Check(MusicTiming.TimingLabel(PlayJudgement(0)) == "0.0 ms", "exact timing");
            var tick = PlayJudgement(0, holdTick: true);
            Check(tick != null && !tick.HasTimingError && tick.Result == Judge.Perfect &&
                MusicTiming.TimingLabel(tick) == "", "hold tick has no invented timing");
            var miss = PlayJudgement(.2, press: false);
            Check(miss != null && miss.Result == Judge.Miss && MusicTiming.TimingLabel(miss) == "", "miss has no press timing");
            Check(MusicTiming.TimingLabel(null) == "", "empty feedback");
            foreach (int offset in new[] { -500, 0, 500 })
            {
                var timing = new NoteTimingSettings(offset);
                double end = 60.15 + Math.Max(0, offset / 1000.0);
                Check(!timing.HasFinished(end - .001, 60.15), "short chart waits for final judgement and device offset");
                Check(timing.HasFinished(end + .001, 60.15), "short chart can finish before a longer song ends");
            }
            return count;
        }
        // Editor tests cross an assembly boundary: obtain events through the public gameplay API.
        private static JudgementEvent PlayJudgement(double error, bool holdTick = false, bool press = true)
        {
            var chart = new ChartData { schemaVersion = 1, chartId = "feedback", title = "Feedback test",
                audio = new AudioData { path = "" }, timing = new TimingData { initialBpm = 120 },
                events = new EventData[0], notes = new[] { new NoteData { id = "n",
                    type = holdTick ? "LONG" : "TOUCH", beat = 2, lane = 1, width = 2,
                    durationBeats = holdTick ? 4 : 0 } } };
            var session = new RhythmSession(chart, new PrototypeSettings());
            var keys = new bool[31]; keys[0] = press;
            session.Step(1 + error, keys, keys);
            if (holdTick) session.Step(1.25, new bool[31], keys);
            return session.LastJudgement;
        }
        private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; }
    }
}
