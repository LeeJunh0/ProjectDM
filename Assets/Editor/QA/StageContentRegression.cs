using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace ProjectDM.Editor
{
    /// <summary>Checks imported stage resources and actual rule-tile selection without scene/save edits.</summary>
    public static class StageContentRegression
    {
        private static int checks;
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }

        [MenuItem("Project DM/검증/1·2 스테이지 리소스 검증")]
        public static void RunMenu() { Verify(); Debug.Log($"Stage content: {checks} assertions PASS."); }

        public static void RunBatch()
        {
            try
            {
                Verify(); Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/StageContentRegression-result.txt", $"PASS: {checks} assertions (two stages, retro sprites, transparent actors, opaque floor, rule-tile topology, selection boundary, dungeon ground preserved).\n");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void RunAllBatch()
        {
            try { RunMenu(); PrototypeRegression.RunMenu(); GrowthGraphQa.RunMenu(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void ReimportBatch()
        {
            try
            {
                string[] paths = { "Assets/GameContent/Art/ProjectDM_MeadowMonsters_2Frame_v1.png", "Assets/GameContent/Art/ProjectDM_ExtraMonsters_16Bit_v1.png" };
                string[] before = paths.SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>())
                    .Select(s => GlobalObjectId.GetGlobalObjectIdSlow(s).ToString()).OrderBy(s => s).ToArray();
                StageArtPipeline.ImportContent(); Verify();
                string[] after = paths.SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>())
                    .Select(s => GlobalObjectId.GetGlobalObjectIdSlow(s).ToString()).OrderBy(s => s).ToArray();
                Check(before.SequenceEqual(after), "Reimport preserves sprite IDs and all existing references");
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/MonsterReimport-result.txt", $"PASS: {checks} assertions, repeated stage import preserves retro monster bindings, 96 PPU and legacy sprite IDs.\n");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // Batch-only artwork inspection. Creates an unsaved scene and renders actual Tilemap/SpriteRenderer
        // components, not an image mockup; no progression or project scene is saved.
        public static void RenderBatch()
        {
            try
            {
                if (!Application.isBatchMode) throw new InvalidOperationException("격리 배치 프로젝트에서만 실행하세요.");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Directory.CreateDirectory("Logs");
                foreach (StageDefinition stage in Resources.LoadAll<StageDefinition>("ProjectDM/Stages"))
                {
                    GameObject grid = new GameObject("Stage render", typeof(Grid));
                    GameObject floor = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer));
                    floor.transform.SetParent(grid.transform, false);
                    floor.GetComponent<TilemapRenderer>().sortingOrder = -10;
                    DungeonFloorTilemap ground = floor.AddComponent<DungeonFloorTilemap>();
                    ground.SetGroundRuleTile(stage.ground); ground.Configure(new Vector2(25, 17));
                    for (int i = 0; i < 5; i++)
                    {
                        GameObject actor = new GameObject(stage.monsters[i].displayName, typeof(SpriteRenderer));
                        actor.transform.SetParent(grid.transform, false);
                        actor.transform.localPosition = new Vector3((i - 2) * 2, -1, 0);
                        actor.transform.localScale = Vector3.one * (i == 4 ? .82f : i == 3 ? .78f : .65f);
                        actor.GetComponent<SpriteRenderer>().sprite = stage.monsters[i].frames[0];
                        actor.GetComponent<SpriteRenderer>().sortingOrder = 3;
                    }
                    GameObject boss = new GameObject(stage.bossName, typeof(SpriteRenderer));
                    boss.transform.SetParent(grid.transform, false); boss.transform.localPosition = new Vector3(0, 2, 0);
                    boss.transform.localScale = Vector3.one * stage.bossScale;
                    boss.GetComponent<SpriteRenderer>().sprite = stage.bossFrames[0]; boss.GetComponent<SpriteRenderer>().sortingOrder = 3;
                    Camera camera = new GameObject("Render Camera").AddComponent<Camera>();
                    camera.transform.SetParent(grid.transform, false);
                    ground.TryGetWorldBounds(out Bounds floorBounds);
                    camera.transform.position = new Vector3(floorBounds.center.x, floorBounds.center.y, -10);
                    camera.orthographic = true; camera.orthographicSize = 10.5f;
                    camera.backgroundColor = stage.background; camera.clearFlags = CameraClearFlags.SolidColor;
                    RenderTexture target = new RenderTexture(1920, 1080, 24); target.Create(); camera.targetTexture = target;
                    camera.Render(); RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
                    Texture2D pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply(); RenderTexture.active = previous;
                    File.WriteAllBytes($"Logs/Stage{stage.stageId}-render.png", pixels.EncodeToPNG());
                    Object.DestroyImmediate(pixels); camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(grid);
                }
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static void Verify()
        {
            checks = 0;
            StageDefinition[] definitions = Resources.LoadAll<StageDefinition>("ProjectDM/Stages");
            Check(definitions.Length == 2, "Exactly two stage definitions");
            WorldArtAssets art = new WorldArtAssets();
            GameSettings settings = new GameSettings();
            GameObject grid = new GameObject("Stage regression", typeof(Grid));
            GameObject floorObject = new GameObject("Test floor", typeof(Tilemap), typeof(TilemapRenderer));
            floorObject.transform.SetParent(grid.transform, false);
            settings.dungeonFloor = floorObject.AddComponent<DungeonFloorTilemap>();
            settings.gameplayCamera = grid.AddComponent<Camera>();
            UnityEngine.Random.State random = UnityEngine.Random.state;
            try
            {
                StageSession session = new StageSession(settings, art); session.SetDefinitions(definitions);
                foreach (string biome in ProjectDMMonsterArtSetup.Biomes)
                {
                    StageDefinition stage = session.Definition(biome == "Meadow" ? 1 : 2);
                    string expected = ProjectDMMonsterArtSetup.SheetPath(biome);
                    Check(stage.monsters.All(monster => monster.frames.Length == MonsterAnimationArtSetup.FrameCount && monster.frames.All(sprite => AssetDatabase.GetAssetPath(Pixel32ArtSetup.Original(sprite)) == MonsterAnimationArtSetup.SheetPath(biome))),
                        "All five active monster slots use this biome's four-frame walk atlas");
                    Check(stage.monsters.All(monster => monster.animatorController == null), "Manual playback is the sole retro animation owner");
                    Check(stage.bossFrames.All(sprite => AssetDatabase.GetAssetPath(Pixel32ArtSetup.Original(sprite)) == expected), "Boss preview uses the matching retro source artwork");
                }
                Check(!session.Select(3), "Unconfigured stage cannot be selected");
                foreach (int id in new[] { 1, 2, 1, 2 })
                {
                    StageDefinition before = session.Current;
                    Check(session.Select(id) && session.Current == before, "Selecting a stage does not mutate the running biome");
                    session.ApplySelected();
                    StageDefinition stage = session.Current;
                    Check(stage.stageId == id && art.stage == stage && !session.SelectionChanged, "Next-run stage/monster artwork applied together");
                    Check(settings.dungeonFloor.HasCompletePalette && settings.dungeonFloor.TryGetWorldBounds(out _), "A 3x3-only palette builds a complete camera field");
                    Tilemap map = floorObject.GetComponent<Tilemap>();
                    Check(map.GetTile(Vector3Int.zero) == stage.ground, "Center uses this stage's actual rule asset");
                    BoundsInt bounds = map.cellBounds;
                    Sprite center = map.GetSprite(Vector3Int.zero);
                    Check(Mathf.Approximately(center.bounds.size.x, 1) && Mathf.Approximately(center.bounds.size.y, 1), "Cropped floor sprites still exactly fill a grid cell");
                    Sprite north = map.GetSprite(new Vector3Int(0, bounds.yMax - 1, 0));
                    Sprite south = map.GetSprite(new Vector3Int(0, bounds.yMin, 0));
                    Sprite west = map.GetSprite(new Vector3Int(bounds.xMin, 0, 0));
                    Sprite east = map.GetSprite(new Vector3Int(bounds.xMax - 1, 0, 0));
                    Check(center != null && north != center && south != center && west != center && east != center, "Four borders choose authored edge tiles, not a repeating corner sheet");
                    for (int i = 0; i < 5; i++)
                    {
                        bool animated = stage.monsters[i].animatorController != null;
                        Check(art.MonsterSprite((MonsterKind)i) == stage.monsters[i].frames[0] && art.MonsterAlternate((MonsterKind)i) == (animated ? null : stage.monsters[i].frames[1]), "Each renderer has exactly one animation owner");
                        Check(art.MonsterController((MonsterKind)i) == stage.monsters[i].animatorController, "Stage animation controller resolves correctly");
                        Check(stage.monsters[i].frames[0] != stage.monsters[i].frames[1], "Animation frames are separate sprite slices");
                        if (!animated) Check(stage.monsters[i].frames.All(frame => frame.bounds.size.y == stage.monsters[i].frames[0].bounds.size.y), "Manual animation frames share a stable vertical anchor");
                        float worldHeight = stage.monsters[i].frames[0].bounds.size.y * stage.monsters[i].visualScale * (i == 4 ? .82f : i == 3 ? .78f : .65f);
                        Check(worldHeight >= 1f && worldHeight <= 5f, "Normal monsters are readable world-size actors, not sub-unit miniatures");
                        Check(Mathf.Approximately(Pixel32ArtSetup.Original(stage.monsters[i].frames[0]).pixelsPerUnit, 96f), "Original monster source retains its 96 PPU convention");
                    }
                    Check(settings.gameplayCamera.backgroundColor == stage.background, "Camera background follows biome");
                    float bossHeight = stage.bossFrames[0].bounds.size.y * stage.bossScale;
                    Check(bossHeight > 1f && bossHeight < 3f, "Normal-actor PPU correction does not accidentally enlarge the separate boss preview");
                }
                settings.initialStage = 1; session.Select(1); session.ApplySelected();
                settings.initialStage = 2; session.ApplySelected();
                Check(session.Current.stageId == 2, "Changed Inspector default applies at a run boundary");
                Check(random.Equals(UnityEngine.Random.state), "Artwork selection does not perturb combat/drop RNG");
                Check(session.Definition(2).ground == AssetDatabase.LoadAssetAtPath<DungeonGroundRuleTile>("Assets/GameContent/Tiles/DungeonGroundRuleTile.asset"), "Stage 2 reuses the original dungeon asset");
                foreach (string biome in new[] { "Meadow", "Dungeon" })
                {
                    string path = ProjectDMMonsterArtSetup.SheetPath(biome);
                    Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                    Check(sprites.Length == 12, "Five active monsters and boss have two frames each");
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    Texture2D raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    raw.LoadImage(File.ReadAllBytes(path));
                    Color32[] pixels = raw.GetPixels32();
                    Object.DestroyImmediate(raw);
                    Check(pixels[0].a <= 16 && pixels.Count(p => p.a == 0) > pixels.Length / 4, "Transparent corner and empty gutters, not an opaque rectangle");
                    foreach (Sprite sprite in sprites)
                        Check(sprite.rect.xMin >= 0 && sprite.rect.yMin >= 0 && sprite.rect.xMax <= texture.width && sprite.rect.yMax <= texture.height,
                            "Slices stay within source texture");
                }
                Texture2D meadow = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameContent/Art/ProjectDM_MeadowRuleTiles_3x3_v1.png");
                Check(meadow.GetPixels32().All(p => p.a == 255), "Grass floor has no transparent holes");
                Check(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(meadow)).OfType<Sprite>().Count() == 9, "Exactly nine meadow autotiles");
                PrototypeGrowthTable growth = JsonUtility.FromJson<PrototypeGrowthTable>(Resources.Load<TextAsset>("ProjectDM/Data/PrototypeGrowth").text);
                Check(growth.goalLine.Length == 4 && growth.goalLine.Where(g => g.kind == "stage").All(g => session.Definition(g.stage) != null), "Two configured stages match the growth objective roadmap");
            }
            finally { Object.DestroyImmediate(grid); UnityEngine.Random.state = random; }
        }
    }
}
