using System;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    /// <summary>Creates the lightweight visual-shell prefabs used by the runtime object pool.</summary>
    public static class ProjectDMPrefabFactory
    {
        public const string PrefabFolder = "Assets/GameContent/Prefabs";
        private const string CollectibleVariantsSheetPath = "Assets/GameContent/Art/ProjectDM_ExperienceCurrency_5Types_4Frame_v1.png";

        public static void CreateOrUpdatePrefabs()
        {
            EnsureFolder();
            CreatePrefab("Player", root =>
            {
                CreateVisual(root, true);
                root.AddComponent<PlayerMovement>();
                root.AddComponent<PlayerAnimation>();
                root.AddComponent<Player>();
            });
            CreatePrefab("Enemy", root => CreateVisual(root, true));
            CreatePrefab("Projectile", root => CreateVisual(root, true));
            EnsureAnimator("Projectile", "ProjectDM_Bolt");
            CreatePrefab("ChestPickup", root => CreateVisual(root, false));
            EnsureAnimator("ChestPickup", "ProjectDM_PickupChest");
            EnsureChestInteractionVisual();
            for (int variant = 1; variant <= 5; variant++)
            {
                string suffix = variant.ToString("00");
                CreatePrefab($"ExperiencePickup_{suffix}", root => CreateVisual(root, false));
                CreatePrefab($"CurrencyPickup_{suffix}", root => CreateVisual(root, false));
                EnsureStaticPickupVisual($"ExperiencePickup_{suffix}", $"Experience_{suffix}_1");
                EnsureStaticPickupVisual($"CurrencyPickup_{suffix}", $"Currency_{suffix}_1");
            }
        }

        private static void CreatePrefab(string prefabName, Action<GameObject> configure)
        {
            string path = $"{PrefabFolder}/{prefabName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return;
            }

            GameObject root = new(prefabName);
            configure(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateVisual(GameObject root, bool includeAnimator)
        {
            GameObject visual = new("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<SpriteRenderer>();
            if (includeAnimator)
            {
                Animator animator = visual.AddComponent<Animator>();
                animator.applyRootMotion = false;
            }
        }

        private static void EnsureAnimator(string prefabName, string controllerName)
        {
            string path = $"{PrefabFolder}/{prefabName}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Transform visual = root.transform.Find("Visual");
            Animator animator = visual != null ? visual.GetComponent<Animator>() : null;
            bool changed = false;
            if (visual != null && animator == null)
            {
                animator = visual.gameObject.AddComponent<Animator>();
                animator.applyRootMotion = false;
                changed = true;
            }

            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"Assets/GameContent/Animation/{controllerName}.controller");
            if (animator != null && controller != null && animator.runtimeAnimatorController != controller)
            {
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void EnsureStaticPickupVisual(string prefabName, string spriteName)
        {
            string path = $"{PrefabFolder}/{prefabName}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Transform visual = root.transform.Find("Visual");
            if (visual == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            bool changed = false;
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            Sprite sprite = LoadCollectibleSprite(spriteName);
            if (renderer != null && sprite != null && renderer.sprite != sprite)
            {
                renderer.sprite = sprite;
                changed = true;
            }

            Animator animator = visual.GetComponent<Animator>();
            if (animator != null)
            {
                UnityEngine.Object.DestroyImmediate(animator);
                changed = true;
            }

            if (visual.GetComponent<PickupFloatVisual>() == null)
            {
                visual.gameObject.AddComponent<PickupFloatVisual>();
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void EnsureChestInteractionVisual()
        {
            const string path = PrefabFolder + "/ChestPickup.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Transform visual = root.transform.Find("Visual");
            if (visual != null && visual.GetComponent<PickupInteractionVisual>() == null)
            {
                visual.gameObject.AddComponent<PickupInteractionVisual>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        private static Sprite LoadCollectibleSprite(string spriteName)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(CollectibleVariantsSheetPath))
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            return null;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/GameContent", "Prefabs");
            }
        }
    }
}
