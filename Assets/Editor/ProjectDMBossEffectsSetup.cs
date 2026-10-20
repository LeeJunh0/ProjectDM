using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public static class ProjectDMBossEffectsSetup
    {
        [MenuItem("Project DM/Install Boss Death Ripple")]
        public static void Install()
        {
            const string folder = "Assets/Resources/ProjectDM/Effects";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Resources/ProjectDM", "Effects");
            const string materialPath = folder + "/BossDeathRipple.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("ProjectDM/Boss Death Space Ripple");
                if (shader == null) throw new System.InvalidOperationException("Space ripple shader missing");
                material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath);
            }
            const string path = folder + "/BossDeathRipple.asset";
            var settings = AssetDatabase.LoadAssetAtPath<BossDeathRippleSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<BossDeathRippleSettings>(); AssetDatabase.CreateAsset(settings, path);
            }
            settings.material = material; EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        }

        public static void RunBatch()
        {
            try
            {
                Install(); MiniBossRegression.RunMenu();
                ManaPickupRegression.RunMenu(); LootGradeQa.RunMenu(); SkillVisualRegression.RunMenu();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
