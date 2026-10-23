using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectDM.Editor
{
    public static class MonsterAnimationArtSetup
    {
        public const int FrameCount = 4;
        public static string SheetPath(string biome) => $"{ProjectDMMonsterArtSetup.ArtFolder}/ProjectDM_{biome}_Walk_4Frame_BlackEyes_v5.png";
        private static readonly float[][] Intervals = { new[] { .165f, .18f, .1575f, .18f, .1275f }, new[] { .1125f, .12f, .225f, .1425f, .2025f } };

        [MenuItem("Project DM/Import Monster Walk Animations")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이를 종료한 다음 임포트하세요.");
            foreach (string biome in ProjectDMMonsterArtSetup.Biomes) Import(biome);
            ApplyInstalledArtwork();
            AssetDatabase.SaveAssets();
        }

        internal static void ApplyInstalledArtwork()
        {
            for (int biomeIndex = 0; biomeIndex < 2; biomeIndex++)
            {
                string biome = ProjectDMMonsterArtSetup.Biomes[biomeIndex], path = SheetPath(biome);
                if (!File.Exists(path)) continue;
                var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(sprite => sprite.name);
                if (frames.Count == 0) continue;
                if (frames.Count != 5 * FrameCount) throw new InvalidOperationException("20 walk frames required: " + path);
                var original = AssetDatabase.LoadAllAssetsAtPath(ProjectDMMonsterArtSetup.SheetPath(biome)).OfType<Sprite>().ToDictionary(sprite => sprite.name);
                StageDefinition stage = Resources.Load<StageDefinition>($"ProjectDM/Stages/Stage{biomeIndex + 1}_{biome}");
                for (int row = 0; row < 5; row++)
                {
                    var visual = stage.monsters[row];
                    visual.frames = Enumerable.Range(0, FrameCount).Select(frame => frames[$"Walk_{biome}_{row}_{frame}"]).ToArray();
                    visual.frameSeconds = Intervals[biomeIndex][row];
                    // Normalize artwork only; root scale, hit radius and mini-boss multipliers stay unchanged.
                    visual.visualScale = original[$"Retro_{biome}_{row}_0"].bounds.size.y / visual.frames[0].bounds.size.y;
                    visual.animatorController = null;
                    visual.mirrorToMovement = !(biome == "Meadow" && (row == 1 || row == 3));
                }
                stage.ValidateContent(); EditorUtility.SetDirty(stage);
            }
            Pixel32ArtSetup.ApplyInstalledArtwork();
        }

        private static void Import(string biome)
        {
            string path = SheetPath(biome);
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!raw.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Invalid atlas " + path);
                Color32[] pixels = raw.GetPixels32();
                int width = raw.width, height = raw.height;
                // Discover occupied rows and real transparent gutters rather than assuming that
                // generated cell spacing is perfectly uniform.
                var rows = new List<Vector2Int>(); int start = -1;
                for (int y = 0; y <= height; y++)
                {
                    int count = 0;
                    if (y < height) for (int x = 0; x < width; x++) if (pixels[y * width + x].a > 32) count++;
                    if (count > 4) { if (start < 0) start = y; }
                    else if (start >= 0) { if (y - start > 20) rows.Add(new Vector2Int(start, y)); start = -1; }
                }
                if (rows.Count != 5) throw new InvalidOperationException($"Expected 5 isolated animation rows, found {rows.Count}: {path}");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = 96; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048; importer.isReadable = false;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);
                var factories = new SpriteDataProviderFactories(); factories.Init();
                var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
                var existing = provider.GetSpriteRects().ToDictionary(rect => rect.name);
                var rects = new List<SpriteRect>();
                for (int row = 0; row < 5; row++)
                {
                    Vector2Int band = rows[4 - row];
                    int[] dividers = new int[FrameCount + 1]; dividers[FrameCount] = width;
                    for (int column = 1; column < FrameCount; column++)
                    {
                        int nominal = column * width / FrameCount; bool found = false;
                        for (int distance = 0; distance < width / (FrameCount * 2) && !found; distance++)
                            foreach (int candidate in new[] { nominal - distance, nominal + distance })
                            {
                                bool empty = true;
                                for (int y = band.x; y < band.y && empty; y++)
                                    for (int x = candidate - 4; x <= candidate + 4; x++)
                                        if (pixels[y * width + x].a > 32) { empty = false; break; }
                                if (!empty) continue;
                                dividers[column] = candidate; found = true; break;
                            }
                        if (!found) throw new InvalidOperationException($"No transparent gutter: {biome}, row {row}, column {column}");
                    }
                    // Shared vertical extent keeps feet/bob motion intact. Anchor the body's
                    // horizontal pixel mass, so a long tail or swinging chain cannot shift the torso.
                    for (int frame = 0; frame < FrameCount; frame++)
                    {
                        string name = $"Walk_{biome}_{row}_{frame}";
                        int low = dividers[frame], high = dividers[frame + 1];
                        int left = high, right = low, mass = 0;
                        int[] columns = new int[high - low];
                        for (int x = low; x < high; x++) for (int y = band.x; y < band.y; y++)
                            if (pixels[y * width + x].a > 32)
                            { left = Math.Min(left, x); right = Math.Max(right, x); columns[x - low]++; mass++; }
                        if (mass == 0) throw new InvalidOperationException("Empty pose: " + name);
                        int sum = 0, anchor = low;
                        while (anchor < high - 1 && sum + columns[anchor - low] < mass / 2) { sum += columns[anchor - low]; anchor++; }
                        left = Math.Max(low, left - 3); right = Math.Min(high - 1, right + 3);
                        rects.Add(new SpriteRect { name = name, rect = new Rect(left, band.x, right - left + 1, band.y - band.x),
                            alignment = SpriteAlignment.Custom, pivot = new Vector2((anchor + .5f - left) / (right - left + 1), .5f),
                            spriteID = existing.TryGetValue(name, out SpriteRect old) ? old.spriteID : GUID.Generate() });
                    }
                }
                provider.SetSpriteRects(rects.ToArray());
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
                provider.Apply(); importer.SaveAndReimport();
            }
            finally { Object.DestroyImmediate(raw); }
        }

        public static void RunBatch()
        {
            try
            {
                Install(); MonsterAnimationRegression.RunMenu();
                StageContentRegression.RunMenu(); ArtToneRegression.RunMenu(); UnusedSpriteCleanupRegression.RunMenu();
                MiniBossRegression.RunMenu(); CharacterRegression.RunMenu();
                MonsterAnimationRegression.RenderPreview();
                File.WriteAllText("Logs/MonsterAnimation-result.txt", "PASS: 40 rough-pixel walk frames, playback timing, pooled actors, stage resources and existing combat regressions.\n");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/MonsterAnimation-result.txt", "FAIL: " + error);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}
