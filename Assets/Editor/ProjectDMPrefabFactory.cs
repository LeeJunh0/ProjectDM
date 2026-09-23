using System;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    /// <summary>Creates the lightweight visual-shell prefabs used by the runtime object pool.</summary>
    public static class ProjectDMPrefabFactory
    {
        public const string PrefabFolder = "Assets/GameContent/Prefabs";

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
            CreatePrefab("ChestPickup", root => CreateVisual(root, false));
            EnsurePickupAnimator("ChestPickup", "ProjectDM_PickupChest");
            for (int variant = 1; variant <= 5; variant++)
            {
                string suffix = variant.ToString("00");
                CreatePrefab($"ExperiencePickup_{suffix}", root => CreateVisual(root, false));
                CreatePrefab($"CurrencyPickup_{suffix}", root => CreateVisual(root, false));
                EnsurePickupAnimator($"ExperiencePickup_{suffix}", $"ProjectDM_Experience_{suffix}");
                EnsurePickupAnimator($"CurrencyPickup_{suffix}", $"ProjectDM_Currency_{suffix}");
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

        private static void EnsurePickupAnimator(string prefabName, string controllerName)
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

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/GameContent", "Prefabs");
            }
        }
    }
}
