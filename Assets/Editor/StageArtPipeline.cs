using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectDM.Editor
{
    /// <summary>Imports stage atlases without touching scenes, old dungeon art or saved progression.</summary>
    [InitializeOnLoad]
    public static class StageArtPipeline
    {
        private const string Art = "Assets/GameContent/Art/";
        private const string Meadow = Art + "ProjectDM_MeadowRuleTiles_3x3_v1.png";
        private const string StageRoot = "Assets/Resources/ProjectDM/Stages";
        private const string TileRoot = "Assets/GameContent/Tiles/Meadow";
        private const string MeadowRule = StageRoot + "/MeadowGroundRuleTile.asset";
        private const string Stamp = "ProjectDM.StageArt.v1";

        static StageArtPipeline() { EditorApplication.delayCall += ImportIfMissing; }

        private static void ImportIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Meadow) ||
                !File.Exists(Art + "ProjectDM_MeadowMonsters_2Frame_v1.png") || !File.Exists(Art + "ProjectDM_DungeonMonsters_2Frame_v1.png")) return;
            if (AssetDatabase.LoadAssetAtPath<StageDefinition>(StageRoot + "/Stage1_Meadow.asset") == null ||
                AssetDatabase.LoadAssetAtPath<StageDefinition>(StageRoot + "/Stage2_Dungeon.asset") == null) ImportContent();
        }

        [MenuItem("Project DM/Import Stage Content")]
        public static void ImportContent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이를 종료한 다음 임포트하세요.");
            Folder(StageRoot); Folder(TileRoot);
            ImportTiles();
            ImportMonsters("Meadow"); ImportMonsters("Dungeon");
            ImportExistingMonsters();
            ConfigureStage(1, "초원", new[] { "풀 슬라임", "새싹 정령", "들판 고블린", "버섯 요정", "멧돼지" }, "고목 수호자", "Meadow",
                AssetDatabase.LoadAssetAtPath<DungeonGroundRuleTile>(MeadowRule), new Color(.22f, .17f, .1f));
            ConfigureStage(2, "석재 던전", new[] { "슬라임", "스켈레톤", "고블린", "버섯", "멧돼지" }, "골석 수호자", "Dungeon",
                AssetDatabase.LoadAssetAtPath<DungeonGroundRuleTile>("Assets/GameContent/Tiles/DungeonGroundRuleTile.asset"), new Color(.025f, .018f, .07f));
            ProjectDMMonsterArtSetup.ApplyInstalledArtwork();
            AssetDatabase.SaveAssets();
        }

        public static void RunBatch()
        {
            try { ImportContent(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static void ImportTiles()
        {
            TextureImporter importer = Prepare(Meadow, false);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Meadow);
            float side = texture.width / 3f;
            if (texture.width != texture.height) throw new InvalidOperationException("초원 타일은 정사각형 3×3이어야 합니다.");
            List<SpriteRect> rects = new();
            // Exclude the atlas's faint drawn cell-divider pixels. The cropped PPU still makes
            // each sprite exactly one cell wide, so this removes seams without transparent gaps.
            const float trim = 4f;
            for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++)
                rects.Add(Rect($"Meadow_{row}_{col}", new Rect(col * side + trim, texture.height - (row + 1) * side + trim, side - trim * 2, side - trim * 2)));
            importer.spritePixelsPerUnit = side - trim * 2;
            SetRects(importer, rects);
            Dictionary<string, Sprite> sprites = SpriteMap(Meadow);
            TileBase[] tiles = new TileBase[9];
            for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++)
            {
                string name = $"Meadow_{row}_{col}";
                Tile tile = Asset<Tile>($"{TileRoot}/{name}.asset");
                tile.sprite = sprites[name]; tile.color = Color.white;
                tile.colliderType = Tile.ColliderType.None; tile.transform = Matrix4x4.identity;
                tiles[row * 3 + col] = tile; EditorUtility.SetDirty(tile);
            }
            DungeonGroundRuleTile rule = Asset<DungeonGroundRuleTile>(MeadowRule);
            rule.ConfigureRuleTilePalette(tiles); EditorUtility.SetDirty(rule);
        }

        private static void ImportMonsters(string biome)
        {
            string path = Art + $"ProjectDM_{biome}Monsters_2Frame_v1.png";
            if (HasConfiguredSprites(path, biome == "Dungeon" ? 2 : 12)) return;
            TextureImporter importer = Prepare(path, true);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Color32[] pixels = texture.GetPixels32();
            int width = texture.width, height = texture.height, cellWidth = width / 2;
            int[] bounds = new int[7]; bounds[0] = 0; bounds[6] = height;
            // Generated animation rows can drift slightly from a strict export grid. Find the nearest
            // transparent separator rather than clipping a head into the preceding sprite.
            for (int i = 1; i < 6; i++)
            {
                int ideal = height * i / 6, radius = height / 12, best = -1;
                for (int d = 0; d <= radius && best < 0; d++) foreach (int y in new[] { ideal - d, ideal + d })
                {
                    bool empty = true;
                    for (int x = 0; x < width; x++) if (pixels[y * width + x].a > 16) { empty = false; break; }
                    if (empty) { best = y; break; }
                }
                if (best < 0) throw new InvalidOperationException($"{biome}: 몬스터 행 {i}의 투명 구분을 찾지 못했습니다.");
                bounds[i] = best;
            }
            List<SpriteRect> rects = new();
            // Stage 2 uses the original monster assets. Only its boss row is still needed here.
            for (int row = biome == "Dungeon" ? 5 : 0; row < 6; row++) for (int frame = 0; frame < 2; frame++)
            {
                int low = bounds[5 - row], high = bounds[6 - row];
                int minX = cellWidth, maxX = -1, minY = high, maxY = -1;
                // Use the union of both frame bounds, relative to their respective column, to avoid
                // changes in pivot/size as the animation toggles. No pixel data is modified.
                for (int y = low; y < high; y++) for (int x = 0; x < width; x++)
                    if (pixels[y * width + x].a > 16) { minX = Math.Min(minX, x % cellWidth); maxX = Math.Max(maxX, x % cellWidth); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
                if (maxX < minX || maxY < minY) throw new InvalidOperationException($"{biome}: 비어 있는 몬스터 행 {row}");
                rects.Add(Rect($"{biome}_{row}_{frame}", new Rect(frame * cellWidth + minX, minY, maxX - minX + 1, maxY - minY + 1)));
            }
            // Match the original actors' 96 PPU instead of treating an entire atlas row as one unit.
            // Dungeon's generated sheet is now only used by its existing boss preview.
            importer.spritePixelsPerUnit = biome == "Meadow" ? 96f : height / 6f;
            SetRects(importer, rects);
        }

        private static void ImportExistingMonsters()
        {
            string path = Art + "ProjectDM_ExtraMonsters_16Bit_v1.png";
            if (HasConfiguredSprites(path, 6)) return;
            TextureImporter importer = Prepare(path, true);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            float width = texture.width / 2f, height = texture.height / 3f;
            List<SpriteRect> rects = new();
            for (int row = 0; row < 3; row++) for (int frame = 0; frame < 2; frame++)
                rects.Add(Rect($"Existing_{row}_{frame}", new Rect(frame * width, texture.height - (row + 1) * height, width, height)));
            importer.spritePixelsPerUnit = 96f;
            SetRects(importer, rects);
        }

        private static bool HasConfiguredSprites(string path, int count) =>
            (AssetImporter.GetAtPath(path) as TextureImporter)?.userData == Stamp &&
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() == count;

        private static TextureImporter Prepare(string path, bool alpha)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("스테이지 이미지가 없습니다: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = alpha; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true; importer.maxTextureSize = 4096;
            importer.SaveAndReimport(); return importer;
        }

        private static SpriteRect Rect(string name, Rect rect) => new SpriteRect { name = name, rect = rect,
            alignment = SpriteAlignment.Center, pivot = new Vector2(.5f, .5f) };

        private static void SetRects(TextureImporter importer, List<SpriteRect> rects)
        {
            SpriteDataProviderFactories factories = new(); factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            Dictionary<string, GUID> old = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            foreach (SpriteRect rect in rects) rect.spriteID = old.TryGetValue(rect.name, out GUID id) ? id : GUID.Generate();
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply(); importer.userData = Stamp; importer.SaveAndReimport();
        }

        private static void ConfigureStage(int id, string name, string[] names, string boss, string biome, DungeonGroundRuleTile ground, Color background)
        {
            StageDefinition stage = Asset<StageDefinition>($"{StageRoot}/Stage{id}_{biome}.asset");
            Dictionary<string, Sprite> sprites = SpriteMap(Art + $"ProjectDM_{biome}Monsters_2Frame_v1.png");
            stage.stageId = id; stage.displayName = name; stage.ground = ground; stage.background = background;
            stage.monsters = new StageDefinition.MonsterVisual[5];
            for (int i = 0; i < 5; i++) stage.monsters[i] = new StageDefinition.MonsterVisual { displayName = names[i], frames = id == 2 ? null : Frames(sprites, biome, i) };
            if (id == 2)
            {
                ProjectDMSpriteCatalog catalog = AssetDatabase.LoadAssetAtPath<ProjectDMSpriteCatalog>("Assets/GameContent/Configuration/ProjectDMSpriteCatalog.asset");
                Dictionary<string, Sprite> existing = SpriteMap(Art + "ProjectDM_ExtraMonsters_16Bit_v1.png");
                stage.monsters[0].frames = catalog.slimeFrames;
                stage.monsters[1].frames = catalog.skeletonFrames;
                stage.monsters[0].animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/GameContent/Animation/ProjectDM_Slime.controller");
                stage.monsters[1].animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/GameContent/Animation/ProjectDM_Skeleton.controller");
                for (int i = 2; i < 5; i++) stage.monsters[i].frames = Frames(existing, "Existing", i - 2);
            }
            stage.bossName = boss; stage.bossFrames = Frames(sprites, biome, 5);
            // PPU correction is for normal actors; preserve the previously authored boss preview size.
            stage.bossScale = 1.8f * stage.bossFrames[0].pixelsPerUnit / (stage.bossFrames[0].texture.height / 6f);
            stage.ValidateContent(); EditorUtility.SetDirty(stage);
        }

        private static Sprite[] Frames(Dictionary<string, Sprite> sprites, string biome, int row) => new[] { sprites[$"{biome}_{row}_0"], sprites[$"{biome}_{row}_1"] };
        private static Dictionary<string, Sprite> SpriteMap(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        private static T Asset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
