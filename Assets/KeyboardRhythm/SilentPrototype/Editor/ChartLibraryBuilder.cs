using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KeyboardRhythm.SilentPrototype.Editor
{
    public sealed class ChartLibraryBuilder : AssetPostprocessor
    {
        private static bool queued;
        [InitializeOnLoadMethod]
        private static void QueueRefresh()
        {
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += () => { queued = false; Refresh(); };
        }

        [MenuItem("Tools/KeyboardRhythm/Refresh Chart Library")]
        public static void Refresh()
        {
            const string file = "Assets/Resources/ChartLibrary.json";
            var library = new ChartLibrary { charts = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Resources/Charts" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Substring("Assets/Resources/".Length, p.Length - "Assets/Resources/".Length - 5))
                .OrderBy(p => p, StringComparer.Ordinal).ToArray() };
            string json = JsonUtility.ToJson(library, true) + "\n";
            if (File.Exists(file) && File.ReadAllText(file) == json) return;
            File.WriteAllText(file, json);
            AssetDatabase.ImportAsset(file);
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(p =>
                p.StartsWith("Assets/Resources/Charts/", StringComparison.Ordinal) && p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))) QueueRefresh();
        }
    }
}
