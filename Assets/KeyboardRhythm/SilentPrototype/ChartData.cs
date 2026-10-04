using System;
using System.Collections.Generic;

namespace KeyboardRhythm.SilentPrototype
{
    // Plain JSON data. No Unity object references: usable by an external GUI editor.
    [Serializable]
    public sealed class ChartData
    {
        public int schemaVersion;
        public string chartId;
        public string title;
        public AudioData audio;
        public TimingData timing;
        public NoteData[] notes;
        public EventData[] events;
    }

    [Serializable]
    public sealed class AudioData
    {
        public string path;
        public string songId;
        public string songTitle;
        // Raw chart seconds = audio playback seconds - this value.
        public double chartZeroAtAudioSeconds;
    }

    [Serializable]
    public sealed class TimingData
    {
        public double initialBpm;
    }

    [Serializable]
    public sealed class NoteData
    {
        public string id;
        public string type;
        public double beat;
        public int lane;
        public int width;
        public double durationBeats;
    }

    [Serializable]
    public sealed class EventData
    {
        public string id;
        public double beat;
        public string tag;
        public EventParameter[] parameters;
    }

    [Serializable]
    public sealed class EventParameter
    {
        public string name;
        // string, number, boolean or json. The value is always a JSON string.
        public string type;
        public string value;
    }

    public static class ChartValidation
    {
        public static void Validate(ChartData chart)
        {
            Require(chart != null, "Chart is null.");
            Require(chart.schemaVersion == 1, "Only schemaVersion 1 is supported.");
            Require(!string.IsNullOrWhiteSpace(chart.chartId), "chartId is required.");
            Require(!string.IsNullOrWhiteSpace(chart.title), "title is required.");
            Require(chart.audio != null && chart.audio.path != null, "audio.path is required (empty for silence).");
            Require(Finite(chart.audio.chartZeroAtAudioSeconds), "Invalid audio offset.");
            Require(chart.timing != null && Finite(chart.timing.initialBpm) && chart.timing.initialBpm > 0,
                "initialBpm must be positive.");
            Require(chart.notes != null && chart.notes.Length > 0, "notes must not be empty.");
            Require(chart.events != null, "events is required (use [] when empty).");

            var noteIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (NoteData note in chart.notes)
            {
                Require(note != null && !string.IsNullOrWhiteSpace(note.id), "Every note needs an id.");
                Require(noteIds.Add(note.id), "Duplicate note id: " + note.id);
                Require(Finite(note.beat) && note.beat >= 0, "Invalid beat: " + note.id);
                Require(Finite(note.durationBeats) && note.durationBeats >= 0, "Invalid duration: " + note.id);
                bool normal = note.type == "TOUCH" || note.type == "DOUBLE" || note.type == "LONG";
                bool floor = note.type == "FLOOR" || note.type == "FLOOR_LONG";
                Require(normal || floor, "Unknown note type: " + note.type);
                if (normal)
                    Require(note.lane >= 1 && note.width >= 1 && note.width <= 10 && note.lane + note.width - 1 <= 10,
                        "Invalid lane/width: " + note.id);
                else
                    Require(note.lane == 0 && note.width == 0, "FLOOR lane/width must be 0: " + note.id);
                bool hold = note.type == "LONG" || note.type == "FLOOR_LONG";
                Require(hold ? note.durationBeats > 0 : note.durationBeats == 0,
                    "durationBeats must be positive for holds, 0 for single notes: " + note.id);
                Require(Finite(note.beat * 60.0 / chart.timing.initialBpm), "Note time overflow: " + note.id);
                Require(Finite((note.beat + note.durationBeats) * 60.0 / chart.timing.initialBpm), "Note end time overflow: " + note.id);
            }

            // Only simultaneous intersecting single notes are rejected here.
            // Wider temporal overlap rules remain undecided (SPEC U09).
            for (int i = 0; i < chart.notes.Length; i++)
                for (int j = i + 1; j < chart.notes.Length; j++)
                {
                    NoteData a = chart.notes[i], b = chart.notes[j];
                    bool aSingle = a.type == "TOUCH" || a.type == "DOUBLE";
                    bool bSingle = b.type == "TOUCH" || b.type == "DOUBLE";
                    if (aSingle && bSingle && a.beat == b.beat)
                        Require(a.lane + a.width <= b.lane || b.lane + b.width <= a.lane,
                            "Simultaneous single-note areas overlap: " + a.id + ", " + b.id);
                }

            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (EventData ev in chart.events)
            {
                Require(ev != null && !string.IsNullOrWhiteSpace(ev.id), "Every event needs an id.");
                Require(eventIds.Add(ev.id), "Duplicate event id: " + ev.id);
                Require(Finite(ev.beat) && ev.beat >= 0 && !string.IsNullOrWhiteSpace(ev.tag), "Invalid event: " + ev.id);
                Require(ev.parameters != null, "Event parameters required: " + ev.id);
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (EventParameter p in ev.parameters)
                {
                    Require(p != null && !string.IsNullOrWhiteSpace(p.name) && names.Add(p.name) && p.value != null,
                        "Invalid/duplicate event parameter: " + ev.id);
                    Require(p.type == "string" || p.type == "number" || p.type == "boolean" || p.type == "json",
                        "Unknown parameter type: " + ev.id);
                    // Payload values are opaque here; handlers/editors validate their own payloads later.
                }
            }
        }

        public static void ValidateSilentPlayer(ChartData chart)
        {
            Validate(chart);
            // All five known note types are playable; event execution is still disabled.
        }

        private static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
        private static void Require(bool ok, string message) { if (!ok) throw new ArgumentException(message); }
    }
}
