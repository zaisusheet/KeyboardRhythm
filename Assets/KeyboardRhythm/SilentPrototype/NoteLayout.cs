using System;

namespace KeyboardRhythm.SilentPrototype
{
    public readonly struct NoteVisualSpan
    {
        // Zero-based lane units: 0 is the left edge of lane 1, 10 the right edge of lane 10.
        public readonly double Left;
        public readonly double Right;
        public double Width { get { return Right - Left; } }
        public NoteVisualSpan(double left, double right) { Left = left; Right = right; }
    }

    // Presentation geometry is separate from the note's input area.
    public static class NoteLayout
    {
        public const int FloorLeftCenterLane = 3;
        public const int FloorRightCenterLane = 8;

        public static NoteVisualSpan GetSpan(NoteData note)
        {
            if (note == null) throw new ArgumentNullException(nameof(note));
            if (note.type == "FLOOR" || note.type == "FLOOR_LONG")
                return new NoteVisualSpan(FloorLeftCenterLane - 0.5, FloorRightCenterLane - 0.5);
            return new NoteVisualSpan(note.lane - 1, note.lane - 1 + note.width);
        }
    }
}
