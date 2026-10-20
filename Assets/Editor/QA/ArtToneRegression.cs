using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    /// <summary>Protects fixed atlas dimensions and existing Unity identities during palette edits.</summary>
    public static class ArtToneRegression
    {
        [Serializable] private sealed class Manifest { public Entry[] assets; }
        [Serializable] private sealed class Entry
        {
            public string path, metaSha256;
            public int width, height;
        }

        private static int checks;
        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            checks++;
        }

        [MenuItem("Project DM/검증/전체 아트 톤·슬라이스 검증")]
        public static void RunMenu()
        {
            checks = 0;
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("Docs/Art/SoftTone-assets.json"));
            Check(manifest?.assets?.Length == 16, "Character, monster, spell and pickup inventory contains all 16 textures");
            foreach (Entry entry in manifest.assets)
            {
                byte[] bytes = File.ReadAllBytes(entry.path);
                Check(bytes.Length > 24 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71,
                    "Valid PNG source: " + entry.path);
                int width = ReadBigEndian(bytes, 16), height = ReadBigEndian(bytes, 20);
                Check(width == entry.width && height == entry.height, "Palette edits preserve exact source canvas: " + entry.path);
                using (SHA256 hash = SHA256.Create())
                {
                    string actual = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(entry.path + ".meta"))).Replace("-", "");
                    Check(string.Equals(actual, entry.metaSha256, StringComparison.OrdinalIgnoreCase),
                        "GUIDs, slice rectangles, pivots, PPU and import settings remain byte-for-byte intact: " + entry.path);
                }
                TextureImporter importer = AssetImporter.GetAtPath(entry.path) as TextureImporter;
                Check(importer != null && importer.textureType == TextureImporterType.Sprite && importer.alphaIsTransparency,
                    "Edited artwork still imports as transparent sprites: " + entry.path);
                Check(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled, "Crisp pixel import: " + entry.path);
                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(entry.path).OfType<Sprite>().ToArray();
                Check(sprites.Length > 0, "Existing sprite subassets still load: " + entry.path);
                foreach (Sprite sprite in sprites)
                    Check(sprite.rect.width > 0 && sprite.rect.height > 0 && sprite.rect.xMin >= 0 && sprite.rect.yMin >= 0 &&
                        sprite.rect.xMax <= sprite.texture.width && sprite.rect.yMax <= sprite.texture.height,
                        "Existing slice stays within the edited atlas: " + sprite.name);
            }
            Debug.Log($"Art tone: {checks} assertions PASS (16 sources, exact canvas, unchanged metadata and valid sprite slices).");
        }

        private static int ReadBigEndian(byte[] bytes, int offset) =>
            bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];

        public static void RunBatch()
        {
            try
            {
                RunMenu();
                UnusedSpriteCleanupRegression.RunMenu();
                StageContentRegression.RunMenu();
                SkillVisualRegression.RunMenu();
                ManaPickupRegression.RunMenu();
                CharacterRegression.RunMenu();
                LootGradeQa.RunMenu();
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/ArtToneRegression-result.txt", $"PASS: {checks} art assertions; sprite identities, stages, skills, mana drops, personal magic and loot suites passed.\n");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/ArtToneRegression-result.txt", "FAIL: " + exception);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
