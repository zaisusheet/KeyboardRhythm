using System;

namespace KeyboardRhythm.SilentPrototype.Editor
{
    // Runs the actual chart-time conversion through the real judgement core.
    public static class NoteTimingChecks
    {
        private static int count;
        public static int Run()
        {
            count = 0;
            var preference = new NoteTimingSettings(0);
            Check(preference.ChartSeconds(1) == 1, "zero offset preserves original time");
            preference.SetMilliseconds(100);
            Check(Math.Abs(preference.ChartSeconds(1) - .9) < 1e-9, "positive value delays notes");
            preference.SetMilliseconds(-100);
            Check(Math.Abs(preference.ChartSeconds(1) - 1.1) < 1e-9, "negative value advances notes");
            Check(preference.ChartSeconds(-3) < 0, "count-in retains negative chart time");
            Check(new NoteTimingSettings(int.MaxValue).SavedMilliseconds == 500, "positive bound");
            Check(new NoteTimingSettings(int.MinValue).SavedMilliseconds == -500, "negative bound");
            preference = new NoteTimingSettings(100);
            preference.BeginPlayback();
            preference.SetMilliseconds(200);
            Check(preference.SavedMilliseconds == 200 && preference.ActiveMilliseconds == 100 &&
                preference.HasPendingChange, "playing changes are queued, not applied to an active hold");
            double beforeResume = preference.ChartSeconds(10);
            preference.BeginPlayback();
            Check(preference.ChartSeconds(10) == beforeResume, "resume retains active offset and monotonic time");
            preference.ResetPlayback();
            Check(preference.ActiveMilliseconds == 200 && !preference.HasPendingChange, "restart applies pending setting");
            preference.BeginPlayback();
            preference.SetMilliseconds(-200);
            Check(Math.Abs(preference.ChartSeconds(10) - 9.8) < 1e-9, "earlier setting also waits for restart");
            preference.ResetPlayback();
            Check(Math.Abs(preference.ChartSeconds(10) - 10.2) < 1e-9, "next play applies earlier timing");
            preference.SetMilliseconds(0);
            Check(preference.ActiveMilliseconds == 0 && !preference.HasPendingChange, "reset in READY is immediate");

            foreach (int milliseconds in new[] { -500, -200, 0, 200, 500 })
            {
                var timing = new NoteTimingSettings(milliseconds);
                double shift = milliseconds / 1000.0;
                foreach (string type in new[] { "TOUCH", "DOUBLE", "LONG", "FLOOR", "FLOOR_LONG" })
                {
                    RhythmSession session = Session(type);
                    bool isHold = type == "LONG" || type == "FLOOR_LONG";
                    int key = type == "FLOOR" || type == "FLOOR_LONG" ? 30 : 0;
                    int[] keys = type == "DOUBLE" ? new[] { 0, 10 } : new[] { key };
                    timing.BeginPlayback();
                    Frame(session, timing, -3, Empty, Empty);
                    Frame(session, timing, 1 + shift, keys, keys);
                    Check(session.PerfectCount == 1 && session.Notes[0].Result == Judge.Perfect,
                        type + " perfect at shifted head: " + milliseconds);
                    Check(Math.Abs(session.LastJudgement.ErrorSeconds) < 1e-9 &&
                        Math.Abs(session.LastJudgement.TimeSeconds - 1) < 1e-9,
                        type + " judgement uses the same corrected time as drawing: " + milliseconds);
                    Check(session.Notes[0].TimeSeconds == 1 && session.Notes[0].Data.beat == 2,
                        type + " chart data and scheduled note time remain intact: " + milliseconds);
                    if (isHold)
                    {
                        Frame(session, timing, 1.25 + shift, Empty, keys);
                        Check(session.PerfectCount == 2, type + " first hold tick shifts with head");
                        Frame(session, timing, 2 + shift, Empty, Empty);
                        Check(session.PerfectCount == 4 && session.MissCount == 0 && session.ResolvedCount == 1,
                            type + " hold ticks/end shift together and release at end succeeds");
                    }
                }

                RhythmSession touch = Session("TOUCH");
                Frame(touch, timing, 1.15 + shift, Empty, Empty);
                Check(touch.MissCount == 0, "inclusive MISS deadline shifted: " + milliseconds);
                Frame(touch, timing, 1.15001 + shift, Empty, Empty);
                Check(touch.MissCount == 1, "MISS after corrected deadline: " + milliseconds);
                foreach (var sample in new[] { new[] { -.15, 3.0 }, new[] { -.1, 2.0 }, new[] { -.05, 1.0 },
                    new[] { .05, 1.0 }, new[] { .1, 2.0 }, new[] { .15, 3.0 } })
                {
                    touch = Session("TOUCH");
                    Frame(touch, timing, 1 + shift + sample[0], new[] { 0 }, new[] { 0 });
                    Check(touch.Notes[0].Result == (Judge)(int)sample[1], "judgement window width unchanged");
                }
                RhythmSession pair = Session("DOUBLE");
                Frame(pair, timing, .975 + shift, new[] { 0 }, new[] { 0 });
                Frame(pair, timing, 1.025 + shift, new[] { 10 }, new[] { 0, 10 });
                Check(pair.PerfectCount == 1, "DOUBLE pair separation unchanged by offset");
                Check(!timing.HasFinished(3.15 + Math.Max(0, shift) - .00001, 3.15),
                    "finish waits for audio and corrected note tail");
                Check(timing.HasFinished(3.15 + Math.Max(0, shift), 3.15), "corrected finish boundary");
                var grid = new MetronomeBeatGrid(120);
                Check(grid.SecondsAtBeat(2) == 1, "metronome stays on original audio beat");
            }
            return count;
        }

        private static readonly int[] Empty = new int[0];
        private static void Frame(RhythmSession session, NoteTimingSettings timing, double audioSeconds,
            int[] pressedKeys, int[] heldKeys)
        {
            var pressed = new bool[31]; var held = new bool[31];
            foreach (int key in pressedKeys) pressed[key] = true;
            foreach (int key in heldKeys) held[key] = true;
            session.Step(timing.ChartSeconds(audioSeconds), pressed, held);
        }
        private static RhythmSession Session(string type)
        {
            bool floor = type == "FLOOR" || type == "FLOOR_LONG";
            var chart = new ChartData { schemaVersion = 1, chartId = "offset-test", title = "Offset test",
                audio = new AudioData { path = "", chartZeroAtAudioSeconds = 1.25 },
                timing = new TimingData { initialBpm = 120 }, events = new EventData[0],
                notes = new[] { new NoteData { id = "n", type = type, beat = 2,
                    lane = floor ? 0 : 1, width = floor ? 0 : 2,
                    durationBeats = type == "LONG" || type == "FLOOR_LONG" ? 2 : 0 } } };
            return new RhythmSession(chart, new PrototypeSettings());
        }
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("Note timing check failed: " + name);
            count++;
        }
    }
}
