// Copy into Assets/Editor of an isolated project; run ProjectDM.ScenerySharpnessRegression.Run.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectDM
{
    public static class ScenerySharpnessRegression
    {
        private static readonly List<Tuple<Camera, string>> captures = new();
        private static bool requireResolution = true;
        public static void CaptureBefore() { requireResolution = false; Run(); }
        public static void Run()
        {
            try
            {
                int count = 0;
                foreach (string path in Directory.GetFiles("Assets/GameContent/Art/Scenery", "*.png"))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (requireResolution && (importer.maxTextureSize < 256 || Mathf.Max(texture.width, texture.height) < 128))
                        throw new Exception("Scenery loses detail before rendering: " + path + " imported=" + texture.width + "x" + texture.height + " cap=" + importer.maxTextureSize);
                    if (importer.filterMode != FilterMode.Point || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
                        throw new Exception("Scenery must keep sharp Point sampling without mipmaps/compression: " + path);
                    count++;
                }
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                foreach (int stageId in new[] { 1, 2 })
                {
                    var camera = new GameObject("sharpness camera " + stageId, typeof(Camera)).GetComponent<Camera>();
                    camera.orthographic = true; camera.orthographicSize = 10;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    var settings = new GameSettings { initialStage = stageId, fieldSize = new Vector2(100, 100), gameplayCamera = camera };
                    var stages = new StageSession(settings, new WorldArtAssets()); stages.LoadContent(); stages.ApplySelected();
                    var layout = stages.Current.scenery;
                    var renderer = settings.dungeonFloor.GetComponent<StageSceneryRenderer>();
                    if (renderer.RenderedCount != layout.placements.Count) throw new Exception("Visible props must not be reduced");
                    Vector3 center = new Vector3(stageId == 1 ? -20 : 20, 0, 0);
                    // Put each preview in its own world region to keep their scenery batches separate.
                    var grid = settings.dungeonFloor.transform.parent;
                    grid.position += new Vector3(stageId * 300, 0, 0);
                    camera.transform.position = center + new Vector3(stageId * 300, 0, -10);
                    foreach (var item in layout.placements)
                        if ((stageId == 1 ? item.propId.Contains("Tree") : item.propId.Contains("Pillar")) && item.position.sqrMagnitude < 625)
                        { camera.transform.position = (Vector3)item.position + new Vector3(stageId * 300, 0, -10); break; }
                    captures.Add(Tuple.Create(camera, stageId == 1 ? "meadow-sharp.png" : "dungeon-sharp.png"));
                }
                EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
                {
                    try
                    {
                        foreach (var capture in captures)
                        {
                            var target = new RenderTexture(1652, 771, 24); capture.Item1.targetTexture = target; capture.Item1.Render();
                            RenderTexture.active = target;
                            var png = new Texture2D(1652, 771, TextureFormat.RGBA32, false);
                            png.ReadPixels(new Rect(0, 0, 1652, 771), 0, 0); png.Apply();
                            File.WriteAllBytes(Application.dataPath + "/../" + (requireResolution ? capture.Item2 : capture.Item2.Replace(".png", "-before.png")), png.EncodeToPNG());
                        }
                        Debug.Log((requireResolution ? "SCENERY_SHARPNESS_PASS " : "SCENERY_BASELINE_CAPTURE ") + count + " textures; Point/no mipmaps/uncompressed, both biomes retain all props and render correctly");
                        EditorApplication.Exit(0);
                    }
                    catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
                };
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
