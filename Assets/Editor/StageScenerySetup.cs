using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    internal static class StageScenerySetup
    {
        internal const string ArtPath = "Assets/GameContent/Art/Scenery";
        internal const string StagePath = "Assets/Resources/ProjectDM/Stages";

        [MenuItem("Project DM/Import Stage Scenery")]
        public static void Install()
        {
            foreach (string name in new[] { "Meadow_Grass", "Meadow_Flowers", "Meadow_Rock", "Meadow_Shrub", "Meadow_Tree_Oak", "Meadow_Tree_Young", "Meadow_Tree_Bent", "Meadow_Tree_Double", "Dungeon_Bricks", "Dungeon_Rubble", "Dungeon_Torch", "Dungeon_Pillar_Stump", "Dungeon_Pillar_Tall", "Dungeon_Pillar_Cracked", "Dungeon_Pillar_Fallen" })
            {
                string path = ArtPath + "/" + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("장식 PNG 누락: " + path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = true; // Cache the visible pixel mask for accurate player occlusion.
                importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 64;
                importer.spritePixelsPerUnit = 64;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = Vector2.one * .5f; settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings); importer.SaveAndReimport();
            }
            SetupStage("Stage1_Meadow", "MeadowScenery", 1);
            SetupStage("Stage2_Dungeon", "DungeonScenery", 2);
            AssetDatabase.SaveAssets();
        }

        private static void SetupStage(string stageName, string layoutName, int stageId)
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(StagePath + "/" + stageName + ".asset");
            if (stage == null) throw new InvalidOperationException("스테이지 누락: " + stageName);
            string layoutPath = StagePath + "/" + layoutName + ".asset";
            var layout = AssetDatabase.LoadAssetAtPath<StageSceneryLayout>(layoutPath);
            bool created = layout == null;
            if (created) { layout = ScriptableObject.CreateInstance<StageSceneryLayout>(); AssetDatabase.CreateAsset(layout, layoutPath); }
            StageSceneryLayout.Prop Prop(string id, string label, float height, bool flame = false) => new()
            { id = id, displayName = label, height = height, flame = flame, sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + "/" + id + ".png") };
            // Refresh the palette without touching any existing authored placement.
            if (created || layout.palette == null || layout.palette.Length == 0)
                layout.palette = stageId == 1
                    ? new[] { Prop("Meadow_Grass", "풀 무더기", 1.3f), Prop("Meadow_Flowers", "들꽃", 1.2f), Prop("Meadow_Rock", "이끼 낀 돌", 1.7f), Prop("Meadow_Shrub", "작은 관목", 2.5f), Prop("Meadow_Tree_Oak", "풍성한 나무", 5.2f), Prop("Meadow_Tree_Young", "어린 나무", 4f), Prop("Meadow_Tree_Bent", "기울어진 나무", 5.5f), Prop("Meadow_Tree_Double", "갈라진 나무", 5.2f) }
                    : new[] { Prop("Dungeon_Bricks", "흩어진 벽돌", 1.6f), Prop("Dungeon_Rubble", "석재 잔해", 2.1f), Prop("Dungeon_Torch", "횃불", 3.3f, true), Prop("Dungeon_Pillar_Stump", "짧은 석재 기둥", 4f), Prop("Dungeon_Pillar_Tall", "높은 석재 기둥", 5f), Prop("Dungeon_Pillar_Cracked", "갈라진 석재 기둥", 4.4f), Prop("Dungeon_Pillar_Fallen", "쓰러진 석재 기둥", 3.2f) };
            if (created) FillDefault(layout, stageId);
            layout.ValidateContent(); stage.scenery = layout;
            EditorUtility.SetDirty(layout); EditorUtility.SetDirty(stage);
        }

        internal static void FillDefault(StageSceneryLayout layout, int stageId)
        {
            var random = new System.Random(stageId == 1 ? 17631 : 27491);
            var placements = new List<StageSceneryLayout.Placement>();
            float Next(float low, float high) => Mathf.Lerp(low, high, (float)random.NextDouble());
            void Add(int slot, Vector2 position, bool torch = false)
            {
                if (layout.palette.Length <= slot) return;
                placements.Add(new StageSceneryLayout.Placement { propId = layout.palette[slot].id, position = position,
                    scale = torch ? 1 : Next(.8f, 1.2f), rotation = torch ? 0 : Next(-18, 18), flipX = random.Next(2) == 1 });
            }
            float step = stageId == 1 ? 3 : 4;
            for (float y = -layout.referenceSize.y * .5f + 2; y < layout.referenceSize.y * .5f - 2; y += step)
                for (float x = -layout.referenceSize.x * .5f + 2; x < layout.referenceSize.x * .5f - 2; x += step)
                {
                    Vector2 p = new Vector2(x + Next(-.8f, .8f), y + Next(-.8f, .8f));
                    if (p.sqrMagnitude < 3.2f * 3.2f || random.NextDouble() > .74) continue;
                    double roll = random.NextDouble();
                    int slot = stageId == 1 ? (roll < .56 ? 0 : roll < .73 ? 1 : roll < .88 ? 2 : 3) : (roll < .65 ? 0 : 1);
                    Add(slot, p);
                    if (stageId == 1 && slot == 0 && random.NextDouble() < .2) Add(0, p + new Vector2(.6f, .4f));
                }
            if (stageId == 2)
            {
                for (float y = -layout.referenceSize.y * .5f + 8; y < layout.referenceSize.y * .5f - 4; y += 12)
                    for (float x = -layout.referenceSize.x * .5f + 8; x < layout.referenceSize.x * .5f - 4; x += 12)
                    {
                        var p = new Vector2(x + Next(-1, 1), y + Next(-1, 1));
                        if (p.sqrMagnitude > 81) Add(2, p, true);
                    }
                Add(2, new Vector2(-5, 3.2f), true); Add(2, new Vector2(5, 3.2f), true);
            }
            // Large props are sparse, remain upright, and leave room around the spawn point.
            string[] largeIds = stageId == 1
                ? new[] { "Meadow_Tree_Oak", "Meadow_Tree_Young", "Meadow_Tree_Bent", "Meadow_Tree_Double" }
                : new[] { "Dungeon_Pillar_Stump", "Dungeon_Pillar_Tall", "Dungeon_Pillar_Cracked", "Dungeon_Pillar_Fallen" };
            for (float y = -layout.referenceSize.y * .5f + 10; y <= layout.referenceSize.y * .5f - 10; y += 16)
                for (float x = -layout.referenceSize.x * .5f + 10; x <= layout.referenceSize.x * .5f - 10; x += 16)
                {
                    Vector2 position = new Vector2(x + Next(-2.5f, 2.5f), y + Next(-2.5f, 2.5f));
                    if (layout.referenceSize == new Vector2(100, 100) && Mathf.Abs(x) == 8 && Mathf.Abs(y) == 8)
                        position = new Vector2(Mathf.Sign(x) * 7.6f, Mathf.Sign(y) * 5.6f);
                    if (position.sqrMagnitude < 64) continue;
                    string id = largeIds[random.Next(largeIds.Length)];
                    if (layout.Find(id) == null) continue;
                    placements.Add(new StageSceneryLayout.Placement { propId = id, position = position,
                        scale = Next(.85f, 1.1f), rotation = 0, flipX = random.Next(2) == 1 });
                }
            layout.placements = placements;
        }
    }
}
