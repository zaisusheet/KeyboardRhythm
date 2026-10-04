using System;
using System.Collections.Generic;
using UnityEngine;

namespace KeyboardRhythm.SilentPrototype
{
    [Serializable]
    public sealed class ChartLibrary
    {
        public string[] charts;

        public static List<string> LoadPaths(string defaultPath)
        {
            var paths = new List<string>();
            TextAsset asset = Resources.Load<TextAsset>("ChartLibrary");
            if (asset != null)
            {
                var library = JsonUtility.FromJson<ChartLibrary>(asset.text);
                if (library != null && library.charts != null)
                    foreach (string path in library.charts)
                        if (!string.IsNullOrWhiteSpace(path) && !paths.Contains(path)) paths.Add(path);
            }
            if (!paths.Contains(defaultPath)) paths.Insert(0, defaultPath);
            return paths;
        }

        public static string Title(string path)
        {
            try
            {
                TextAsset asset = Resources.Load<TextAsset>(path);
                var chart = asset == null ? null : JsonUtility.FromJson<ChartData>(asset.text);
                return chart == null || string.IsNullOrWhiteSpace(chart.title) ? path : chart.title;
            }
            catch (ArgumentException) { return path; }
        }
    }
}
