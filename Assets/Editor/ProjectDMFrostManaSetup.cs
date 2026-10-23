using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    /// <summary>Imports the dedicated art and wires pickup content without opening or saving scenes.</summary>
    public static class ProjectDMFrostManaSetup
    {
        public const string FrostPath = "Assets/GameContent/Art/Skills/ProjectDM_FrostShard_v1.png";
        public const string ManaPath = "Assets/GameContent/Art/ProjectDM_ManaPickup_v1.png";

        [MenuItem("Project DM/Install Frost and Mana Content")]
        public static void Install()
        {
            Import(FrostPath, 256);
            Import(ManaPath, 512);
            SkillVisualAssets art = Resources.Load<SkillVisualAssets>("ProjectDM/Skills/SkillVisuals");
            if (art == null) throw new InvalidOperationException("SkillVisuals 설정이 없습니다.");
            art.frostShard = AssetDatabase.LoadAssetAtPath<Sprite>(FrostPath);
            art.frostArtworkDiameter = .94f;
            EditorUtility.SetDirty(art);
            ProjectDMPrefabFactory.CreateOrUpdatePrefabs();
            ProjectDMAddressablesMigration.ConfigurePickupContent();
            Pixel32ArtSetup.ApplyInstalledArtwork();
            AssetDatabase.SaveAssets();
            Debug.Log("Frost shard artwork and pooled mana pickup content installed.");
        }

        private static void Import(string path, float pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing artwork: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            TextureImporterSettings settings = new();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        public static void RunBatch()
        {
            try
            {
                Install();
                ManaPickupRegression.RunMenu();
                SkillVisualRegression.RunMenu();
                ManaRegression.RunMenu();
                CharacterRegression.RunMenu();
                PrototypeRegression.RunMenu();
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/FrostManaRegression-result.txt", "PASS: mana pickup, frost artwork, skill visuals, mana lifecycle, character magic and prototype regression suites.\n");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/FrostManaRegression-result.txt", "FAIL: " + exception);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
