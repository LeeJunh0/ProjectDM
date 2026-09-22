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
            CreatePrefab("ExperiencePickup", root => CreateVisual(root, false));
            CreatePrefab("GoldPickup", root => CreateVisual(root, false));
            CreatePrefab("ChestPickup", root => CreateVisual(root, false));
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

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/GameContent", "Prefabs");
            }
        }
    }
}
