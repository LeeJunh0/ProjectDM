using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    /// <summary>Idempotent VFX installation; does not rebuild atlas slices or touch scenes.</summary>
    public static class ProjectDMPickupEffectsSetup
    {
        private const string MaterialPath = "Assets/GameContent/Art/Skills/PickupMote.mat";
        private static readonly Color[] ExperienceColors =
        {
            new(.35f,.6f,.8f), new(.32f,.7f,.7f), new(.62f,.43f,.75f),
            new(.35f,.67f,.43f), new(.8f,.64f,.32f)
        };
        private static readonly Color[] CurrencyColors =
        {
            new(.72f,.46f,.28f), new(.63f,.7f,.77f), new(.83f,.66f,.32f),
            new(.3f,.64f,.45f), new(.61f,.43f,.74f)
        };

        [MenuItem("Project DM/Install Quiet Pickup Effects")]
        public static void Install()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("ProjectDM/Quiet Pickup Mote");
                if (shader == null) throw new System.InvalidOperationException("Pickup mote shader is missing.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            for (int grade = 1; grade <= 5; grade++)
            {
                Configure($"ExperiencePickup_{grade:00}", ExperienceColors[grade - 1], material);
                Configure($"CurrencyPickup_{grade:00}", CurrencyColors[grade - 1], material);
            }
            Configure("ManaPickup", new Color(.32f, .57f, .76f), material);
            Configure("ChestPickup", new Color(.77f, .59f, .31f), material);
            AssetDatabase.SaveAssets();
            Debug.Log("Quiet rising motes installed on all 12 pickup prefabs.");
        }

        private static void Configure(string name, Color color, Material material)
        {
            string path = $"{ProjectDMPrefabFactory.PrefabFolder}/{name}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform visual = root.transform.Find("Visual");
                if (visual == null) throw new System.InvalidOperationException("Missing pickup visual: " + name);
                // Give the closed chest an authored first frame before its Animator starts.
                // This also lets the effect use the real sprite pivot/bounds on its first pooled spawn.
                SpriteRenderer body = visual.GetComponent<SpriteRenderer>();
                if (name == "ChestPickup" && body != null && body.sprite == null)
                    foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/GameContent/Art/ProjectDM_Pickups_4Frame_v1.png"))
                        if (asset is Sprite sprite && sprite.name == "Pickup_TreasureChest_0") { body.sprite = sprite; break; }
                Transform existing = visual.Find("RisingMotes");
                GameObject instance = existing != null ? existing.gameObject : new GameObject("RisingMotes");
                instance.transform.SetParent(visual, false);
                PickupAmbientVfx effect = instance.GetComponent<PickupAmbientVfx>() ?? instance.AddComponent<PickupAmbientVfx>();
                effect.particleMaterial = material;
                effect.tint = color;
                effect.Configure();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void RunBatch()
        {
            try
            {
                Install();
                PickupEffectsRegression.RunMenu();
                ManaPickupRegression.RunMenu();
                LootGradeQa.RunMenu();
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }
}
