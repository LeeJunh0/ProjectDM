using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public static class ProjectDMGrowthThemeSetup
    {
        [MenuItem("Project DM/Install Character Growth Colors")]
        public static void Install()
        {
            const string path = "Assets/Resources/ProjectDM/UI/GrowthGraphTheme.asset";
            if (AssetDatabase.LoadAssetAtPath<GrowthGraphTheme>(path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GrowthGraphTheme>(), path);
            AssetDatabase.SaveAssets();
        }

        public static void RunBatch()
        {
            try
            {
                Install(); GrowthGraphThemeRegression.RunMenu(); GrowthGraphQa.RunMenu(); CharacterRegression.RunMenu();
                EditorApplication.Exit(0);
            }
            catch (System.Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
