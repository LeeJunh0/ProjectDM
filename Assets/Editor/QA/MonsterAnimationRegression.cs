using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectDM.Editor
{
    public static class MonsterAnimationRegression
    {
        private static int checks;
        private static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); checks++; }

        [MenuItem("Project DM/검증/몬스터 4프레임 애니메이션 검증")]
        public static void RunMenu()
        {
            checks = 0;
            foreach (string biome in ProjectDMMonsterArtSetup.Biomes)
            {
                string path = MonsterAnimationArtSetup.SheetPath(biome);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Check(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled && !importer.isReadable &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed, "Pixel animation import settings");
                var stage = Resources.Load<StageDefinition>($"ProjectDM/Stages/Stage{(biome == "Meadow" ? 1 : 2)}_{biome}");
                stage.ValidateContent();
                var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    raw.LoadImage(File.ReadAllBytes(path)); Color32[] pixels = raw.GetPixels32();
                    Check(pixels.Count(pixel => pixel.a == 0) > pixels.Length / 2, "Truly transparent animation gutters");
                    Check(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() == 20, "Exactly 20 imported animation sprites");
                    foreach (var visual in stage.monsters)
                    {
                        Check(visual.frames.Length == MonsterAnimationArtSetup.FrameCount && visual.frames.Distinct().Count() == MonsterAnimationArtSetup.FrameCount && visual.animatorController == null,
                            "Four separate frames with a single playback owner: " + visual.displayName);
                        int[] masses = new int[MonsterAnimationArtSetup.FrameCount];
                        var hashes = new System.Collections.Generic.HashSet<ulong>();
                        for (int frame = 0; frame < visual.frames.Length; frame++)
                        {
                            Sprite sprite = Pixel32ArtSetup.Original(visual.frames[frame]); Rect rect = sprite.rect;
                            Check(AssetDatabase.GetAssetPath(sprite) == path && sprite.pivot.y == Pixel32ArtSetup.Original(visual.frames[0]).pivot.y && rect.height == Pixel32ArtSetup.Original(visual.frames[0]).rect.height,
                                "Shared vertical canvas and anchor: " + sprite.name);
                            ulong hash = 14695981039346656037UL;
                            bool clearEdges = true;
                            for (int y = (int)rect.yMin; y < rect.yMax; y++)
                                for (int x = (int)rect.xMin; x < rect.xMax; x++)
                                {
                                    Color32 pixel = pixels[y * raw.width + x];
                                    if (pixel.a > 32)
                                    {
                                        masses[frame]++;
                                        clearEdges &= x > rect.xMin + 2 && x < rect.xMax - 3;
                                    }
                                    unchecked { hash = (hash ^ pixel.a) * 1099511628211UL; }
                                }
                            hashes.Add(hash);
                            Check(clearEdges, "No neighboring frame intrudes into cell: " + sprite.name);
                            Check(masses[frame] > 200, "Nonempty actual pose");
                        }
                        Check(hashes.Count == visual.frames.Length, "Four distinct silhouettes, rather than duplicated two-pose art: " + visual.displayName);
                        Check(masses.Min() > masses.Max() * .5f, "Visual mass remains consistent over cycle: " + visual.displayName);
                        VerifyPlayback(visual);
                    }
                }
                finally { Object.DestroyImmediate(raw); }
            }
            string[] ids = Identities();
            float[] scales = Resources.LoadAll<StageDefinition>("ProjectDM/Stages").OrderBy(stage => stage.stageId)
                .SelectMany(stage => stage.monsters.Select(visual => visual.visualScale)).ToArray();
            MonsterAnimationArtSetup.Install(); ProjectDMMonsterArtSetup.Install();
            Check(ids.SequenceEqual(Identities()), "Repeated art imports preserve all animation sprite identities");
            Check(scales.SequenceEqual(Resources.LoadAll<StageDefinition>("ProjectDM/Stages").OrderBy(stage => stage.stageId)
                .SelectMany(stage => stage.monsters.Select(visual => visual.visualScale))), "Repeated imports preserve actor sizes and four-frame references");
            Debug.Log($"Monster animation: {checks} assertions PASS (40 poses, timing/catch-up, pause, legacy fallback, stable import).");
        }

        private static string[] Identities() => ProjectDMMonsterArtSetup.Biomes.SelectMany(biome =>
            AssetDatabase.LoadAllAssetsAtPath(MonsterAnimationArtSetup.SheetPath(biome)).OfType<Sprite>())
            .Select(sprite => GlobalObjectId.GetGlobalObjectIdSlow(sprite).ToString()).OrderBy(id => id).ToArray();

        private static void VerifyPlayback(StageDefinition.MonsterVisual visual)
        {
            var actor = new GameObject("Isolated animation regression", typeof(SpriteRenderer));
            try
            {
                var enemy = new Enemy { renderer = actor.GetComponent<SpriteRenderer>(), animationFrames = visual.frames, frameSeconds = visual.frameSeconds };
                enemy.renderer.sprite = visual.frames[0];
                int count = visual.frames.Length;
                for (int frame = 1; frame <= count * 2; frame++)
                {
                    enemy.AdvanceAnimation(visual.frameSeconds);
                    Check(enemy.animationFrame == frame % count && enemy.renderer.sprite == visual.frames[frame % count], "Every ordered pose and seamless loop");
                }
                enemy.AdvanceAnimation(visual.frameSeconds * .4f); float remainder = enemy.animationTime;
                enemy.AdvanceAnimation(0);
                Check(enemy.animationFrame == 0 && enemy.animationTime == remainder, "Pause does not consume animation time");
                enemy.AdvanceAnimation(visual.frameSeconds * (count + 2.3f));
                Check(enemy.animationFrame == 2 && Mathf.Abs(enemy.animationTime / visual.frameSeconds - .7f) < .001f, "Long update catches up and preserves fractional remainder");
                var fine = new Enemy { renderer = enemy.renderer, animationFrames = visual.frames, frameSeconds = visual.frameSeconds };
                var coarse = new Enemy { renderer = enemy.renderer, animationFrames = visual.frames, frameSeconds = visual.frameSeconds };
                for (int i = 0; i < 137; i++) fine.AdvanceAnimation(.01f);
                coarse.AdvanceAnimation(1.37f);
                Check(fine.animationFrame == coarse.animationFrame && Mathf.Abs(fine.animationTime - coarse.animationTime) < .0001f,
                    "Animation timing is independent of frame rate");
                enemy.animationFrames = new[] { visual.frames[0], visual.frames[1] }; enemy.animationFrame = 0; enemy.animationTime = 0;
                enemy.AdvanceAnimation(visual.frameSeconds * 3);
                Check(enemy.animationFrame == 1, "Legacy two-frame fallback still loops");
                enemy.animationFrames = null; enemy.AdvanceAnimation(1);
                Check(enemy.animationFrame == 1, "Animator-owned sprites have no manual writer");
            }
            finally { Object.DestroyImmediate(actor); }
        }

        public static void RenderPreview()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory("Logs/MonsterAnimationFrames");
            var root = new GameObject("Monster animation preview");
            Camera camera = new GameObject("Preview camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 5.7f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .085f, .105f);
            var stages = Resources.LoadAll<StageDefinition>("ProjectDM/Stages").OrderBy(stage => stage.stageId).ToArray();
            var renderers = new SpriteRenderer[2, 5];
            for (int stage = 0; stage < 2; stage++) for (int slot = 0; slot < 5; slot++)
            {
                var actor = new GameObject(stages[stage].monsters[slot].displayName, typeof(SpriteRenderer));
                actor.transform.SetParent(root.transform); actor.transform.position = new Vector3(stage == 0 ? -2.4f : 2.4f, 4.3f - slot * 2.15f);
                actor.transform.localScale = Vector3.one * stages[stage].monsters[slot].visualScale * (slot == 4 ? .82f : slot == 3 ? .78f : .65f);
                renderers[stage, slot] = actor.GetComponent<SpriteRenderer>();
            }
            var target = new RenderTexture(800, 960, 24); var pixels = new Texture2D(800, 960, TextureFormat.RGB24, false);
            RenderTexture before = RenderTexture.active;
            try
            {
                for (int frame = 0; frame < 60; frame++)
                {
                    for (int stage = 0; stage < 2; stage++) for (int slot = 0; slot < 5; slot++)
                    {
                        var visual = stages[stage].monsters[slot];
                        renderers[stage, slot].sprite = visual.frames[Mathf.FloorToInt(frame / 24f / visual.frameSeconds) % visual.frames.Length];
                    }
                    camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 800, 960), 0, 0); pixels.Apply();
                    File.WriteAllBytes($"Logs/MonsterAnimationFrames/{frame:000}.png", pixels.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = before; camera.targetTexture = null; Object.DestroyImmediate(pixels);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(root);
            }
        }
    }
}
