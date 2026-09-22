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
        private const string FloorSheetPath = "Assets/GameContent/Art/ProjectDM_FloorTiles_v1.png";
        private const string TileFolder = "Assets/GameContent/Tiles";
        private const string AnimationFolder = "Assets/GameContent/Animation";
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
            if (spriteImporter == null || actorImporter == null || cutePlayerImporter == null || floorImporter == null)
            {
                return;
            }

            if (!force && spriteImporter.userData == ConfigurationTag && actorImporter.userData == ConfigurationTag && cutePlayerImporter.userData == ConfigurationTag && floorImporter.userData == ConfigurationTag)
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
            ConfigureActorImporter(actorImporter, actorSheet);
            ConfigureCutePlayerImporter(cutePlayerImporter, cutePlayerSheet);
            ConfigureFloorImporter(floorImporter, floorSheet);
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
            Dictionary<string, Sprite> cutePlayer = SpriteMap(CutePlayerSheetPath);
            if (!actors.ContainsKey("Actor_Slime_0") || !actors.ContainsKey("Actor_Skeleton_0") || !items.ContainsKey("Arcane_Bolt") || !cutePlayer.ContainsKey("Cute_Down_0"))
            {
                return;
            }

            AnimationClip skeletonWalk = CreateSpriteClip("ProjectDM_SkeletonWalk", new[] { actors["Actor_Skeleton_0"], actors["Actor_Skeleton_1"] }, 6f);
            AnimationClip slimePulse = CreateSpriteClip("ProjectDM_SlimePulse", new[] { actors["Actor_Slime_0"], actors["Actor_Slime_1"] }, 6f);
            AnimationClip boltLoop = CreateSpriteClip("ProjectDM_BoltLoop", new[] { items["Arcane_Bolt"] }, 8f);

            CreateCutePlayerController(
                CreateSpriteClip("ProjectDM_CuteDown", new[] { cutePlayer["Cute_Down_0"], cutePlayer["Cute_Down_1"] }, 7f),
                CreateSpriteClip("ProjectDM_CuteLeft", new[] { cutePlayer["Cute_Left_0"], cutePlayer["Cute_Left_1"] }, 7f),
                CreateSpriteClip("ProjectDM_CuteRight", new[] { cutePlayer["Cute_Right_0"], cutePlayer["Cute_Right_1"] }, 7f),
                CreateSpriteClip("ProjectDM_CuteUp", new[] { cutePlayer["Cute_Up_0"], cutePlayer["Cute_Up_1"] }, 7f));
            CreateSingleClipController("ProjectDM_Skeleton", skeletonWalk);
            CreateSingleClipController("ProjectDM_Slime", slimePulse);
            CreateSingleClipController("ProjectDM_Bolt", boltLoop);
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

        private static void CreateCutePlayerController(AnimationClip down, AnimationClip left, AnimationClip right, AnimationClip up)
        {
            string path = $"{AnimationFolder}/ProjectDM_Player_Cute_v4.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) return;
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddLayer("Base Layer");
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState downState = stateMachine.AddState("Walk_Down"); downState.motion = down; stateMachine.defaultState = downState;
            AnimatorState leftState = stateMachine.AddState("Walk_Left"); leftState.motion = left;
            AnimatorState rightState = stateMachine.AddState("Walk_Right"); rightState.motion = right;
            AnimatorState upState = stateMachine.AddState("Walk_Up"); upState.motion = up;
            EditorUtility.SetDirty(controller);
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
