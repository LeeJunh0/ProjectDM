using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ProjectDM.Editor
{
    /// <summary>One-time, repeatable setup that keeps game content outside Resources and registers the bootstrap catalog.</summary>
    public static class ProjectDMAddressablesMigration
    {
        private const string ContentRoot = "Assets/GameContent";
        private const string CatalogPath = ContentRoot + "/Configuration/ProjectDMAssetCatalog.asset";
        private const string BootstrapGroupName = "Bootstrap";

        [MenuItem("Project DM/Configure Local Addressables")]
        public static void Configure()
        {
            EnsureFolder("Assets", "GameContent");
            MoveFolder("Assets/Resources/Art", ContentRoot + "/Art");
            MoveFolder("Assets/Resources/Animation", ContentRoot + "/Animation");
            MoveFolder("Assets/Resources/Tiles", ContentRoot + "/Tiles");
            EnsureFolder(ContentRoot, "Configuration");

            ProjectDMArtPipeline.RebuildGeneratedAssets();
            ProjectDMAssetCatalog catalog = CreateOrLoadCatalog();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            AddressableAssetGroup bootstrap = settings.FindGroup(BootstrapGroupName)
                ?? settings.CreateGroup(BootstrapGroupName, true, false, false, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            ConfigureCatalog(catalog);
            Register(settings, bootstrap, CatalogPath, "project-dm/catalog");
            Register(settings, bootstrap, ContentRoot + "/Art/ProjectDM_Player_16Bit_v5.png", "art/player");
            Register(settings, bootstrap, ContentRoot + "/Art/ProjectDM_Monsters_16Bit_v5.png", "art/monsters");
            Register(settings, bootstrap, ContentRoot + "/Art/ProjectDM_ExtraMonsters_16Bit_v1.png", "art/extra-monsters");
            Register(settings, bootstrap, ContentRoot + "/Art/ProjectDM_Sprites_TopDown_v2.png", "art/gameplay");
            Register(settings, bootstrap, ContentRoot + "/Art/ProjectDM_FloorTiles_v1.png", "art/floor");
            Register(settings, bootstrap, ContentRoot + "/Animation/ProjectDM_Player_Cute_v4.controller", "animation/player");
            Register(settings, bootstrap, ContentRoot + "/Animation/ProjectDM_Slime.controller", "animation/slime");
            Register(settings, bootstrap, ContentRoot + "/Animation/ProjectDM_Skeleton.controller", "animation/skeleton");
            Register(settings, bootstrap, ContentRoot + "/Animation/ProjectDM_Bolt.controller", "animation/bolt");

            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Project DM Addressables are configured. Bootstrap content now loads through ProjectDMAssetCatalog.");
        }

        private static ProjectDMAssetCatalog CreateOrLoadCatalog()
        {
            ProjectDMAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<ProjectDMAssetCatalog>(CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<ProjectDMAssetCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        private static void ConfigureCatalog(ProjectDMAssetCatalog catalog)
        {
            catalog.playerSheet = TextureReference(ContentRoot + "/Art/ProjectDM_Player_16Bit_v5.png");
            catalog.monsterSheet = TextureReference(ContentRoot + "/Art/ProjectDM_Monsters_16Bit_v5.png");
            catalog.extraMonsterSheet = TextureReference(ContentRoot + "/Art/ProjectDM_ExtraMonsters_16Bit_v1.png");
            catalog.gameplaySheet = TextureReference(ContentRoot + "/Art/ProjectDM_Sprites_TopDown_v2.png");
            catalog.floorSheet = TextureReference(ContentRoot + "/Art/ProjectDM_FloorTiles_v1.png");
            catalog.playerAnimatorController = Reference(ContentRoot + "/Animation/ProjectDM_Player_Cute_v4.controller");
            catalog.slimeAnimatorController = Reference(ContentRoot + "/Animation/ProjectDM_Slime.controller");
            catalog.skeletonAnimatorController = Reference(ContentRoot + "/Animation/ProjectDM_Skeleton.controller");
            catalog.boltAnimatorController = Reference(ContentRoot + "/Animation/ProjectDM_Bolt.controller");
        }

        private static AssetReferenceTexture2D TextureReference(string assetPath)
        {
            return new AssetReferenceTexture2D(AssetDatabase.AssetPathToGUID(assetPath));
        }

        private static AssetReference Reference(string assetPath)
        {
            return new AssetReference(AssetDatabase.AssetPathToGUID(assetPath));
        }

        private static void Register(AddressableAssetSettings settings, AddressableAssetGroup group, string assetPath, string address)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                throw new System.InvalidOperationException($"Addressable source is missing: {assetPath}");
            }

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = address;
            entry.SetLabel("bootstrap", true, true, false);
        }

        private static void MoveFolder(string source, string destination)
        {
            if (!AssetDatabase.IsValidFolder(source) || AssetDatabase.IsValidFolder(destination))
            {
                return;
            }

            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
            {
                throw new System.InvalidOperationException($"Could not move '{source}' to '{destination}': {error}");
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
