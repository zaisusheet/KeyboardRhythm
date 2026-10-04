using System;

namespace KeyboardRhythm.SilentPrototype.Editor
{
    public static class DemoPresentationChecks
    {
        private static int count;
        public static int Run()
        {
            count = 0;
            Check(new ScrollSpeedSettings(int.MinValue).Tenths == 5, "speed lower bound on load");
            Check(new ScrollSpeedSettings(int.MaxValue).Tenths == 30, "speed upper bound on load");
            var speed = new ScrollSpeedSettings(10);
            Check(speed.TravelSeconds(2) == 2, "default preserves travel");
            Check(!speed.SetTenths(10), "unchanged speed");
            speed.SetTenths(20);
            Check(speed.TravelSeconds(2) == 1, "twice speed halves travel time");
            speed.SetTenths(5);
            Check(speed.TravelSeconds(2) == 4, "half speed doubles travel time");
            foreach (double invalid in new[] { 0, -1, double.NaN, double.PositiveInfinity })
            {
                bool rejected = false;
                try { speed.TravelSeconds(invalid); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "invalid base travel time");
            }

            // Use public gameplay APIs; these tests also compile across Runtime/Editor assemblies.
            foreach (int tenths in new[] { 5, 10, 30 })
                foreach (string type in new[] { "TOUCH", "DOUBLE", "LONG", "FLOOR", "FLOOR_LONG" })
                {
                    var session = Session(type);
                    int key = type.StartsWith("FLOOR") ? 30 : 0;
                    var keys = Keys(key);
                    if (type == "DOUBLE") keys[10] = true;
                    speed.SetTenths(tenths);
                    Check(speed.TravelSeconds(2) > 0, "finite travel for all note types");
                    session.Step(.7, Empty(), Empty());
                    Check(session.SuccessfulPressCount == 0, "no sound before press");
                    session.Step(1, keys, keys);
                    Check(session.SuccessfulPressCount == 1 && session.PerfectCount == 1,
                        "all speeds preserve perfect time and report one successful press");
                    session.Step(1.25, Empty(), keys);
                    Check(session.SuccessfulPressCount == 1, "held keys and hold ticks add no hit clicks");
                }
            foreach (double error in new[] { -.125, -.075, 0, .075, .125 })
            {
                var session = Session("TOUCH");
                session.Step(1 + error, Keys(0), Keys(0));
                Check(session.SuccessfulPressCount == 1, "GOOD GREAT PERFECT all produce hit feedback");
            }
            var missed = Session("TOUCH");
            missed.Step(1, Keys(9), Keys(9));
            Check(missed.SuccessfulPressCount == 0, "outside area has no hit sound");
            missed.Step(1.2, Empty(), Empty());
            Check(missed.MissCount == 1 && missed.SuccessfulPressCount == 0, "MISS has no hit sound");

            var doubleNote = Session("DOUBLE");
            doubleNote.Step(1, Keys(0), Keys(0));
            Check(doubleNote.SuccessfulPressCount == 0, "first DOUBLE key is not yet a successful hit");
            doubleNote.Step(1.01, Keys(0), Keys(0));
            Check(doubleNote.SuccessfulPressCount == 0, "same physical key cannot complete DOUBLE");
            doubleNote.Step(1.02, Keys(10), Keys(0, 10));
            Check(doubleNote.SuccessfulPressCount == 1, "second distinct key completes DOUBLE once");
            doubleNote.Step(1.03, Keys(1), Keys(1));
            Check(doubleNote.SuccessfulPressCount == 1, "completed note cannot retrigger");

            var mixedChart = Chart(new NoteData { id = "touch", type = "TOUCH", beat = 2, lane = 1, width = 2 },
                new NoteData { id = "hold", type = "LONG", beat = 1, lane = 1, width = 2, durationBeats = 4 },
                new NoteData { id = "miss", type = "TOUCH", beat = 1.6, lane = 8, width = 2 });
            var mixed = new RhythmSession(mixedChart, new PrototypeSettings());
            mixed.Step(.5, Keys(0), Keys(0));
            mixed.Step(1, Keys(1), Keys(1));
            Check(mixed.SuccessfulPressCount == 2 && mixed.LastJudgement.Kind == "HOLD TICK",
                "a same-frame hold tick cannot hide the successful touch from audio");
            var simultaneous = new RhythmSession(Chart(
                new NoteData { id = "a", type = "TOUCH", beat = 2, lane = 1, width = 2 },
                new NoteData { id = "b", type = "FLOOR", beat = 2 }), new PrototypeSettings());
            simultaneous.Step(1, Keys(0, 30), Keys(0, 30));
            Check(simultaneous.SuccessfulPressCount == 2, "all simultaneous successes are observable");
            Check(Session("TOUCH").SuccessfulPressCount == 0, "restart has no stale hit feedback");

            float[] samples = HitSoundWaveform.Create();
            Check(samples.Length == 1440 && (double)samples.Length / HitSoundWaveform.SampleRate == .03,
                "hit sound lasts only 30 ms");
            Check(samples[0] == 0 && samples[samples.Length - 1] == 0, "smooth silent waveform endpoints");
            double energy = 0;
            foreach (float sample in samples)
            {
                if (float.IsNaN(sample) || float.IsInfinity(sample) || Math.Abs(sample) > .65)
                    throw new Exception("Invalid hit sound sample");
                energy += sample * sample;
            }
            Check(energy > 0, "generated waveform is audible data");
            return count;
        }
        private static RhythmSession Session(string type) => new RhythmSession(Chart(new NoteData
        { id = "n", type = type, beat = 2, lane = type.StartsWith("FLOOR") ? 0 : 1,
            width = type.StartsWith("FLOOR") ? 0 : 2, durationBeats = type == "LONG" || type == "FLOOR_LONG" ? 4 : 0 }),
            new PrototypeSettings());
        private static ChartData Chart(params NoteData[] notes) => new ChartData
        { schemaVersion = 1, chartId = "demo-check", title = "Demo check", audio = new AudioData { path = "" },
            timing = new TimingData { initialBpm = 120 }, events = new EventData[0], notes = notes };
        private static bool[] Empty() => new bool[31];
        private static bool[] Keys(params int[] indices)
        { var keys = Empty(); foreach (int key in indices) keys[key] = true; return keys; }
        private static void Check(bool condition, string name)
        { if (!condition) throw new Exception(name); count++; }
    }
}
