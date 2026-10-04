using System;
using System.Collections.Generic;

namespace KeyboardRhythm.SilentPrototype
{
    public enum Judge { Pending, Perfect, Great, Good, Miss }
    public enum NoteState { Waiting, Holding, Completed, MissedStart }

    [Serializable]
    public sealed class PrototypeSettings
    {
        // Provisional timing values; confirmed hold input rules are described in SPEC.
        public double perfectFrames = 3, greatFrames = 6, goodFrames = 9;
        public double doublePairFrames = 3, longMissGapFrames = 6;
        public double holdTickBeats = 0.5, lowBpmHoldTickBeats = 0.25, lowBpmThreshold = 120;
        public double leadInSeconds = 3, travelSeconds = 2, resultTailSeconds = 1;
        public double PerfectSeconds { get { return perfectFrames / 60.0; } }
        public double GreatSeconds { get { return greatFrames / 60.0; } }
        public double GoodSeconds { get { return goodFrames / 60.0; } }
        public double DoublePairSeconds { get { return doublePairFrames / 60.0; } }
        public double LongMissGapSeconds { get { return longMissGapFrames / 60.0; } }
        public PrototypeSettings Copy() { return (PrototypeSettings)MemberwiseClone(); }
        public void Validate()
        {
            foreach (double value in new[] { perfectFrames, greatFrames, goodFrames, doublePairFrames,
                longMissGapFrames, holdTickBeats, lowBpmHoldTickBeats, lowBpmThreshold,
                leadInSeconds, travelSeconds, resultTailSeconds })
                if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("Non-finite setting.");
            if (perfectFrames < 0 || greatFrames < perfectFrames || goodFrames < greatFrames ||
                doublePairFrames <= 0 || longMissGapFrames <= 0 || holdTickBeats <= 0 ||
                lowBpmHoldTickBeats <= 0 || lowBpmThreshold <= 0 || leadInSeconds < 0 ||
                travelSeconds <= 0 || resultTailSeconds < 0)
                throw new ArgumentException("Invalid prototype settings.");
        }
    }

    public sealed class JudgementEvent
    {
        public readonly string NoteId, Kind;
        public readonly Judge Result;
        public readonly double TimeSeconds, ErrorSeconds;
        public readonly bool HasTimingError;
        internal JudgementEvent(RuntimeNote note, string kind, Judge result, double time, double error, bool timed)
        { NoteId = note.Data.id; Kind = kind; Result = result; TimeSeconds = time; ErrorSeconds = error; HasTimingError = timed; }
    }

    public sealed class RuntimeNote
    {
        public NoteData Data { get; private set; }
        public double TimeSeconds { get; private set; }
        public double EndTimeSeconds { get; private set; }
        public bool IsHold { get { return Data.type == "LONG" || Data.type == "FLOOR_LONG"; } }
        public bool IsFloor { get { return Data.type == "FLOOR" || Data.type == "FLOOR_LONG"; } }
        public NoteState State { get; internal set; }
        public Judge Result { get; internal set; }
        public double ErrorSeconds { get; internal set; }
        public bool IsHeld { get; internal set; }
        public bool GapMissReported { get; internal set; }
        public int DoubleCandidateCount { get; internal set; }
        internal double GapStart = double.NaN, NextTick;
        internal Judge HoldGrade; // Short-gap feedback only; scheduled hold ticks are always PERFECT.
        internal readonly double[] DoublePressTimes = new double[30];
        internal readonly bool[] DoublePressValid = new bool[30];
        internal RuntimeNote(NoteData data, double bpm)
        {
            Data = data;
            TimeSeconds = data.beat * 60.0 / bpm;
            EndTimeSeconds = (data.beat + data.durationBeats) * 60.0 / bpm;
            State = NoteState.Waiting;
            Result = Judge.Pending;
        }
    }

    // Actual judgement core, independent of Unity. Physical key IDs: 0..29 rows, 30 Space.
    public sealed class RhythmSession
    {
        private const double Eps = 1e-9;
        private readonly PrototypeSettings settings;
        private readonly List<RuntimeNote> notes = new List<RuntimeNote>();
        private readonly bool[] heldKeys = new bool[31];
        private double lastTime = double.NegativeInfinity;
        private readonly double tickSeconds;
        public IReadOnlyList<RuntimeNote> Notes { get { return notes; } }
        public JudgementEvent LastJudgement { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int PerfectCount { get; private set; }
        public int GreatCount { get; private set; }
        public int GoodCount { get; private set; }
        public int MissCount { get; private set; }
        public int JudgementCount { get { return PerfectCount + GreatCount + GoodCount + MissCount; } }
        public int ResolvedCount { get { int n = 0; foreach (var note in notes) if (note.State == NoteState.Completed) n++; return n; } }
        public int ActiveHoldCount { get { int n = 0; foreach (var note in notes) if (note.State == NoteState.Holding) n++; return n; } }
        public double EndSeconds { get; private set; }

        public RhythmSession(ChartData chart, PrototypeSettings settings)
        {
            ChartValidation.ValidateSilentPlayer(chart);
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            this.settings = settings.Copy();
            double interval = chart.timing.initialBpm < settings.lowBpmThreshold ? settings.lowBpmHoldTickBeats : settings.holdTickBeats;
            tickSeconds = interval * 60.0 / chart.timing.initialBpm;
            if (!Finite(tickSeconds) || tickSeconds <= Eps) throw new ArgumentException("Hold tick interval is too small.");
            foreach (var data in chart.notes) notes.Add(new RuntimeNote(data, chart.timing.initialBpm));
            notes.Sort((a, b) => {
                int c = a.TimeSeconds.CompareTo(b.TimeSeconds);
                return c != 0 ? c : StringComparer.Ordinal.Compare(a.Data.id, b.Data.id);
            });
            double end = 0;
            foreach (var note in notes) end = Math.Max(end, note.EndTimeSeconds);
            EndSeconds = end + this.settings.GoodSeconds + this.settings.resultTailSeconds;
            if (!Finite(EndSeconds)) throw new ArgumentException("Chart end time overflow.");
        }

        // Sampling contract: the previous held state persists until this timestamp.
        // Current presses are applied at this timestamp. Equal-time ticks follow current input;
        // a hold ending exactly now completes before a release, so no end-release timing is required.
        public void Step(double chartSeconds, bool[] newlyPressed, bool[] currentlyHeld)
        {
            if (!Finite(chartSeconds) || chartSeconds < lastTime || newlyPressed == null || currentlyHeld == null ||
                newlyPressed.Length != 31 || currentlyHeld.Length != 31)
                throw new ArgumentException("Step requires monotonic time and 31-key snapshots.");
            AdvanceScheduled(chartSeconds, false);
            Array.Copy(currentlyHeld, heldKeys, 31);
            foreach (var note in notes)
                if (note.State == NoteState.Holding) ApplyHoldState(note, chartSeconds);
            for (int key = 0; key < 31; key++)
                if (newlyPressed[key]) ProcessPress(key, chartSeconds);
            AdvanceScheduled(chartSeconds, true);
            foreach (var note in notes)
                if (note.Data.type == "DOUBLE" && note.State == NoteState.Waiting) PruneDouble(note, chartSeconds);
            lastTime = chartSeconds;
        }

        private void ProcessPress(int key, double now)
        {
            // One press can start every overlapping LONG and also serve a single-note candidate.
            foreach (var note in notes)
                if (note.IsHold && InArea(note, key) && now < note.EndTimeSeconds)
                {
                    if (note.State == NoteState.Waiting && Math.Abs(now - note.TimeSeconds) <= settings.GoodSeconds + Eps)
                        BeginHold(note, now, false);
                    else if (note.State == NoteState.MissedStart)
                        BeginHold(note, now, true);
                }
            RuntimeNote best = null;
            double distance = double.MaxValue;
            foreach (var note in notes)
            {
                if (note.IsHold || note.State != NoteState.Waiting || !InArea(note, key)) continue;
                double error = Math.Abs(now - note.TimeSeconds);
                if (error <= settings.GoodSeconds + Eps && error < distance) { best = note; distance = error; }
            }
            if (best == null) return;
            if (best.Data.type == "DOUBLE") ProcessDouble(best, key, now);
            else FinishSingle(best, Grade(distance), now, now - best.TimeSeconds);
        }

        private void BeginHold(RuntimeNote note, double now, bool lateJoin)
        {
            note.State = NoteState.Holding;
            note.IsHeld = AreaHeld(note);
            note.GapStart = note.IsHeld ? double.NaN : now;
            note.GapMissReported = false;
            if (lateJoin)
            {
                // Keep START MISS. Joining adds no synthetic start judgement or retroactive ticks.
                note.HoldGrade = Judge.Perfect;
                double steps = Math.Max(1, Math.Ceiling((now - note.TimeSeconds - Eps) / tickSeconds));
                note.NextTick = note.TimeSeconds + steps * tickSeconds;
            }
            else
            {
                Judge result = Grade(Math.Abs(now - note.TimeSeconds));
                note.Result = note.HoldGrade = result;
                note.ErrorSeconds = now - note.TimeSeconds;
                // Successful start is counted once; following ticks are strictly after it.
                double steps = Math.Max(1, Math.Floor((now - note.TimeSeconds + Eps) / tickSeconds) + 1);
                note.NextTick = note.TimeSeconds + steps * tickSeconds;
                Count(note, "START", result, now, note.ErrorSeconds, true);
            }
        }

        private void ProcessDouble(RuntimeNote note, int key, double now)
        {
            PruneDouble(note, now);
            note.DoublePressTimes[key] = now;
            note.DoublePressValid[key] = true;
            double bestError = double.MaxValue, signedError = 0;
            bool found = false;
            for (int a = 0; a < 30; a++)
                for (int b = a + 1; b < 30; b++)
                {
                    if (!note.DoublePressValid[a] || !note.DoublePressValid[b] ||
                        Math.Abs(note.DoublePressTimes[a] - note.DoublePressTimes[b]) > settings.DoublePairSeconds + Eps) continue;
                    double ea = note.DoublePressTimes[a] - note.TimeSeconds;
                    double eb = note.DoublePressTimes[b] - note.TimeSeconds;
                    double worse = Math.Max(Math.Abs(ea), Math.Abs(eb));
                    if (worse < bestError)
                    { bestError = worse; signedError = Math.Abs(ea) >= Math.Abs(eb) ? ea : eb; found = true; }
                }
            if (found) FinishSingle(note, Grade(bestError), now, signedError);
            else PruneDouble(note, now);
        }

        private void PruneDouble(RuntimeNote note, double now)
        {
            note.DoubleCandidateCount = 0;
            for (int key = 0; key < 30; key++)
            {
                if (note.DoublePressValid[key] && now - note.DoublePressTimes[key] > settings.DoublePairSeconds + Eps)
                    note.DoublePressValid[key] = false;
                if (note.DoublePressValid[key]) note.DoubleCandidateCount++;
            }
        }

        private void ApplyHoldState(RuntimeNote note, double now)
        {
            bool nextHeld = AreaHeld(note);
            if (nextHeld == note.IsHeld) return;
            if (!nextHeld) { note.GapStart = now; note.GapMissReported = false; }
            else
            {
                if (note.GapMissReported) note.HoldGrade = Judge.Good;
                else if (now > note.GapStart + Eps) DegradeShortGap(note, now);
                note.GapStart = double.NaN;
                note.GapMissReported = false;
            }
            note.IsHeld = nextHeld;
        }

        // Global chronological scheduling keeps combo order correct across overlapping holds
        // and when several ticks are passed in one slow frame. No frame-count-based scoring.
        private void AdvanceScheduled(double until, bool includeEqualTicks)
        {
            while (true)
            {
                RuntimeNote selected = null;
                double selectedTime = double.PositiveInfinity;
                int selectedKind = int.MaxValue; // 0 gap, 1 missed start, 2 tick, 3 hold end
                foreach (var note in notes)
                {
                    if (note.State == NoteState.Waiting)
                    {
                        double deadline = note.IsHold ? Math.Min(note.TimeSeconds + settings.GoodSeconds, note.EndTimeSeconds) :
                            note.TimeSeconds + settings.GoodSeconds;
                        if (until > deadline + Eps || (note.IsHold && note.EndTimeSeconds <= until + Eps))
                            Choose(note, deadline, 1, ref selected, ref selectedTime, ref selectedKind);
                    }
                    else if (note.State == NoteState.Holding)
                    {
                        double gapEnd = note.GapStart + settings.LongMissGapSeconds;
                        if (!note.IsHeld && !note.GapMissReported && Finite(gapEnd) &&
                            gapEnd <= note.EndTimeSeconds + Eps && gapEnd <= until + Eps)
                            Choose(note, gapEnd, 0, ref selected, ref selectedTime, ref selectedKind);
                        bool tickDue = includeEqualTicks ? note.NextTick <= until + Eps : note.NextTick < until - Eps;
                        if (note.NextTick < note.EndTimeSeconds - Eps && tickDue)
                            Choose(note, note.NextTick, 2, ref selected, ref selectedTime, ref selectedKind);
                        if (note.EndTimeSeconds <= until + Eps)
                            Choose(note, note.EndTimeSeconds, 3, ref selected, ref selectedTime, ref selectedKind);
                    }
                    else if (note.State == NoteState.MissedStart && note.EndTimeSeconds <= until + Eps)
                        Choose(note, note.EndTimeSeconds, 3, ref selected, ref selectedTime, ref selectedKind);
                }
                if (selected == null) break;
                switch (selectedKind)
                {
                    case 0:
                        selected.GapMissReported = true;
                        selected.Result = Judge.Miss;
                        Count(selected, "HOLD GAP", Judge.Miss, selectedTime, 0, false);
                        break;
                    case 1:
                        if (selected.IsHold)
                        {
                            selected.State = NoteState.MissedStart;
                            selected.Result = Judge.Miss;
                            Count(selected, "START MISS", Judge.Miss, selectedTime, 0, false);
                        }
                        else FinishSingle(selected, Judge.Miss, selectedTime, 0);
                        break;
                    case 2:
                        // Grade the maintained hold independently of its start timing or gap feedback.
                        if (selected.IsHeld) Count(selected, "HOLD TICK", Judge.Perfect, selectedTime, 0, false);
                        selected.NextTick += tickSeconds;
                        break;
                    case 3:
                        if (selected.State == NoteState.Holding && !selected.IsHeld && !selected.GapMissReported &&
                            selectedTime > selected.GapStart + Eps)
                            DegradeShortGap(selected, selectedTime);
                        selected.State = NoteState.Completed;
                        break;
                }
            }
        }

        private static void Choose(RuntimeNote note, double time, int kind, ref RuntimeNote best, ref double bestTime, ref int bestKind)
        {
            if (time < bestTime - Eps || (Math.Abs(time - bestTime) <= Eps && kind < bestKind))
            { best = note; bestTime = time; bestKind = kind; }
        }
        private void DegradeShortGap(RuntimeNote note, double now)
        {
            note.HoldGrade = note.HoldGrade == Judge.Perfect ? Judge.Great : Judge.Good;
            if ((int)note.Result < (int)note.HoldGrade) note.Result = note.HoldGrade;
            // Quality feedback only: no extra combo or judgement count on short-gap recovery.
            LastJudgement = new JudgementEvent(note, "SHORT GAP", note.HoldGrade, now, 0, false);
        }
        private void FinishSingle(RuntimeNote note, Judge result, double now, double error)
        {
            note.State = NoteState.Completed;
            note.Result = result;
            note.ErrorSeconds = error;
            Count(note, note.IsHold ? "START MISS" : note.Data.type, result, now, error, result != Judge.Miss);
        }
        private void Count(RuntimeNote note, string kind, Judge result, double now, double error, bool timed)
        {
            LastJudgement = new JudgementEvent(note, kind, result, now, error, timed);
            switch (result) {
                case Judge.Perfect: PerfectCount++; break;
                case Judge.Great: GreatCount++; break;
                case Judge.Good: GoodCount++; break;
                case Judge.Miss: MissCount++; break;
            }
            if (result == Judge.Miss) Combo = 0;
            else { Combo++; MaxCombo = Math.Max(MaxCombo, Combo); }
        }
        private bool AreaHeld(RuntimeNote note)
        {
            for (int key = 0; key < 31; key++) if (heldKeys[key] && InArea(note, key)) return true;
            return false;
        }
        private static bool InArea(RuntimeNote note, int key)
        {
            if (note.IsFloor) return key == 30;
            if (key == 30) return false;
            int lane = key % 10 + 1;
            return lane >= note.Data.lane && lane < note.Data.lane + note.Data.width;
        }
        private Judge Grade(double error)
        {
            return error <= settings.PerfectSeconds + Eps ? Judge.Perfect :
                error <= settings.GreatSeconds + Eps ? Judge.Great : Judge.Good;
        }
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}
