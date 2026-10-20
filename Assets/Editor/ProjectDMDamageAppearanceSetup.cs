using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public static class ProjectDMDamageAppearanceSetup
    {
        [MenuItem("Project DM/Install Pixel Damage Appearance")]
        public static void Install()
        {
            const string path = "Assets/Resources/ProjectDM/UI/CombatDamageAppearance.asset";
            var appearance = AssetDatabase.LoadAssetAtPath<CombatDamageAppearance>(path);
            if (appearance == null)
            {
                appearance = ScriptableObject.CreateInstance<CombatDamageAppearance>(); AssetDatabase.CreateAsset(appearance, path);
            }
            appearance.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/ProjectDM/UI/Fonts/VT323-Regular.ttf");
            if (appearance.font == null) throw new System.InvalidOperationException("Dedicated pixel font missing");
            EditorUtility.SetDirty(appearance); AssetDatabase.SaveAssets();
        }

        public static void RunBatch()
        {
            try { Install(); DamageAppearanceRegression.RunMenu(); CharacterRegression.RunMenu(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
