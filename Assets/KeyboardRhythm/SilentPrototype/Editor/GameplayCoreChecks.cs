using System;

namespace KeyboardRhythm.SilentPrototype.Editor
{
    // Pure C# checks of the actual core. Called by Unity and by verification/CoreChecks.csproj.
    public static class GameplayCoreChecks
    {
        private static int count;
        public static int Run()
        {
            count = 0;
            foreach (var test in new[] {
                new[] { 0.0, 1.0 }, new[] { -0.05, 1.0 }, new[] { 0.05, 1.0 },
                new[] { 0.05001, 2.0 }, new[] { -0.1, 2.0 }, new[] { 0.1, 2.0 },
                new[] { -0.15, 3.0 }, new[] { 0.15, 3.0 } })
            {
                var s = Session(Note("a", "TOUCH", 2, 1, 2));
                Frame(s, 1 + test[0], new[] { 0 }, new[] { 0 });
                Check(s.Notes[0].Result == (Judge)(int)test[1], "TOUCH inclusive timing boundary " + test[0]);
            }
            var session = Session(Note("a", "TOUCH", 2, 1, 2));
            Frame(session, 0.84999, new[] { 0 }, new[] { 0 });
            Check(session.JudgementCount == 0, "Too-early press rejected");
            Frame(session, 1.15, Empty, new[] { 0 });
            Check(session.MissCount == 0, "No MISS at inclusive deadline");
            Frame(session, 1.15001, new[] { 0 }, new[] { 0 });
            Frame(session, 5, Empty, Empty);
            Check(session.MissCount == 1 && session.ResolvedCount == 1, "Late/missing note receives one MISS");
            ExpectInvalid(() => Frame(session, 4, Empty, Empty), "Non-monotonic time rejected");

            session = Session(Note("wide", "TOUCH", 2, 3, 3));
            Frame(session, 1, new[] { 1, 5, 30 }, new[] { 1, 5, 30 });
            Check(session.JudgementCount == 0, "Wrong lanes and Space rejected for TOUCH");
            Frame(session, 1.01, new[] { 4 }, new[] { 4 });
            Frame(session, 1.02, new[] { 2 }, new[] { 2 });
            Check(session.PerfectCount == 1, "Wide TOUCH and no second hit");
            session = Session(Note("left", "TOUCH", 2, 1, 2), Note("right", "TOUCH", 2, 9, 2));
            Frame(session, 1, new[] { 0, 9 }, new[] { 0, 9 });
            Check(session.PerfectCount == 2, "Simultaneous disjoint TOUCH");
            session = Session(Note("first", "TOUCH", 2, 1, 1), Note("near", "TOUCH", 2.2, 1, 1));
            Frame(session, 1.09, new[] { 0 }, new[] { 0 });
            Check(session.Notes[1].State == NoteState.Completed && session.JudgementCount == 1, "Nearest single-note candidate");

            for (int key = 0; key < 30; key++)
            {
                session = Session(Floor("f", 2));
                Frame(session, 1, new[] { key }, new[] { key });
                Check(session.JudgementCount == 0, "Physical key cannot hit FLOOR: " + key);
            }
            session = Session(Floor("f", 2));
            Frame(session, 1, new[] { 30 }, new[] { 30 });
            Check(session.PerfectCount == 1, "Space hits FLOOR");
            NoteVisualSpan span = NoteLayout.GetSpan(Floor("f", 2));
            Check(span.Left == 2.5 && span.Right == 7.5 && span.Width == 5, "FLOOR spans centers of lanes 3 and 8");
            span = NoteLayout.GetSpan(Floor("h", 2, 4));
            Check(span.Left == 2.5 && span.Right == 7.5, "FLOOR_LONG has the same visual width");
            span = NoteLayout.GetSpan(Note("n", "LONG", 2, 3, 3, 4));
            Check(span.Left == 2 && span.Right == 5, "Normal hold visual span");

            // DOUBLE requires two new, distinct physical keys, even within one lane.
            session = Session(Note("d", "DOUBLE", 2, 1, 3));
            Frame(session, 1, new[] { 0, 10 }, new[] { 0, 10 });
            Check(session.PerfectCount == 1 && session.Combo == 1, "Q+A accepted as distinct keys in one lane");
            Frame(session, 1.01, new[] { 20 }, new[] { 20 });
            Check(session.JudgementCount == 1, "Extra key after DOUBLE does not count again");
            session = Session(Note("d", "DOUBLE", 2, 1, 3));
            Frame(session, 1, new[] { 0 }, new[] { 0 });
            Frame(session, 1.01, new[] { 0 }, new[] { 0 });
            Check(session.JudgementCount == 0 && session.Notes[0].DoubleCandidateCount == 1, "Same key twice cannot satisfy DOUBLE");
            Frame(session, 1.2, Empty, Empty);
            Check(session.MissCount == 1, "One-key DOUBLE expires once");
            session = Session(Note("d", "DOUBLE", 2, 1, 3));
            Frame(session, 0.8, Empty, new[] { 0 });
            Frame(session, 1, new[] { 10 }, new[] { 0, 10 });
            Check(session.JudgementCount == 0, "Previously held key is not a DOUBLE press");
            session = Session(Note("d", "DOUBLE", 2, 1, 3));
            Frame(session, 0.95, new[] { 0 }, new[] { 0 });
            Frame(session, 1, new[] { 10 }, new[] { 0, 10 });
            Check(session.PerfectCount == 1, "Inclusive DOUBLE pair boundary 50 ms");
            session = Session(Note("d", "DOUBLE", 2, 1, 3));
            Frame(session, 0.95, new[] { 0 }, new[] { 0 });
            Frame(session, 1.00001, new[] { 10 }, new[] { 0, 10 });
            Check(session.JudgementCount == 0, "DOUBLE separation beyond 50 ms rejected");
            Frame(session, 1.01, new[] { 0 }, new[] { 0, 10 });
            Check(session.PerfectCount == 1, "Expired first key can be replaced with a fresh pair");
            session = Session(Note("d", "DOUBLE", 2, 1, 3));
            Frame(session, 0.93, new[] { 0 }, new[] { 0 });
            Frame(session, 0.97, new[] { 10 }, new[] { 0, 10 });
            Check(session.GreatCount == 1, "DOUBLE uses worse of two timing results");
            session = Session(Note("d", "DOUBLE", 2, 1, 3));
            Frame(session, 0.85, new[] { 0 }, new[] { 0 });
            Frame(session, 0.9, new[] { 10 }, new[] { 0, 10 });
            Check(session.GoodCount == 1, "Each DOUBLE key must be in note window");

            // LONG starts with a fresh press and follows area occupancy, not a fixed key.
            session = Session(Note("h", "LONG", 2, 3, 3, 4));
            Frame(session, 0.7, new[] { 12 }, new[] { 12 });
            Frame(session, 1, Empty, new[] { 12 });
            Frame(session, 1.2, Empty, new[] { 12 });
            Frame(session, 1.3, new[] { 14 }, new[] { 14 });
            Check(session.MissCount == 1 && session.ActiveHoldCount == 0, "Held-before-start and missed-start late entry rejected");
            session = Session(Note("h", "LONG", 2, 3, 3, 4));
            Frame(session, 1, new[] { 12 }, new[] { 12 });
            Frame(session, 1.1, new[] { 14 }, new[] { 14 });
            Frame(session, 3, Empty, Empty);
            Check(session.Notes[0].Result == Judge.Perfect && session.PerfectCount == 8 && session.Combo == 8,
                "LONG swap, catch-up ticks, end release allowed, end tick excluded");
            Check(session.ResolvedCount == 1 && session.ActiveHoldCount == 0, "LONG completion counted separately from eight judgements");

            // Start timing remains GREAT/GOOD; every maintained tick is independently PERFECT.
            foreach (string type in new[] { "LONG", "FLOOR_LONG" })
                foreach (double offset in new[] { -0.15, -0.1, 0.1, 0.15 })
                {
                    int key = type == "LONG" ? 12 : 30;
                    Judge start = Math.Abs(offset) <= 0.1 ? Judge.Great : Judge.Good;
                    session = Session(type == "LONG" ? Note("h", type, 2, 3, 3, 4) : Floor("h", 2, 4));
                    Frame(session, 1 + offset, new[] { key }, new[] { key });
                    Check(session.LastJudgement.Kind == "START" && session.LastJudgement.Result == start &&
                        session.PerfectCount == 0 && session.JudgementCount == 1,
                        type + " preserves early/late " + start + " start: " + offset);
                    Check(session.LastJudgement.HasTimingError && Math.Abs(session.LastJudgement.ErrorSeconds - offset) < 1e-9,
                        type + " preserves start timing error: " + offset);
                    Frame(session, 1.25, Empty, new[] { key });
                    Check(session.LastJudgement.Kind == "HOLD TICK" && session.LastJudgement.Result == Judge.Perfect &&
                        !session.LastJudgement.HasTimingError && session.PerfectCount == 1 && session.Combo == 2,
                        type + " first held tick is PERFECT after " + start + " start: " + offset);
                    Frame(session, 3, Empty, Empty);
                    Check(session.PerfectCount == 7 && session.GreatCount == (start == Judge.Great ? 1 : 0) &&
                        session.GoodCount == (start == Judge.Good ? 1 : 0) && session.MissCount == 0 &&
                        session.JudgementCount == 8 && session.Combo == 8,
                        type + " remaining ticks are PERFECT without extra start/end counts: " + offset);
                    Check(session.Notes[0].Result == start && session.ResolvedCount == 1 && session.ActiveHoldCount == 0,
                        type + " keeps overall start quality and completes normally: " + offset);
                }

            session = Session(Note("h", "LONG", 2, 3, 3, 4));
            Frame(session, 1, new[] { 12 }, new[] { 12 });
            Frame(session, 1.05, Empty, Empty);
            Frame(session, 1.0666667, new[] { 14 }, new[] { 14 });
            Check(session.LastJudgement.Kind == "SHORT GAP" && session.LastJudgement.Result == Judge.Great &&
                session.Notes[0].Result == Judge.Great && session.JudgementCount == 1,
                "Short gap retains quality feedback without an extra judgement count");
            Frame(session, 1.25, Empty, new[] { 14 });
            Check(session.PerfectCount == 2 && session.GreatCount == 0 && session.MissCount == 0 && session.Combo == 2 &&
                session.LastJudgement.Result == Judge.Perfect && session.Notes[0].Result == Judge.Great,
                "Short-gap feedback does not lower resumed PERFECT ticks");
            session = Session(Note("h", "LONG", 2, 3, 3, 4));
            Frame(session, 1, new[] { 12 }, new[] { 12 });
            Frame(session, 1.05, Empty, Empty);
            Frame(session, 1.15, new[] { 14 }, new[] { 14 });
            Check(session.MissCount == 1 && session.Combo == 0, "Gap at 100 ms is a MISS before recovery");
            Frame(session, 1.25, Empty, new[] { 14 });
            Check(session.PerfectCount == 2 && session.GoodCount == 0 && session.Combo == 1,
                "Recovery at next tick uses PERFECT and new combo");
            session = Session(Note("h", "LONG", 2, 3, 3, 4));
            Frame(session, 1, new[] { 12 }, new[] { 12 });
            Frame(session, 1.05, Empty, Empty);
            Frame(session, 1.4, Empty, Empty);
            Frame(session, 1.7, Empty, Empty);
            Check(session.MissCount == 1 && session.Combo == 0, "Long empty episode is not repeatedly missed");
            Frame(session, 1.71, new[] { 14 }, new[] { 14 });
            Check(session.Combo == 0, "Recovery does not add an immediate combo");
            Frame(session, 1.75, Empty, new[] { 14 });
            Check(session.PerfectCount == 2 && session.GoodCount == 0 && session.Combo == 1 && session.Notes[0].Result == Judge.Miss,
                "Mid-MISS recovery works while note retains worst overall result");
            Frame(session, 1.8, Empty, Empty);
            Frame(session, 1.91, Empty, Empty);
            Check(session.MissCount == 2, "New empty episode receives its own MISS");
            session = Session(Note("h", "LONG", 2, 3, 3, 4));
            Frame(session, 1, new[] { 12 }, new[] { 12 });
            Frame(session, 2.99, Empty, Empty);
            Frame(session, 3, Empty, Empty);
            Check(session.Notes[0].Result == Judge.Great && session.MissCount == 0, "Short gap at hold tail degrades once without release judgement");

            session = Session(Floor("fh", 2, 4));
            Frame(session, 1, new[] { 12 }, new[] { 12 });
            Check(session.JudgementCount == 0, "Normal key cannot start FLOOR_LONG");
            Frame(session, 1.01, new[] { 30 }, new[] { 30 });
            Frame(session, 3, Empty, Empty);
            Check(session.PerfectCount == 8 && session.Notes[0].State == NoteState.Completed, "Space hold uses LONG ticks and completion");
            session = Session(Floor("fh", 2, 4));
            Frame(session, 1.15, new[] { 30 }, new[] { 30 });
            Frame(session, 1.25, Empty, new[] { 30 });
            Frame(session, 1.3, Empty, Empty);
            Frame(session, 1.6, Empty, Empty);
            Check(session.GoodCount == 1 && session.PerfectCount == 1 && session.MissCount == 1 && session.Combo == 0,
                "FLOOR_LONG GOOD start and empty gap retain their independent counts");
            Frame(session, 1.61, new[] { 30 }, new[] { 30 });
            Check(session.Combo == 0, "FLOOR_LONG recovery does not add an immediate combo");
            Frame(session, 1.75, Empty, new[] { 30 });
            Check(session.LastJudgement.Kind == "HOLD TICK" && session.LastJudgement.Result == Judge.Perfect &&
                session.PerfectCount == 2 && session.GoodCount == 1 && session.MissCount == 1 &&
                session.Combo == 1 && session.Notes[0].Result == Judge.Miss,
                "FLOOR_LONG resumed tick is PERFECT while previous MISS remains");
            var low = Chart(Note("low", "LONG", 0, 1, 2, 4));
            low.timing.initialBpm = 90;
            session = new RhythmSession(low, new PrototypeSettings());
            Frame(session, 0, new[] { 0 }, new[] { 0 });
            Frame(session, 4 * 60.0 / 90, Empty, new[] { 0 });
            Check(session.PerfectCount == 16, "Provisional low-BPM sixteenth-note ticks");

            // Shared input and chronological scoring across notes.
            session = Session(Note("a", "LONG", 2, 3, 3, 4), Note("b", "LONG", 2, 5, 3, 4));
            Frame(session, 1, new[] { 14 }, new[] { 14 });
            Frame(session, 1.25, Empty, new[] { 14 });
            Check(session.Combo == 4 && session.ActiveHoldCount == 2, "One G starts/holds overlapping LONGs; ticks per note");
            session = Session(Note("h", "LONG", 2, 3, 3, 4), Note("t", "TOUCH", 2, 4, 2));
            Frame(session, 1, new[] { 14 }, new[] { 14 });
            Check(session.PerfectCount == 2 && session.ActiveHoldCount == 1, "New G shared by LONG start and TOUCH");
            session = Session(Note("h", "LONG", 2, 3, 3, 4), Note("d", "DOUBLE", 2, 5, 3));
            Frame(session, 1, new[] { 14, 16 }, new[] { 14, 16 });
            Check(session.PerfectCount == 2 && session.ActiveHoldCount == 1, "G+J hits DOUBLE while G holds LONG");
            session = Session(Note("h", "LONG", 0, 1, 2, 4), Note("m", "TOUCH", 1.6, 9, 2));
            Frame(session, 0, new[] { 0 }, new[] { 0 });
            Frame(session, 1.1, Empty, new[] { 0 });
            Check(session.PerfectCount == 5 && session.MissCount == 1 && session.Combo == 1 && session.MaxCombo == 4,
                "Slow frame processes gap/deadline/ticks in global time order");
            session = Session(Note("h", "LONG", 2, 3, 3, 4));
            Frame(session, 1, new[] { 12 }, new[] { 12 });
            Frame(session, 1, Empty, new[] { 12 });
            Check(session.JudgementCount == 1, "Repeated same timestamp does not duplicate ticks");
            var fresh = Session(Note("h", "LONG", 2, 3, 3, 4));
            Check(fresh.Combo == 0 && fresh.JudgementCount == 0 && fresh.LastJudgement == null &&
                fresh.Notes[0].State == NoteState.Waiting, "Restart resets hold/DOUBLE/score state");

            ExpectInvalid(() => Session(Note("x", "TOUCH", 0, 10, 2)), "Invalid width");
            ExpectInvalid(() => Session(Note("x", "LONG", 0, 1, 2, 0)), "LONG needs positive duration");
            ExpectInvalid(() => Session(Note("x", "UNKNOWN", 0, 1, 2)), "Unknown note type rejected");
            ExpectInvalid(() => Session(Note("x", "TOUCH", 0, 1, 2), Note("x", "TOUCH", 2, 3, 2)), "Duplicate note IDs rejected");
            ExpectInvalid(() => Session(Note("x", "TOUCH", 0, 1, 3), Note("y", "DOUBLE", 0, 3, 2)), "Same-beat intersecting singles rejected");
            return count;
        }
        private static readonly int[] Empty = new int[0];
        private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); count++; }
        private static void ExpectInvalid(Action action, string label)
        { bool invalid = false; try { action(); } catch (ArgumentException) { invalid = true; } Check(invalid, label); }
        private static void Frame(RhythmSession s, double time, int[] pressed, int[] held)
        {
            var p = new bool[31]; var h = new bool[31];
            foreach (int key in pressed) p[key] = true;
            foreach (int key in held) h[key] = true;
            s.Step(time, p, h);
        }
        private static NoteData Note(string id, string type, double beat, int lane, int width, double length = 0)
        { return new NoteData { id = id, type = type, beat = beat, lane = lane, width = width, durationBeats = length }; }
        private static NoteData Floor(string id, double beat, double length = 0)
        { return Note(id, length > 0 ? "FLOOR_LONG" : "FLOOR", beat, 0, 0, length); }
        private static ChartData Chart(params NoteData[] notes)
        {
            return new ChartData { schemaVersion = 1, chartId = "core-check", title = "Core check",
                audio = new AudioData { path = "", chartZeroAtAudioSeconds = 0 },
                timing = new TimingData { initialBpm = 120 }, notes = notes, events = new EventData[0] };
        }
        private static RhythmSession Session(params NoteData[] notes) { return new RhythmSession(Chart(notes), new PrototypeSettings()); }
    }
}
