using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectDM.Editor
{
    /// <summary>Creates the project's composition-root scene without overwriting an existing Main scene.</summary>
    public static class ProjectDMMainSceneSetup
    {
        private const string SceneFolder = "Assets/Scenes";
        private const string MainScenePath = SceneFolder + "/Main.unity";

        [MenuItem("Project DM/Create or Update Main Scene")]
        public static void CreateOrUpdateMainScene()
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            Scene scene;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }

            if (Object.FindFirstObjectByType<GameManager>() == null)
            {
                new GameObject("GameManager").AddComponent<GameManager>();
            }

            EditorSceneManager.SaveScene(scene, MainScenePath);
            AddMainSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Project DM Main scene is ready with its GameManager composition root.");
        }

        private static void AddMainSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
            int index = scenes.FindIndex(scene => scene.path == MainScenePath);
            if (index >= 0)
            {
                scenes[index] = new EditorBuildSettingsScene(MainScenePath, true);
            }
            else
            {
                scenes.Insert(0, new EditorBuildSettingsScene(MainScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
