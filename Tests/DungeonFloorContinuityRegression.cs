// Copy into Assets/Editor of an isolated project; run ProjectDM.DungeonFloorContinuityRegression.Run.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectDM
{
    public static class DungeonFloorContinuityRegression
    {
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

        public static void Run()
        {
            try
            {
                var settings = new GameSettings();
                var tables = GameTables.LoadResources(settings, false);
                var save = new SaveService(tables, new MemoryProgressStore(), true) { progress = new PrototypeSave(), metaCurrency = 123 };
                var stages = new StageSession(settings, new WorldArtAssets());
                stages.LoadContent();
                var run = new RunController(settings, save, tables, new ProgressionService(save, tables), null,
                    () => { }, () => { }, () => { }, () => { }, () => { }, () => false, new RunMana(), stages)
                    { transition = new ScreenTransition() };
                run.BeginDebugStage(99);
                Check(!run.transition.Active && stages.SelectedId == 1, "Invalid debug stages must not restart a run");
                run.BeginDebugStage(2);
                Check(run.transition.Active && stages.SelectedId == 2 && stages.Definition(2).scenery != null,
                    "Debug entry must select the dungeon with scenery and use the normal run transition");
                run.BeginDebugStage(1);
                Check(stages.SelectedId == 2, "A repeated debug click during a transition must be ignored");
                Check(save.metaCurrency == 123 && save.progress.completedRuns == 0, "Debug entry must not award currency or record a completed run");
                Time.timeScale = 1;
                var dungeon = AssetDatabase.LoadAssetAtPath<DungeonGroundRuleTile>("Assets/GameContent/Tiles/DungeonGroundRuleTile.asset");
                var meadow = AssetDatabase.LoadAssetAtPath<DungeonGroundRuleTile>("Assets/Resources/ProjectDM/Stages/MeadowGroundRuleTile.asset");
                Check(dungeon.HasRuleTilePalette && meadow.HasRuleTilePalette, "Dungeon and meadow must retain their authored edge palettes");
                var grid = new GameObject("test grid", typeof(Grid));
                var holder = new GameObject("floor", typeof(Tilemap), typeof(TilemapRenderer));
                holder.transform.parent = grid.transform;
                var map = holder.GetComponent<Tilemap>();
                for (int y = -6; y < 6; y++) for (int x = -9; x < 9; x++) map.SetTile(new Vector3Int(x, y, 0), dungeon);
                map.RefreshAllTiles();
                var interiorSprites = new HashSet<Sprite>();
                var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameContent/Art/ProjectDM_GroundRuleTiles_3x3_v1.png");
                var paving = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameContent/Art/ProjectDM_DungeonInterior_Continuous_v1.png");
                Check(atlas.width == 1254 && paving.width == 1024, "C assets must retain their configured sheet resolutions");
                for (int y = -6; y < 6; y++) for (int x = -9; x < 9; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    var sprite = map.GetSprite(cell);
                    bool edge = x == -9 || x == 8 || y == -6 || y == 5;
                    Check(sprite != null && sprite.texture == (edge ? atlas : paving), "Cells must use the correct paving or border sheet");
                    Check(Mathf.Abs(sprite.bounds.size.x - 1) < .001f, "World tile size must stay one unit");
                    if (!edge)
                    {
                        interiorSprites.Add(sprite);
                        Check(map.GetSprite(cell + new Vector3Int(4, 0, 0)) == sprite || x + 4 >= 8, "Interior must preserve the four-cell horizontal period");
                        Check(map.GetSprite(cell + new Vector3Int(0, 4, 0)) == sprite || y + 4 >= 5, "Interior must preserve the four-cell vertical period");
                    }
                }
                Check(interiorSprites.Count == 16, "C floor must spread its texture over all sixteen connected interior tiles");
                var camera = new GameObject("capture", typeof(Camera)).GetComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 7;
                camera.transform.position = new Vector3(-.5f, -.5f, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .018f, .07f);
                // Tilemap renderer rebuilds its cached cell transforms on the next editor update.
                EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
                {
                    try
                    {
                        var target = new RenderTexture(1000, 700, 24); camera.targetTexture = target; camera.Render();
                        var renderedRotations = new HashSet<int>();
                        for (int y = -5; y < 5; y++) for (int x = -8; x < 8; x++)
                        {
                            var matrix = map.GetTransformMatrix(new Vector3Int(x, y, 0));
                            renderedRotations.Add(Mathf.RoundToInt(Mathf.Atan2(matrix.m10, matrix.m00) * Mathf.Rad2Deg / 90));
                        }
                        Check(renderedRotations.Count == 1 && renderedRotations.Contains(0), "Paving must not rotate and break grout connections");
                        RenderTexture.active = target;
                        var png = new Texture2D(1000, 700, TextureFormat.RGBA32, false);
                        png.ReadPixels(new Rect(0, 0, 1000, 700), 0, 0); png.Apply();
                        File.WriteAllBytes(Application.dataPath + "/../restored-bedrock-field.png", png.EncodeToPNG());
                        Debug.Log("DUNGEON_CONTINUITY_PASS 216 cells; C floor pattern, preserved borders, meadow and debug entry");
                        EditorApplication.Exit(0);
                    }
                    catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
                };
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }
}
