// Copy to an isolated project's Assets/. Uses the unchanged production Addressables loader.
#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ProjectDM
{
    public sealed class RuntimeBootstrapRegression : MonoBehaviour
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var serialized = new SerializedObject(Object.FindFirstObjectByType<GameManager>());
            serialized.FindProperty("prototypeSandbox").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("QA production bootstrap", typeof(RuntimeBootstrapRegression));
            EditorApplication.isPlaying = true;
        }
        IEnumerator Start()
        {
            float started = Time.realtimeSinceStartup;
            var manager = Object.FindFirstObjectByType<GameManager>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            while (!(bool)typeof(GameManager).GetField("isInitialized", flags).GetValue(manager))
            {
                var loader = (ProjectDMAssetLoader)typeof(GameManager).GetField("assetLoader", flags).GetValue(manager);
                string error = loader?.Error ?? (string)typeof(GameManager).GetField("prototypeError", flags).GetValue(manager);
                if (!string.IsNullOrEmpty(error) || Time.realtimeSinceStartup - started > 60)
                { Debug.LogError("PRODUCTION_BOOTSTRAP_FAILED " + error); EditorApplication.Exit(1); yield break; }
                yield return null;
            }
            var save = (SaveService)typeof(GameManager).GetField("save", flags).GetValue(manager);
            if (!save.sandboxThisSession) { Debug.LogError("Sandbox required"); EditorApplication.Exit(1); yield break; }
            Debug.Log("PRODUCTION_BOOTSTRAP_PASS actual Main scene, production Addressables, current tables, sandbox");
            EditorApplication.Exit(0);
        }
    }
}
#endif
