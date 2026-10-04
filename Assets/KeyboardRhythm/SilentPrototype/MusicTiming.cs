using System;
using System.Collections.Generic;
using System.Globalization;

namespace KeyboardRhythm.SilentPrototype
{
    public static class MusicTiming
    {
        public static double InitialSeconds(double leadIn, double zeroAtAudio) => -Math.Max(leadIn, zeroAtAudio);
        public static double AudioSeconds(double rawChartSeconds, double zeroAtAudio) => rawChartSeconds + zeroAtAudio;
        public static double ScheduledStart(double origin, double rawChartSeconds, double zeroAtAudio) =>
            origin + Math.Max(rawChartSeconds, -zeroAtAudio);

        // JSON audio paths are relative to the chart file, inside Resources.
        public static string ResourcePath(string chartPath, string audioPath)
        {
            if (string.IsNullOrWhiteSpace(audioPath)) return "";
            string path = audioPath.Replace('\\', '/');
            if (path.StartsWith("/") || path.Contains(":")) throw new ArgumentException("Audio path must be relative to the chart JSON.");
            int slash = chartPath.LastIndexOf('/');
            var parts = new List<string>();
            foreach (string part in ((slash < 0 ? "" : chartPath.Substring(0, slash + 1)) + path).Split('/'))
            {
                if (part == "." || part == "") continue;
                if (part == "..")
                {
                    if (parts.Count == 0) throw new ArgumentException("Audio path leaves Resources.");
                    parts.RemoveAt(parts.Count - 1);
                }
                else parts.Add(part);
            }
            if (parts.Count == 0) throw new ArgumentException("Audio file path is empty.");
            string result = string.Join("/", parts);
            int dot = result.LastIndexOf('.');
            if (dot > result.LastIndexOf('/')) result = result.Substring(0, dot);
            return result;
        }

        public static string TimingLabel(JudgementEvent judgement)
        {
            if (judgement == null || !judgement.HasTimingError || judgement.Result == Judge.Miss) return "";
            double ms = judgement.ErrorSeconds * 1000;
            return (ms < 0 ? "FAST " : ms > 0 ? "LATE " : "") + Math.Abs(ms).ToString("0.0", CultureInfo.InvariantCulture) + " ms";
        }
    }
}
