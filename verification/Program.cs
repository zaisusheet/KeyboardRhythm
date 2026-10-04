using System;
using KeyboardRhythm.SilentPrototype.Editor;
using KeyboardRhythm.SilentPrototype;

internal static class Program
{
    private static int Main()
    {
        try
        {
            Console.WriteLine("Gameplay core checks passed: " + GameplayCoreChecks.Run());
            Console.WriteLine("Note timing offset checks passed: " + NoteTimingChecks.Run());
            Console.WriteLine("Music timing and feedback checks passed: " + MusicTimingChecks.Run());
            Console.WriteLine("Scroll speed and hit sound checks passed: " + DemoPresentationChecks.Run());
            foreach (double bpm in new[] { 90.0, 120.0, 180.0 })
            {
                var grid = new MetronomeBeatGrid(bpm);
                Check(Math.Abs(grid.SecondsAtBeat(4) - 240.0 / bpm) < 1e-9, "BPM timing");
                Check(grid.BeatAtOrAfter(grid.SecondsAtBeat(-4)) == -4, "negative count-in boundary");
                Check(grid.BeatAtOrAfter(grid.SecondsAtBeat(2) + .01) == 3, "resume skips elapsed beat");
                Check(grid.BeatAtOrAfter(grid.SecondsAtBeat(2)) == 2, "exact boundary has no duplicate offset");
                Check(grid.IsAccent(-4) && grid.IsAccent(0) && grid.IsAccent(4) && !grid.IsAccent(3), "bar accents");
            }
            Console.WriteLine("Metronome beat grid checks passed: 15");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Gameplay core checks FAILED: " + ex);
            return 1;
        }
    }
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
}
