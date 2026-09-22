using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectDM.Editor
{
    /// <summary>
    /// One-click art pipeline for the generated Project DM sheets.
    /// It imports each sheet as Multiple/Point-filtered sprites, creates floor Tile assets,
    /// and produces usable animation clips plus Animator Controllers.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectDMArtPipeline
    {
        private const string SpriteSheetPath = "Assets/GameContent/Art/ProjectDM_Sprites_TopDown_v2.png";
        private const string ActorSheetPath = "Assets/GameContent/Art/ProjectDM_Monsters_16Bit_v5.png";
        private const string CutePlayerSheetPath = "Assets/GameContent/Art/ProjectDM_Player_16Bit_v5.png";
        private const string WalkCyclePlayerSheetPath = "Assets/GameContent/Art/ProjectDM_Player_Walk_4Frame_v6.png";
        private const string SideWalkPlayerSheetPath = "Assets/GameContent/Art/ProjectDM_Player_Walk_SideRefined_v7.png";
        private const string IdlePlayerSheetPath = "Assets/GameContent/Art/ProjectDM_Player_Idle_4Frame_v1.png";
        private const string FloorSheetPath = "Assets/GameContent/Art/ProjectDM_FloorTiles_v1.png";
        private const string TileFolder = "Assets/GameContent/Tiles";
        private const string AnimationFolder = "Assets/GameContent/Animation";
        private const string SpriteCatalogPath = "Assets/GameContent/Configuration/ProjectDMSpriteCatalog.asset";
        private const string ConfigurationTag = "ProjectDM_ArtPipeline_v2";

        static ProjectDMArtPipeline()
        {
            EditorApplication.delayCall += ConfigureIfNeeded;
        }

        [MenuItem("Project DM/Rebuild Art Pipeline")]
        public static void RebuildFromMenu()
        {
            Configure(force: true);
        }

        [MenuItem("Project DM/Rebuild Generated Tiles and Animation")]
        public static void RebuildGeneratedAssets()
        {
            CreateRequiredFolder("Assets", "GameContent");
            CreateRequiredFolder("Assets/GameContent", "Tiles");
            CreateRequiredFolder("Assets/GameContent", "Animation");
            CreateFloorTiles();
            CreateAnimationsAndControllers();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Project DM generated Tile and Animation assets are ready.");
        }

        [MenuItem("Project DM/Sync Character and Monster Slices")]
        public static void SyncCharacterAndMonsterSlices()
        {
            TextureImporter playerImporter = AssetImporter.GetAtPath(CutePlayerSheetPath) as TextureImporter;
            TextureImporter actorImporter = AssetImporter.GetAtPath(ActorSheetPath) as TextureImporter;
            if (playerImporter == null || actorImporter == null)
            {
                return;
            }

            ConfigureActorImporterFromOpaqueRegions(actorImporter, playerImporter.spritePixelsPerUnit);
            CreateAnimationsAndControllers();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Project DM character and monster slices were synchronized without changing the player slices.");
        }

        [MenuItem("Project DM/Import 4-Frame Player Walk Cycle")]
        public static void ImportFourFramePlayerWalkCycle()
        {
            TextureImporter referenceImporter = AssetImporter.GetAtPath(CutePlayerSheetPath) as TextureImporter;
            TextureImporter walkImporter = AssetImporter.GetAtPath(WalkCyclePlayerSheetPath) as TextureImporter;
            if (referenceImporter == null || walkImporter == null)
            {
                Debug.LogWarning("Project DM could not import the four-frame walk cycle because its source sheet is missing.");
                return;
            }

            ConfigureWalkCyclePlayerImporter(walkImporter, referenceImporter.spritePixelsPerUnit);
            CreateAnimationsAndControllers();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Project DM four-frame player walk cycle is ready.");
        }

        [MenuItem("Project DM/Match Player Walk Frames to First Slice")]
        public static void MatchPlayerWalkFramesToFirstSlice()
        {
            TextureImporter referenceImporter = AssetImporter.GetAtPath(CutePlayerSheetPath) as TextureImporter;
            TextureImporter walkImporter = AssetImporter.GetAtPath(WalkCyclePlayerSheetPath) as TextureImporter;
            if (referenceImporter == null || walkImporter == null)
            {
                Debug.LogWarning("Project DM could not synchronize player walk frames because its source sheet is missing.");
                return;
            }

            ConfigureWalkCyclePlayerImporterFromFirstSlice(walkImporter, referenceImporter.spritePixelsPerUnit);
            CreateAnimationsAndControllers();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Project DM player walk frames now match the first user-defined slice.");
        }

        [MenuItem("Project DM/Import Refined Player Side Walk")]
        public static void ImportRefinedPlayerSideWalk()
        {
            TextureImporter sourceImporter = AssetImporter.GetAtPath(WalkCyclePlayerSheetPath) as TextureImporter;
            TextureImporter sideImporter = AssetImporter.GetAtPath(SideWalkPlayerSheetPath) as TextureImporter;
            if (sourceImporter == null || sideImporter == null)
            {
                Debug.LogWarning("Project DM could not import the refined side walk sheet because its source is missing.");
                return;
            }

            ConfigureSideWalkImporterFromSourceSlices(sideImporter, sourceImporter);
            CreateAnimationsAndControllers();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Project DM refined side walk frames are ready and use the source x/y/w/h slices.");
        }

        [MenuItem("Project DM/Import Player Idle Animation")]
        public static void ImportPlayerIdleAnimation()
        {
            TextureImporter walkImporter = AssetImporter.GetAtPath(WalkCyclePlayerSheetPath) as TextureImporter;
            TextureImporter idleImporter = AssetImporter.GetAtPath(IdlePlayerSheetPath) as TextureImporter;
            if (walkImporter == null || idleImporter == null)
            {
                Debug.LogWarning("Project DM could not import the player idle sheet because its source is missing.");
                return;
            }

            ConfigureIdleImporterFromWalkSlices(idleImporter, walkImporter);
            CreateAnimationsAndControllers();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Project DM four-frame player idle animation is ready.");
        }

        private static void ConfigureIfNeeded()
        {
            Configure(force: false);
        }

        private static void Configure(bool force)
        {
            if (AssetDatabase.LoadMainAssetAtPath(SpriteSheetPath) == null || AssetDatabase.LoadMainAssetAtPath(ActorSheetPath) == null || AssetDatabase.LoadMainAssetAtPath(CutePlayerSheetPath) == null || AssetDatabase.LoadMainAssetAtPath(FloorSheetPath) == null)
            {
                return;
            }

            TextureImporter spriteImporter = AssetImporter.GetAtPath(SpriteSheetPath) as TextureImporter;
            TextureImporter actorImporter = AssetImporter.GetAtPath(ActorSheetPath) as TextureImporter;
            TextureImporter cutePlayerImporter = AssetImporter.GetAtPath(CutePlayerSheetPath) as TextureImporter;
            TextureImporter floorImporter = AssetImporter.GetAtPath(FloorSheetPath) as TextureImporter;
            TextureImporter walkImporter = AssetImporter.GetAtPath(WalkCyclePlayerSheetPath) as TextureImporter;
            TextureImporter idleImporter = AssetImporter.GetAtPath(IdlePlayerSheetPath) as TextureImporter;
            if (spriteImporter == null || actorImporter == null || cutePlayerImporter == null || floorImporter == null)
            {
                return;
            }

            bool idleIsConfigured = idleImporter == null || idleImporter.userData == ConfigurationTag;
            if (!force && spriteImporter.userData == ConfigurationTag && actorImporter.userData == ConfigurationTag && cutePlayerImporter.userData == ConfigurationTag && floorImporter.userData == ConfigurationTag && idleIsConfigured)
            {
                return;
            }

            Texture2D characterSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteSheetPath);
            Texture2D actorSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(ActorSheetPath);
            Texture2D cutePlayerSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(CutePlayerSheetPath);
            Texture2D floorSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(FloorSheetPath);
            if (characterSheet == null || actorSheet == null || cutePlayerSheet == null || floorSheet == null)
            {
                EditorApplication.delayCall += ConfigureIfNeeded;
                return;
            }

            ConfigureCharacterImporter(spriteImporter, characterSheet);
            // Player and monster frame rectangles are intentionally user-owned after the initial art setup.
            // Rebuilding generated assets must never overwrite manually adjusted slices.
            ConfigureFloorImporter(floorImporter, floorSheet);
            if (idleImporter != null && walkImporter != null)
            {
                ConfigureIdleImporterFromWalkSlices(idleImporter, walkImporter);
            }
            CreateRequiredFolder("Assets", "GameContent");
            CreateRequiredFolder("Assets/GameContent", "Tiles");
            CreateRequiredFolder("Assets/GameContent", "Animation");
            CreateFloorTiles();
            CreateAnimationsAndControllers();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Project DM art pipeline: sprites, Tile assets, clips, and controllers are ready.");
        }

        private static void ConfigureCharacterImporter(TextureImporter importer, Texture2D texture)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 64;
            SetSpriteRects(importer, CharacterSlices(texture.width, texture.height));
            importer.userData = ConfigurationTag;
            importer.SaveAndReimport();
        }

        private static void ConfigureFloorImporter(TextureImporter importer, Texture2D texture)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 64;
            SetSpriteRects(importer, FloorSlices(texture.width, texture.height));
            importer.userData = ConfigurationTag;
            importer.SaveAndReimport();
        }

        private static void ConfigureActorImporter(TextureImporter importer, Texture2D texture)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 64;
            importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
            SetSpriteRects(importer, ActorSlices(sourceWidth, sourceHeight));
            importer.userData = ConfigurationTag;
            importer.SaveAndReimport();
        }

        private static void ConfigureActorImporterFromOpaqueRegions(TextureImporter importer, float pixelsPerUnit)
        {
            bool restoreReadable = importer.isReadable;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            importer.SaveAndReimport();

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ActorSheetPath);
            List<Rect> regions = CombineMonsterRegions(FindOpaqueRegions(texture), texture.width, texture.height);
            if (regions.Count != 4)
            {
                string details = string.Join(", ", regions.Select(region => $"({region.x},{region.y},{region.width},{region.height})"));
                throw new System.InvalidOperationException($"Expected four monster frames but found {regions.Count} opaque regions: {details}");
            }

            SetSpriteRects(importer, new[]
            {
                SpriteMetadata("Actor_Slime_0", regions[0]), SpriteMetadata("Actor_Slime_1", regions[1]),
                SpriteMetadata("Actor_Skeleton_0", regions[2]), SpriteMetadata("Actor_Skeleton_1", regions[3])
            });
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.isReadable = restoreReadable;
            importer.userData = ConfigurationTag;
            importer.SaveAndReimport();
        }

        private static SpriteMetaData SpriteMetadata(string name, Rect rect)
        {
            return new SpriteMetaData
            {
                name = name,
                rect = rect,
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(.5f, .5f)
            };
        }

        private static List<Rect> FindOpaqueRegions(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            bool[] visited = new bool[pixels.Length];
            List<Rect> regions = new();
            int width = texture.width;
            int height = texture.height;
            for (int index = 0; index < pixels.Length; index++)
            {
                if (visited[index] || pixels[index].a <= 16)
                {
                    continue;
                }

                Queue<int> pending = new();
                pending.Enqueue(index);
                visited[index] = true;
                int count = 0;
                int minX = width;
                int minY = height;
                int maxX = 0;
                int maxY = 0;
                while (pending.Count > 0)
                {
                    int current = pending.Dequeue();
                    int x = current % width;
                    int y = current / width;
                    count++;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            int nextX = x + offsetX;
                            int nextY = y + offsetY;
                            if ((offsetX == 0 && offsetY == 0) || nextX < 0 || nextY < 0 || nextX >= width || nextY >= height)
                            {
                                continue;
                            }

                            int next = nextY * width + nextX;
                            if (!visited[next] && pixels[next].a > 16)
                            {
                                visited[next] = true;
                                pending.Enqueue(next);
                            }
                        }
                    }
                }

                if (count > 128)
                {
                    regions.Add(new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
                }
            }

            return regions;
        }

        private static List<Rect> CombineMonsterRegions(List<Rect> regions, int textureWidth, int textureHeight)
        {
            Rect?[] frames = new Rect?[4];
            foreach (Rect region in regions)
            {
                int row = region.center.y >= textureHeight * .5f ? 0 : 1;
                int column = region.center.x >= textureWidth * .5f ? 1 : 0;
                int index = row * 2 + column;
                frames[index] = frames[index].HasValue ? Union(frames[index].Value, region) : region;
            }

            return frames.Where(frame => frame.HasValue).Select(frame => frame.Value).ToList();
        }

        private static Rect Union(Rect first, Rect second)
        {
            float minX = Mathf.Min(first.xMin, second.xMin);
            float minY = Mathf.Min(first.yMin, second.yMin);
            float maxX = Mathf.Max(first.xMax, second.xMax);
            float maxY = Mathf.Max(first.yMax, second.yMax);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static void ConfigureCutePlayerImporter(TextureImporter importer, Texture2D texture)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 96;
            importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
            SetSpriteRects(importer, CutePlayerSlices(sourceWidth, sourceHeight));
            importer.userData = ConfigurationTag;
            importer.SaveAndReimport();
        }

        private static void ConfigureWalkCyclePlayerImporter(TextureImporter importer, float pixelsPerUnit)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            SetSpriteRects(importer, FourFrameWalkCycleSlices(width, height));
            importer.userData = ConfigurationTag;
            importer.SaveAndReimport();
        }

        private static void ConfigureWalkCyclePlayerImporterFromFirstSlice(TextureImporter importer, float pixelsPerUnit)
        {
            SpriteRect firstSlice = CurrentSpriteRects(importer).FirstOrDefault(sprite => sprite.name == "Walk_Left_0");
            if (firstSlice == null)
            {
                throw new System.InvalidOperationException("The first player walk slice (Walk_Left_0) was not found.");
            }

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            float cellWidth = width / 4f;
            float cellHeight = height / 4f;
            float firstCellBottom = height - cellHeight;
            Vector2 cellOffset = new(firstSlice.rect.x, firstSlice.rect.y - firstCellBottom);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            SetSpriteRects(importer, FourFrameWalkCycleSlices(width, height, firstSlice.rect.size, cellOffset, firstSlice.alignment, firstSlice.pivot));
            importer.userData = ConfigurationTag;
            importer.SaveAndReimport();
        }

        private static void ConfigureSideWalkImporterFromSourceSlices(TextureImporter importer, TextureImporter sourceImporter)
        {
            SpriteRect[] sourceSlices = CurrentSpriteRects(sourceImporter)
                .Where(sprite => sprite.name.StartsWith("Walk_"))
                .ToArray();
            if (sourceSlices.Length != 16)
            {
                throw new System.InvalidOperationException($"Expected 16 source walk slices but found {sourceSlices.Length}.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = sourceImporter.spritePixelsPerUnit;
            SetSpriteRects(importer, sourceSlices.Select(sprite => new SpriteMetaData
            {
                name = sprite.name,
                rect = sprite.rect,
                alignment = (int)sprite.alignment,
                pivot = sprite.pivot
            }).ToArray());
            importer.userData = ConfigurationTag;
            importer.SaveAndReimport();
        }

        private static void ConfigureIdleImporterFromWalkSlices(TextureImporter importer, TextureImporter walkImporter)
        {
            SpriteRect[] walkSlices = CurrentSpriteRects(walkImporter)
                .Where(sprite => sprite.name.StartsWith("Walk_"))
                .ToArray();
            if (walkSlices.Length != 16)
            {
                throw new System.InvalidOperationException($"Expected 16 walk slices but found {walkSlices.Length}.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = walkImporter.spritePixelsPerUnit;
            SetSpriteRects(importer, walkSlices.Select(sprite => new SpriteMetaData
            {
                name = sprite.name.Replace("Walk_", "Idle_"),
                rect = sprite.rect,
                alignment = (int)sprite.alignment,
                pivot = sprite.pivot
            }).ToArray());
            importer.userData = ConfigurationTag;
            importer.SaveAndReimport();
        }

        private static SpriteMetaData[] CharacterSlices(int width, int height)
        {
            // The source was generated at 2:1. Fractions make this resilient to a later re-export.
            return new[]
            {
                Slice("Player_Back", width, height, .175f, .665f, .125f, .31f),
                Slice("Player_Left", width, height, .350f, .665f, .135f, .31f),
                Slice("Player_Right", width, height, .525f, .665f, .135f, .31f),
                Slice("Player_ForwardHidden", width, height, .690f, .665f, .135f, .31f),
                Slice("Skeleton_0", width, height, .165f, .360f, .155f, .30f),
                Slice("Skeleton_1", width, height, .350f, .360f, .155f, .30f),
                Slice("Skeleton_2", width, height, .530f, .360f, .155f, .30f),
                Slice("Skeleton_3", width, height, .700f, .360f, .155f, .30f),
                Slice("Slime_0", width, height, .165f, .115f, .165f, .255f),
                Slice("Slime_1", width, height, .350f, .115f, .165f, .255f),
                Slice("Slime_2", width, height, .530f, .115f, .165f, .255f),
                Slice("Slime_3", width, height, .700f, .115f, .165f, .255f),
                Slice("Arcane_Bolt", width, height, .020f, .025f, .100f, .180f),
                Slice("Experience_Gem", width, height, .385f, .025f, .080f, .180f),
                Slice("Gold_Coin", width, height, .545f, .025f, .060f, .160f),
                Slice("Treasure_Chest", width, height, .690f, .025f, .100f, .180f)
            };
        }

        private static SpriteMetaData[] FloorSlices(int width, int height)
        {
            List<SpriteMetaData> tiles = new();
            float cellWidth = width / 4f;
            float cellHeight = height / 4f;
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    // Unity sprite rects use a bottom-left origin; generated rows are read top-to-bottom.
                    Rect rect = new(column * cellWidth, height - (row + 1) * cellHeight, cellWidth, cellHeight);
                    tiles.Add(new SpriteMetaData
                    {
                        name = $"Floor_{row}_{column}",
                        rect = rect,
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(.5f, .5f)
                    });
                }
            }
            return tiles.ToArray();
        }

        private static SpriteMetaData[] ActorSlices(int width, int height)
        {
            float cellWidth = width / 2f;
            float cellHeight = height / 2f;
            return new[]
            {
                GridSlice("Actor_Slime_0", 0, 0, cellWidth, cellHeight, width, height),
                GridSlice("Actor_Slime_1", 1, 0, cellWidth, cellHeight, width, height),
                GridSlice("Actor_Skeleton_0", 0, 1, cellWidth, cellHeight, width, height),
                GridSlice("Actor_Skeleton_1", 1, 1, cellWidth, cellHeight, width, height)
            };
        }

        private static SpriteMetaData[] CutePlayerSlices(int width, int height)
        {
            float cellWidth = width / 4f;
            float cellHeight = height / 2f;
            return new[]
            {
                GridSlice("Cute_Left_0", 0, 0, cellWidth, cellHeight, width, height), GridSlice("Cute_Down_0", 1, 0, cellWidth, cellHeight, width, height), GridSlice("Cute_Right_0", 2, 0, cellWidth, cellHeight, width, height), GridSlice("Cute_Up_0", 3, 0, cellWidth, cellHeight, width, height),
                GridSlice("Cute_Left_1", 0, 1, cellWidth, cellHeight, width, height), GridSlice("Cute_Down_1", 1, 1, cellWidth, cellHeight, width, height), GridSlice("Cute_Right_1", 2, 1, cellWidth, cellHeight, width, height), GridSlice("Cute_Up_1", 3, 1, cellWidth, cellHeight, width, height)
            };
        }

        private static SpriteMetaData[] FourFrameWalkCycleSlices(int width, int height)
        {
            return FourFrameWalkCycleSlices(width, height, Vector2.zero, Vector2.zero, SpriteAlignment.Center, new Vector2(.5f, .5f));
        }

        private static SpriteMetaData[] FourFrameWalkCycleSlices(int width, int height, Vector2 size, Vector2 cellOffset, SpriteAlignment alignment, Vector2 pivot)
        {
            string[] directions = { "Left", "Down", "Right", "Up" };
            List<SpriteMetaData> slices = new();
            float cellWidth = width / 4f;
            float cellHeight = height / 4f;
            for (int frame = 0; frame < 4; frame++)
            {
                for (int direction = 0; direction < directions.Length; direction++)
                {
                    if (size == Vector2.zero)
                    {
                        slices.Add(GridSlice($"Walk_{directions[direction]}_{frame}", direction, frame, cellWidth, cellHeight, width, height));
                        continue;
                    }

                    slices.Add(new SpriteMetaData
                    {
                        name = $"Walk_{directions[direction]}_{frame}",
                        rect = new Rect(direction * cellWidth + cellOffset.x, height - (frame + 1) * cellHeight + cellOffset.y, size.x, size.y),
                        alignment = (int)alignment,
                        pivot = pivot
                    });
                }
            }
            return slices.ToArray();
        }

        private static SpriteMetaData GridSlice(string name, int column, int row, float cellWidth, float cellHeight, int width, int height)
        {
            const float inset = 1f;
            return new SpriteMetaData
            {
                name = name,
                // Keep a one-pixel inset: float rounding at the outer sheet border otherwise invalidates edge cells.
                rect = new Rect(column * cellWidth + inset, height - (row + 1) * cellHeight + inset, cellWidth - inset * 2f, cellHeight - inset * 2f),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(.5f, .5f)
            };
        }

        private static SpriteMetaData Slice(string name, int width, int height, float x, float y, float sliceWidth, float sliceHeight)
        {
            return new SpriteMetaData
            {
                name = name,
                rect = new Rect(width * x, height * y, width * sliceWidth, height * sliceHeight),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(.5f, .5f)
            };
        }

        private static void SetSpriteRects(TextureImporter importer, SpriteMetaData[] metadata)
        {
            SpriteDataProviderFactories factories = new();
            factories.Init();
            ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();
            SpriteRect[] spriteRects = metadata.Select(data => new SpriteRect
            {
                name = data.name,
                rect = data.rect,
                alignment = (SpriteAlignment)data.alignment,
                pivot = data.pivot,
                spriteID = GUID.Generate()
            }).ToArray();
            dataProvider.SetSpriteRects(spriteRects);

            ISpriteNameFileIdDataProvider nameProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameProvider != null)
            {
                nameProvider.SetNameFileIdPairs(spriteRects.Select(sprite => new SpriteNameFileIdPair(sprite.name, sprite.spriteID)));
            }

            dataProvider.Apply();
        }

        private static SpriteRect[] CurrentSpriteRects(TextureImporter importer)
        {
            SpriteDataProviderFactories factories = new();
            factories.Init();
            ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();
            return dataProvider.GetSpriteRects();
        }

        private static void CreateFloorTiles()
        {
            Dictionary<string, Sprite> sprites = SpriteMap(FloorSheetPath);
            foreach (KeyValuePair<string, Sprite> pair in sprites)
            {
                string path = $"{TileFolder}/{pair.Key}.asset";
                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, path);
                }

                tile.sprite = pair.Value;
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
            }
        }

        private static void CreateAnimationsAndControllers()
        {
            Dictionary<string, Sprite> actors = SpriteMap(ActorSheetPath);
            Dictionary<string, Sprite> items = SpriteMap(SpriteSheetPath);
            Dictionary<string, Sprite> walkCycleFrames = SpriteMap(WalkCyclePlayerSheetPath);
            Dictionary<string, Sprite> refinedSideWalkFrames = SpriteMap(SideWalkPlayerSheetPath);
            Dictionary<string, Sprite> idleFrames = SpriteMap(IdlePlayerSheetPath);
            Sprite[] left = PlayerWalkFrames(refinedSideWalkFrames, "Left") ?? PlayerWalkFrames(walkCycleFrames, "Left");
            Sprite[] down = PlayerWalkFrames(walkCycleFrames, "Down");
            Sprite[] right = PlayerWalkFrames(refinedSideWalkFrames, "Right") ?? PlayerWalkFrames(walkCycleFrames, "Right");
            Sprite[] up = PlayerWalkFrames(walkCycleFrames, "Up");
            Sprite[] idleLeft = PlayerFrames(idleFrames, "Idle", "Left");
            Sprite[] idleDown = PlayerFrames(idleFrames, "Idle", "Down");
            Sprite[] idleRight = PlayerFrames(idleFrames, "Idle", "Right");
            Sprite[] idleUp = PlayerFrames(idleFrames, "Idle", "Up");
            if (left == null || down == null || right == null || up == null)
            {
                List<Sprite> playerFrames = SpriteMap(CutePlayerSheetPath).Values
                    .OrderBy(sprite => sprite.rect.x)
                    .ThenByDescending(sprite => sprite.rect.y)
                    .ToList();
                if (playerFrames.Count == 8)
                {
                    left = new[] { playerFrames[0], playerFrames[1] };
                    down = new[] { playerFrames[2], playerFrames[3] };
                    right = new[] { playerFrames[4], playerFrames[5] };
                    up = new[] { playerFrames[6], playerFrames[7] };
                }
            }
            if (!actors.ContainsKey("Actor_Slime_0") || !actors.ContainsKey("Actor_Slime_1")
                || !actors.ContainsKey("Actor_Skeleton_0") || !actors.ContainsKey("Actor_Skeleton_1")
                || !items.ContainsKey("Arcane_Bolt") || left == null || down == null || right == null || up == null)
            {
                Debug.LogWarning("Project DM could not create animations because the expected player or monster frames are missing.");
                return;
            }

            Sprite[] skeleton = { actors["Actor_Skeleton_0"], actors["Actor_Skeleton_1"] };
            Sprite[] slime = { actors["Actor_Slime_0"], actors["Actor_Slime_1"] };
            idleLeft ??= RepeatFrame(left[0]);
            idleDown ??= RepeatFrame(down[0]);
            idleRight ??= RepeatFrame(right[0]);
            idleUp ??= RepeatFrame(up[0]);

            AnimationClip skeletonWalk = CreateSpriteClip("ProjectDM_SkeletonWalk", skeleton, 6f);
            AnimationClip slimePulse = CreateSpriteClip("ProjectDM_SlimePulse", slime, 6f);
            AnimationClip boltLoop = CreateSpriteClip("ProjectDM_BoltLoop", new[] { items["Arcane_Bolt"] }, 8f);

            CreateCutePlayerController(
                CreateSpriteClip("ProjectDM_IdleDown", idleDown, 5f),
                CreateSpriteClip("ProjectDM_IdleLeft", idleLeft, 5f),
                CreateSpriteClip("ProjectDM_IdleRight", idleRight, 5f),
                CreateSpriteClip("ProjectDM_IdleUp", idleUp, 5f),
                CreateSpriteClip("ProjectDM_CuteDown", down, 9f),
                CreateSpriteClip("ProjectDM_CuteLeft", left, 9f),
                CreateSpriteClip("ProjectDM_CuteRight", right, 9f),
                CreateSpriteClip("ProjectDM_CuteUp", up, 9f));
            CreateSingleClipController("ProjectDM_Skeleton", skeletonWalk);
            CreateSingleClipController("ProjectDM_Slime", slimePulse);
            CreateSingleClipController("ProjectDM_Bolt", boltLoop);
            CreateSpriteCatalog(left, down, right, up, slime, skeleton);
        }

        private static void CreateSpriteCatalog(Sprite[] left, Sprite[] down, Sprite[] right, Sprite[] up, Sprite[] slime, Sprite[] skeleton)
        {
            CreateRequiredFolder("Assets/GameContent", "Configuration");
            ProjectDMSpriteCatalog catalog = AssetDatabase.LoadAssetAtPath<ProjectDMSpriteCatalog>(SpriteCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ProjectDMSpriteCatalog>();
                AssetDatabase.CreateAsset(catalog, SpriteCatalogPath);
            }

            catalog.playerLeftFrames = left;
            catalog.playerDownFrames = down;
            catalog.playerRightFrames = right;
            catalog.playerUpFrames = up;
            catalog.slimeFrames = slime;
            catalog.skeletonFrames = skeleton;
            EditorUtility.SetDirty(catalog);
        }

        private static Sprite[] PlayerWalkFrames(Dictionary<string, Sprite> sprites, string direction)
        {
            return PlayerFrames(sprites, "Walk", direction);
        }

        private static Sprite[] PlayerFrames(Dictionary<string, Sprite> sprites, string prefix, string direction)
        {
            Sprite[] frames = new Sprite[4];
            for (int frame = 0; frame < frames.Length; frame++)
            {
                if (!sprites.TryGetValue($"{prefix}_{direction}_{frame}", out frames[frame]))
                {
                    return null;
                }
            }
            return frames;
        }

        private static Sprite[] RepeatFrame(Sprite sprite)
        {
            return new[] { sprite, sprite, sprite, sprite };
        }

        private static AnimationClip CreateSpriteClip(string clipName, Sprite[] frames, float frameRate)
        {
            string path = $"{AnimationFolder}/{clipName}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.frameRate = frameRate;
            List<ObjectReferenceKeyframe> keys = new();
            for (int i = 0; i < frames.Length; i++)
            {
                keys.Add(new ObjectReferenceKeyframe { time = i / frameRate, value = frames[i] });
            }
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void CreatePlayerController(AnimationClip idle, AnimationClip walk)
        {
            string path = $"{AnimationFolder}/ProjectDM_Player.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }
            if (controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
            }
            if (controller.layers[0].stateMachine.states.Length > 0)
            {
                return;
            }
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = stateMachine.AddState("Idle");
            idleState.motion = idle;
            stateMachine.defaultState = idleState;
            AnimatorState walkState = stateMachine.AddState("Walk");
            walkState.motion = walk;
            AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.08f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.01f, "Speed");
            AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.08f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.01f, "Speed");
            EditorUtility.SetDirty(controller);
        }

        private static void CreateSingleClipController(string name, AnimationClip clip)
        {
            string path = $"{AnimationFolder}/{name}.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }
            if (controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
            }
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            if (stateMachine.states.Length > 0)
            {
                return;
            }
            AnimatorState state = stateMachine.AddState("Loop");
            state.motion = clip;
            stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
        }

        private static void CreateCutePlayerController(AnimationClip idleDown, AnimationClip idleLeft, AnimationClip idleRight, AnimationClip idleUp, AnimationClip walkDown, AnimationClip walkLeft, AnimationClip walkRight, AnimationClip walkUp)
        {
            string path = $"{AnimationFolder}/ProjectDM_Player_Cute_v4.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            if (controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
            }
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleDownState = UpsertState(stateMachine, "Idle_Down", idleDown); stateMachine.defaultState = idleDownState;
            UpsertState(stateMachine, "Idle_Left", idleLeft);
            UpsertState(stateMachine, "Idle_Right", idleRight);
            UpsertState(stateMachine, "Idle_Up", idleUp);
            UpsertState(stateMachine, "Walk_Down", walkDown);
            UpsertState(stateMachine, "Walk_Left", walkLeft);
            UpsertState(stateMachine, "Walk_Right", walkRight);
            UpsertState(stateMachine, "Walk_Up", walkUp);
            EditorUtility.SetDirty(controller);
        }

        private static AnimatorState UpsertState(AnimatorStateMachine stateMachine, string name, Motion motion)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (childState.state.name == name)
                {
                    childState.state.motion = motion;
                    return childState.state;
                }
            }

            AnimatorState state = stateMachine.AddState(name);
            state.motion = motion;
            return state;
        }

        private static Dictionary<string, Sprite> SpriteMap(string assetPath)
        {
            Dictionary<string, Sprite> sprites = new();
            foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
            {
                if (asset is Sprite sprite)
                {
                    sprites[sprite.name] = sprite;
                }
            }
            return sprites;
        }

        private static void CreateRequiredFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
