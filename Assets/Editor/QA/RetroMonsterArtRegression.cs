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
    public static class RetroMonsterArtRegression
    {
        private static int checks;
        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            checks++;
        }

        [MenuItem("Project DM/검증/레트로 몬스터 아트 검증")]
        public static void RunMenu()
        {
            checks = 0;
            foreach (string biome in ProjectDMMonsterArtSetup.Biomes)
            {
                string path = ProjectDMMonsterArtSetup.SheetPath(biome);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Check(importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Multiple &&
                    importer.filterMode == FilterMode.Point && !importer.mipmapEnabled && importer.alphaIsTransparency &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed, "Transparent point-filtered uncompressed sprites: " + biome);
                var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    raw.LoadImage(File.ReadAllBytes(path));
                    Color32[] pixels = raw.GetPixels32();
                    Check(pixels.Count(pixel => pixel.a == 0) > pixels.Length / 2, "Empty gutters are truly transparent, including hidden RGB haze: " + biome);
                    var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(sprite => sprite.name);
                    Check(sprites.Count == 12, "Exactly twelve sprites: " + biome);
                    for (int row = 0; row < 6; row++)
                    {
                        Sprite first = sprites[$"Retro_{biome}_{row}_0"], second = sprites[$"Retro_{biome}_{row}_1"];
                        Check(first.rect.size == second.rect.size && first.pivot.y == second.pivot.y, "Stable frame pair size and vertical anchor: " + biome + row);
                        Check(first.rect.xMin >= 0 && first.rect.xMax <= second.rect.xMin && second.rect.xMax <= raw.width &&
                            first.rect.yMin >= 0 && first.rect.yMax <= raw.height, "Whole sprites stay within their own columns: " + biome + row);
                        int visibleFirst = 0, visibleSecond = 0, different = 0;
                        float horizontalFirst = 0, horizontalSecond = 0;
                        for (int y = 0; y < first.rect.height; y++)
                            for (int x = 0; x < first.rect.width; x++)
                            {
                                Color32 a = pixels[((int)first.rect.y + y) * raw.width + (int)first.rect.x + x];
                                Color32 b = pixels[((int)second.rect.y + y) * raw.width + (int)second.rect.x + x];
                                if (a.a > 160) { visibleFirst++; horizontalFirst += x; }
                                if (b.a > 160) { visibleSecond++; horizontalSecond += x; }
                                if (a.a != b.a || (a.a > 160 && !a.Equals(b))) different++;
                            }
                        Check(visibleFirst > 1000 && visibleSecond > 1000 && different > 100, "Two actual nonempty distinct animation poses: " + biome + row);
                        Check((float)visibleFirst / visibleSecond > .65f && (float)visibleFirst / visibleSecond < 1.5f,
                            "Animation pair retains comparable visual mass: " + biome + row);
                        Check(Mathf.Abs((horizontalFirst / visibleFirst - first.pivot.x) - (horizontalSecond / visibleSecond - second.pivot.x)) < first.rect.width * .12f,
                            "Body centers remain aligned between frames: " + biome + row);
                    }
                }
                finally { Object.DestroyImmediate(raw); }
            }
            StageDefinition dungeon = Resources.Load<StageDefinition>("ProjectDM/Stages/Stage2_Dungeon");
            Check(dungeon.monsters.Select(monster => monster.displayName).SequenceEqual(new[] { "동굴 박쥐", "지하 쥐", "사슬 유령", "돌거미", "망령 갑옷" }) &&
                dungeon.bossName == "봉인 수문장", "Dungeon has its own creature roster rather than the rejected generic set");
            Check(!File.Exists(ProjectDMMonsterArtSetup.ArtFolder + "/ProjectDM_Dungeon_Retro_2Frame_v1.png") &&
                !File.Exists(ProjectDMMonsterArtSetup.ArtFolder + "/ProjectDM_Dungeon_Retro_2Frame_v1.png.meta"),
                "Rejected dungeon atlas and metadata are absent");
            string[] before = Identities();
            float[] bossHeights = Resources.LoadAll<StageDefinition>("ProjectDM/Stages").OrderBy(stage => stage.stageId)
                .Select(stage => stage.bossFrames[0].bounds.size.y * stage.bossScale).ToArray();
            ProjectDMMonsterArtSetup.Install();
            Check(before.SequenceEqual(Identities()), "Repeated import preserves every sprite GUID/fileID");
            float[] after = Resources.LoadAll<StageDefinition>("ProjectDM/Stages").OrderBy(stage => stage.stageId)
                .Select(stage => stage.bossFrames[0].bounds.size.y * stage.bossScale).ToArray();
            Check(bossHeights.Zip(after, (a, b) => Mathf.Abs(a - b) < .0001f).All(same => same), "Repeated import preserves boss preview size");
            Debug.Log($"Retro monster art: {checks} assertions PASS (two biomes, 24 frames, transparent pairs and stable reimport).");
        }

        private static string[] Identities() => ProjectDMMonsterArtSetup.Biomes.SelectMany(biome =>
            AssetDatabase.LoadAllAssetsAtPath(ProjectDMMonsterArtSetup.SheetPath(biome)).OfType<Sprite>())
            .Select(sprite => GlobalObjectId.GetGlobalObjectIdSlow(sprite).ToString()).OrderBy(id => id).ToArray();

        public static void Render()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch-only isolated rendering.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory("Logs");
            foreach (StageDefinition stage in Resources.LoadAll<StageDefinition>("ProjectDM/Stages"))
            {
                GameObject root = new GameObject("Retro monster stage render", typeof(Grid));
                GameObject floor = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer));
                floor.transform.SetParent(root.transform, false);
                floor.GetComponent<TilemapRenderer>().sortingOrder = -10;
                DungeonFloorTilemap ground = floor.AddComponent<DungeonFloorTilemap>();
                ground.SetGroundRuleTile(stage.ground); ground.Configure(new Vector2(25, 17));
                Camera camera = new GameObject("Camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = 5.6f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = stage.background;
                for (int row = 0; row < 2; row++)
                    for (int slot = 0; slot < 5; slot++)
                        Actor(stage.monsters[slot].frames[row], new Vector3((slot - 2) * 3.1f, row == 0 ? -.2f : -3f),
                            slot == 4 ? .82f : slot == 3 ? .78f : .65f);
                Actor(stage.bossFrames[0], new Vector3(-5, 3.1f), stage.bossScale);
                CharacterVisuals characters = Resources.Load<CharacterVisuals>("ProjectDM/Characters/CharacterVisuals");
                Actor(characters.Find(stage.stageId == 1 ? "projectile" : "frost").idleSprite, new Vector3(0, 3.1f), .95f);
                // Actual mini-boss scale on the current highest-slot creature, inherited by live spawning.
                Actor(stage.monsters[4].frames[0], new Vector3(5, 3.1f), .82f * GameTables.LoadResources(new GameSettings()).balance.miniBossScale);
                var target = new RenderTexture(1920, 1080, 24);
                var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                    File.WriteAllBytes($"Logs/RetroMonsters-Stage{stage.stageId}.png", pixels.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous; camera.targetTexture = null;
                    Object.DestroyImmediate(pixels); target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(root);
                }

                void Actor(Sprite sprite, Vector3 position, float scale)
                {
                    GameObject actor = new GameObject(sprite.name, typeof(SpriteRenderer));
                    actor.transform.SetParent(root.transform, false); actor.transform.position = position;
                    actor.transform.localScale = Vector3.one * scale;
                    SpriteRenderer renderer = actor.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = 3;
                }
            }
        }
    }
}
