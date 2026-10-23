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
    /// <summary>Imports retro replacements while preserving stage, progression and legacy atlas identities.</summary>
    public static class ProjectDMMonsterArtSetup
    {
        public const string ArtFolder = "Assets/GameContent/Art/Monsters";
        public static readonly string[] Biomes = { "Meadow", "Dungeon" };
        public static string SheetPath(string biome) => biome == "Dungeon"
            ? $"{ArtFolder}/ProjectDM_DungeonDenizens_Retro_2Frame_v1.png"
            : $"{ArtFolder}/ProjectDM_Meadow_Retro_2Frame_v1.png";

        [MenuItem("Project DM/Import Retro Monster Artwork")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이를 종료한 다음 임포트하세요.");
            var bossHeights = Resources.LoadAll<StageDefinition>("ProjectDM/Stages").ToDictionary(stage => stage.stageId,
                stage => Pixel32ArtSetup.Original(stage.bossFrames[0]).bounds.size.y * stage.bossScale);
            foreach (string biome in Biomes) Import(biome);
            ApplyInstalledArtwork(bossHeights);
            AssetDatabase.SaveAssets();
            Debug.Log("Retro monster artwork and installed walk cycles applied.");
        }

        // The existing stage rebuild also calls this, so rebuilding tiles/content cannot revert the art.
        internal static void ApplyInstalledArtwork(Dictionary<int, float> bossHeights = null)
        {
            foreach (string biome in Biomes)
            {
                string path = SheetPath(biome);
                if (!File.Exists(path)) continue;
                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                if (sprites.Length == 0) continue; // First install has not sliced this source yet.
                if (sprites.Length != 12) throw new InvalidOperationException("몬스터 12프레임이 필요합니다: " + path);
                StageDefinition stage = Resources.Load<StageDefinition>($"ProjectDM/Stages/Stage{(biome == "Meadow" ? 1 : 2)}_{biome}");
                if (stage == null) throw new InvalidOperationException("스테이지 설정이 없습니다: " + biome);
                float bossHeight = bossHeights != null && bossHeights.TryGetValue(stage.stageId, out float beforeImport)
                    ? beforeImport : Pixel32ArtSetup.Original(stage.bossFrames[0]).bounds.size.y * stage.bossScale;
                var map = sprites.ToDictionary(sprite => sprite.name);
                for (int row = 0; row < 5; row++)
                {
                    stage.monsters[row].frames = Frames(map, biome, row);
                    // One animation owner: the existing two-frame manual playback, including pooled enemies.
                    stage.monsters[row].animatorController = null;
                    stage.monsters[row].visualScale = 1;
                    stage.monsters[row].frameSeconds = .16f;
                }
                if (biome == "Dungeon")
                {
                    string[] names = { "동굴 박쥐", "지하 쥐", "사슬 유령", "돌거미", "망령 갑옷" };
                    for (int row = 0; row < names.Length; row++) stage.monsters[row].displayName = names[row];
                    stage.bossName = "봉인 수문장";
                }
                stage.bossFrames = Frames(map, biome, 5);
                stage.bossScale = bossHeight / stage.bossFrames[0].bounds.size.y;
                stage.ValidateContent();
                EditorUtility.SetDirty(stage);
            }
            MonsterAnimationArtSetup.ApplyInstalledArtwork();
        }

        private static Sprite[] Frames(Dictionary<string, Sprite> sprites, string biome, int row) =>
            new[] { sprites[$"Retro_{biome}_{row}_0"], sprites[$"Retro_{biome}_{row}_1"] };

        private static void Import(string biome)
        {
            string path = SheetPath(biome);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Invalid PNG: " + path);
                int width = source.width, height = source.height;
                Color32[] pixels = source.GetPixels32();
                int divider = FindColumnDivider(pixels, width, height);
                int[] columnStart = { 0, divider }, columnWidth = { divider, width - divider };
                List<Vector2Int> bands = FindRowBands(pixels, width, height);

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = 96;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.isReadable = false;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);
                var factories = new SpriteDataProviderFactories(); factories.Init();
                ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                var existing = provider.GetSpriteRects().ToDictionary(rect => rect.name);
                var rects = new List<SpriteRect>();
                for (int row = 0; row < 6; row++)
                {
                    int low = bands[5 - row].x, high = bands[5 - row].y;
                    int[] left = { columnWidth[0], columnWidth[1] }, right = { -1, -1 };
                    int bottom = high, top = -1;
                    // Generated columns are not placed at the same relative x. Center each opaque
                    // frame separately inside a shared-size rectangle, so playback cannot teleport it.
                    for (int y = low; y < high; y++)
                        for (int x = 0; x < width; x++)
                            if (pixels[y * width + x].a > 16)
                            {
                                int frame = x < divider ? 0 : 1;
                                left[frame] = Mathf.Min(left[frame], x - columnStart[frame]);
                                right[frame] = Mathf.Max(right[frame], x - columnStart[frame]);
                                bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                            }
                    if (right[0] < left[0] || right[1] < left[1] || top < bottom)
                        throw new InvalidOperationException("비어 있는 몬스터: " + biome + row);
                    int frameWidth = Mathf.Max(right[0] - left[0], right[1] - left[1]) + 1;
                    for (int frame = 0; frame < 2; frame++)
                    {
                        string name = $"Retro_{biome}_{row}_{frame}";
                        float center = (left[frame] + right[frame] + 1) * .5f;
                        if (frameWidth > columnWidth[frame]) throw new InvalidOperationException("몬스터 프레임이 열보다 큽니다: " + name);
                        int frameX = Mathf.Clamp(Mathf.RoundToInt(center - frameWidth * .5f), 0, columnWidth[frame] - frameWidth);
                        rects.Add(new SpriteRect
                        {
                            name = name, rect = new Rect(columnStart[frame] + frameX, bottom, frameWidth, top - bottom + 1),
                            alignment = SpriteAlignment.Custom, pivot = new Vector2((center - frameX) / frameWidth, .5f),
                            spriteID = existing.TryGetValue(name, out SpriteRect old) ? old.spriteID : GUID.Generate()
                        });
                    }
                }
                provider.SetSpriteRects(rects.ToArray());
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(
                    rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
                provider.Apply(); importer.SaveAndReimport();
            }
            finally { Object.DestroyImmediate(source); }
        }

        private static int FindColumnDivider(Color32[] pixels, int width, int height)
        {
            // A large guardian can extend past the nominal half-width. Use an actual empty
            // gutter shared by all rows, rather than slicing through its fist or chain.
            for (int delta = 0; delta < width / 5; delta++)
                foreach (int x in new[] { width / 2 - delta, width / 2 + delta })
                {
                    bool empty = true;
                    for (int y = 0; y < height; y++) if (pixels[y * width + x].a > 16) { empty = false; break; }
                    if (empty) return x;
                }
            throw new InvalidOperationException("두 몬스터 프레임 사이의 투명한 열을 찾지 못했습니다.");
        }

        private static List<Vector2Int> FindRowBands(Color32[] pixels, int width, int height)
        {
            // Small rats and tall guardians need different row heights. Find actual occupied
            // bands instead of selecting the same nearby gutter twice from a nominal grid.
            var bands = new List<Vector2Int>();
            int start = -1;
            for (int y = 0; y <= height; y++)
            {
                int opaque = 0;
                if (y < height) for (int x = 0; x < width; x++) if (pixels[y * width + x].a > 16) opaque++;
                if (opaque > 4) { if (start < 0) start = y; }
                else if (start >= 0)
                {
                    if (y - start >= 32) bands.Add(new Vector2Int(start, y));
                    start = -1;
                }
            }
            if (bands.Count != 6) throw new InvalidOperationException("몬스터 6개 행을 찾지 못했습니다: " + bands.Count);
            return bands;
        }

        public static void RunBatch()
        {
            try
            {
                Install();
                RetroMonsterArtRegression.RunMenu();
                StageContentRegression.RunMenu();
                ArtToneRegression.RunMenu();
                UnusedSpriteCleanupRegression.RunMenu();
                MiniBossRegression.RunMenu();
                CharacterRegression.RunMenu();
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/RetroMonsterArt-result.txt", "PASS: retro monster art, stages, legacy source identities, sprite references, mini-bosses and character magic.\n");
                RetroMonsterArtRegression.Render();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/RetroMonsterArt-result.txt", "FAIL: " + error);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}
