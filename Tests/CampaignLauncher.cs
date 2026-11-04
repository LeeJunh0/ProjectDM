// Copy to an isolated project's Assets/Editor; CampaignPlaytest.cs belongs in Assets/.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ProjectDM
{
    public static class CampaignLauncher
    {
        public static void Run()
        {
            var tables = GameTables.LoadResources(new GameSettings());
            var authored = Resources.Load<GrowthTreeSettings>(GrowthTreeSettings.ResourcePath);
            System.IO.Directory.CreateDirectory("Design");
            Editor.GrowthTreeSpreadsheet.Export("Design/GrowthTree.xlsx", authored, GrowthTreeSettings.Graphs(tables));
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var manager = Object.FindFirstObjectByType<GameManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("prototypeSandbox").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("QA bot isolated sandbox", typeof(CampaignPlaytest));
            EditorApplication.isPlaying = true;
        }
    }
}
