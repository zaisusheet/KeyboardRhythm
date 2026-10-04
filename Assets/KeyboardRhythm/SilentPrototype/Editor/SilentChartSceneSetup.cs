#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KeyboardRhythm.SilentPrototype.Editor
{
    public static class SilentChartSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/SilentChartTest.unity";

        [MenuItem("Tools/KeyboardRhythm/Open Silent Chart Test")]
        public static void OpenOrCreate()
        { OpenScene(ScenePath, "Charts/silent_demo"); }

        [MenuItem("Tools/KeyboardRhythm/Open LONG DOUBLE Test")]
        public static void OpenLongDouble()
        { OpenScene("Assets/Scenes/LongDoubleTest.unity", "Charts/long_double_demo"); }

        private static void OpenScene(string path, string resource)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before setting up the scene.");
            SilentChartChecks.Run();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SceneAsset existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (existing != null)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int players = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                    players += root.GetComponentsInChildren<SilentChartPlayer>(true).Length;
                if (players != 1)
                    throw new InvalidOperationException("Existing test scene must contain exactly one SilentChartPlayer. Found: " + players);
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            Scene created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("BackgroundCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.055f, 0.09f);
            camera.cullingMask = 0;
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var playerObject = new GameObject("SilentChartPlayer", typeof(SilentChartPlayer));
            playerObject.GetComponent<SilentChartPlayer>().SetChartResourcePath(resource);
            if (!EditorSceneManager.SaveScene(created, path))
                throw new InvalidOperationException("Could not save " + path);
            Selection.activeGameObject = playerObject;
            Debug.Log("Created " + path + ". Press Play, click Game view, then Enter. UI is generated at runtime.");
        }
    }
}
#endif
