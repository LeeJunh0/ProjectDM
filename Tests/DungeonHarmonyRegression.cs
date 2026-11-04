// Copy into Assets/Editor of an isolated project; run ProjectDM.DungeonHarmonyRegression.Run.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectDM
{
    public static class DungeonHarmonyRegression
    {
        public static void Run()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("C environment capture", typeof(Camera)).GetComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 10;
                camera.clearFlags = CameraClearFlags.SolidColor;
                var settings = new GameSettings { initialStage = 2, fieldSize = new Vector2(100, 100), gameplayCamera = camera };
                var stages = new StageSession(settings, new WorldArtAssets());
                stages.LoadContent(); stages.ApplySelected();
                var scenery = settings.dungeonFloor.GetComponent<StageSceneryRenderer>();
                if (stages.Current.stageId != 2 || scenery.RenderedCount != scenery.Layout.placements.Count)
                    throw new Exception("Every authored dungeon prop must remain visible");
                int stones = 0;
                foreach (var prop in scenery.Layout.palette)
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(prop.sprite));
                    if (importer.filterMode != FilterMode.Point || !importer.alphaIsTransparency)
                        throw new Exception("Scenery must preserve Point filtering and transparency");
                    if (!prop.flame)
                    {
                        double sum = 0; int count = 0;
                        foreach (var pixel in prop.sprite.texture.GetPixels32())
                            if (pixel.a > 230) { sum += (pixel.r * .2126 + pixel.g * .7152 + pixel.b * .0722) / 255; count++; }
                        if (count == 0 || sum / count > .53) throw new Exception("Stone highlights must be muted for C: " + prop.id);
                        stones++;
                    }
                }
                if (stones != 6 || scenery.Layout.Find("Dungeon_Torch").flame != true)
                    throw new Exception("C must contain six stone props and the original torch effect");
                var field = settings.dungeonFloor.GetComponent<UnityEngine.Tilemaps.Tilemap>();
                Vector3 center = Vector3.zero;
                var playerObject = new GameObject("unchanged mage", typeof(SpriteRenderer));
                var player = playerObject.GetComponent<SpriteRenderer>();
                player.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameContent/Art/Pixel32/Characters/Missile/Missile_Down_0.png");
                player.sortingOrder = 3; playerObject.transform.localScale = Vector3.one * .76f;
                // Match a real authored pillar location while keeping the mage clear of it.
                foreach (var item in scenery.Layout.placements)
                    if (item.propId == "Dungeon_Pillar_Stump" && item.position.sqrMagnitude < 400)
                    { center = item.position + new Vector2(-2, 2); break; }
                playerObject.transform.position = center;
                camera.transform.position = center + Vector3.back * 10;
                var bat = stages.Current.monsters[0].frames[0];
                for (int i = 0; i < 4; i++)
                {
                    var obj = new GameObject("unchanged bat", typeof(SpriteRenderer));
                    var renderer = obj.GetComponent<SpriteRenderer>(); renderer.sprite = bat; renderer.sortingOrder = 3;
                    obj.transform.position = center + new Vector3(-7 + i * 2, -6, 0);
                    obj.transform.localScale = Vector3.one * stages.Current.monsters[0].visualScale;
                }
                EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
                {
                    try
                    {
                        var target = new RenderTexture(1652, 771, 24); camera.targetTexture = target; camera.Render();
                        RenderTexture.active = target;
                        var png = new Texture2D(1652, 771, TextureFormat.RGBA32, false);
                        png.ReadPixels(new Rect(0, 0, 1652, 771), 0, 0); png.Apply();
                        File.WriteAllBytes(Application.dataPath + "/../dungeon-C-applied.png", png.EncodeToPNG());
                        Debug.Log("DUNGEON_HARMONY_PASS six muted stone assets, Point/alpha, all " + scenery.RenderedCount + " authored props, original flames; real renderer capture saved");
                        EditorApplication.Exit(0);
                    }
                    catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
                };
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }
}
