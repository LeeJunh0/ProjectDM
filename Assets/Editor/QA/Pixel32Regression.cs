using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectDM.Editor
{
    public static class Pixel32Regression
    {
        private static int checks;
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); checks++; }
        private static bool Native(Sprite sprite) => sprite != null && sprite.rect.size == new Vector2(32, 32) &&
            sprite.texture.width == 32 && sprite.texture.height == 32 && AssetDatabase.GetAssetPath(sprite).StartsWith(Pixel32ArtSetup.Folder + "/");

        [MenuItem("Project DM/검증/전체 네이티브 32픽셀 리소스 검증")]
        public static void RunMenu()
        {
            checks = 0;
            Pixel32Catalog catalog = Resources.Load<Pixel32Catalog>("ProjectDM/Pixel32Catalog");
            Check(catalog != null && catalog.entries.Length > 120, "Complete native-resolution catalog");
            var native = catalog.entries.Where(entry => entry.sourceFileId != 0).ToArray();
            foreach (var entry in native)
            {
                Sprite sprite = entry.sprite;
                Check(Native(sprite), "Actual 32x32 PNG and sprite rectangle: " + entry.key);
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                Check(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled && !importer.isReadable &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed && importer.maxTextureSize == 32,
                    "No smoothing, mipmaps, compression or runtime readback");
                Check(Mathf.Abs(sprite.bounds.size.y - entry.worldSize) < .0001f && sprite.pivot == new Vector2(16, 16), "World scale and stable center pivot");
                Sprite source = Pixel32ArtSetup.Original(sprite);
                Check(source != sprite && Pixel32ArtSetup.Resolve(source) == sprite, "Stable source identity round-trip");
                var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    raw.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))); var pixels = raw.GetPixels32();
                    Check(raw.width == 32 && raw.height == 32 && pixels.Any(pixel => pixel.a == 255), "Real nonempty native raster");
                    Check(pixels.All(pixel => pixel.a == 0 || pixel.a == 255), "One RGBA color per native pixel; no antialiased edge pixels");
                    Check(Enumerable.Range(0, 32).All(i => pixels[i].a == 0 && pixels[31 * 32 + i].a == 0 &&
                        pixels[i * 32].a == 0 && pixels[i * 32 + 31].a == 0), "No clipped edges: " + entry.key);
                }
                finally { Object.DestroyImmediate(raw); }
            }
            foreach (StageDefinition stage in Resources.LoadAll<StageDefinition>("ProjectDM/Stages"))
            {
                Check(stage.monsters.All(monster => monster.frames.Length == 4 && monster.frames.All(Native)) && stage.bossFrames.All(Native),
                    "All live monsters, mini bosses and boss previews are native 32px");
                if (stage.stageId == 1) Check(!stage.monsters[1].mirrorToMovement && !stage.monsters[3].mirrorToMovement,
                    "Sprout and mushroom keep their authored face/cap orientation while moving left or right");
                if (stage.stageId == 1)
                    foreach (int slot in new[] { 1, 3 })
                    {
                        Color32[] first = null; RectInt region = slot == 1 ? new RectInt(7, 8, 18, 12) : new RectInt(0, 13, 32, 19);
                        var hashes = new System.Collections.Generic.HashSet<string>();
                        foreach (Sprite frame in stage.monsters[slot].frames)
                        {
                            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                            try
                            {
                                byte[] png = File.ReadAllBytes(AssetDatabase.GetAssetPath(frame)); image.LoadImage(png); Color32[] colors = image.GetPixels32();
                                hashes.Add(Convert.ToBase64String(png));
                                if (first == null) first = colors;
                                bool stable = true;
                                for (int y = region.yMin; y < region.yMax; y++) for (int x = region.xMin; x < region.xMax; x++)
                                    stable &= colors[y * 32 + x].Equals(first[y * 32 + x]);
                                Check(stable, "Immutable mushroom cap/right rim and sprout frontal face across the whole loop");
                            }
                            finally { Object.DestroyImmediate(image); }
                        }
                        Check(hashes.Count == 4, "Four distinct walking poses remain after fixing the head");
                    }
                if (stage.stageId == 1)
                    foreach (Sprite frame in stage.monsters[4].frames)
                    {
                        var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        try
                        {
                            image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(frame))); var colors = image.GetPixels32();
                            int glints = 0, pupils = 0;
                            for (int y = 15; y < 20; y++) for (int x = 5; x < 15; x++)
                            {
                                Color32 color = colors[y * 32 + x];
                                if (color.a < 128) continue;
                                if (color.r >= 230 && color.g >= 230 && color.b >= 230) glints++;
                                if (color.r < 75 && color.g < 75 && color.b < 75) pupils++;
                            }
                            Check(glints >= 2 && pupils >= 3, "Both black boar eyes keep their tiny white glints after native reduction");
                        }
                        finally { Object.DestroyImmediate(image); }
                    }
            }
            StageDefinition dungeon = Resources.LoadAll<StageDefinition>("ProjectDM/Stages").Single(stage => stage.stageId == 2);
            foreach (Sprite frame in dungeon.monsters[1].frames)
            {
                var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(frame))); var colors = image.GetPixels32();
                    int pinkEar = 0, earGlints = 0, eyeGlints = 0, pupils = 0;
                    for (int y = 14; y < 19; y++) for (int x = 12; x < 18; x++)
                    {
                        Color32 color = colors[y * 32 + x];
                        if (color.a < 128) continue;
                        if (color.r > 160 && color.r > color.g * 1.25f && color.b > color.g * .8f) pinkEar++;
                        if (color.r >= 230 && color.g >= 230 && color.b >= 230) earGlints++;
                    }
                    for (int y = 14; y < 17; y++) for (int x = 7; x < 11; x++)
                    {
                        Color32 color = colors[y * 32 + x];
                        if (color.a < 128) continue;
                        if (color.r >= 230 && color.g >= 230 && color.b >= 230) eyeGlints++;
                        if (color.r < 75 && color.g < 75 && color.b < 75) pupils++;
                    }
                    Check(pinkEar >= 4 && earGlints == 0, "Rat near ear stays pink, never becomes a glossy black eye: " + frame.name);
                    Check(eyeGlints >= 1 && pupils >= 2, "Rat actual eye remains black with a white glint: " + frame.name);
                }
                finally { Object.DestroyImmediate(image); }
            }
            foreach (CharacterVisuals.Entry entry in Resources.Load<CharacterVisuals>("ProjectDM/Characters/CharacterVisuals").characters)
                Check(Native(entry.idleSprite), "All four character identities are native 32px");
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/GameContent/Animation" }))
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    Check(AnimationUtility.GetObjectReferenceCurve(clip, binding).Where(key => key.value is Sprite).All(key => Native((Sprite)key.value)),
                        "Every animation key uses a 32px frame: " + clip.name);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ProjectDMPrefabFactory.PrefabFolder }))
                foreach (var renderer in AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)).GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer.sprite != null) Check(Native(renderer.sprite), "Prefab artwork is native 32px: " + renderer.name);
            SkillVisualAssets skills = Resources.Load<SkillVisualAssets>("ProjectDM/Skills/SkillVisuals");
            Check(new[] { skills.chain, skills.splash, skills.meteor, skills.frostShard }.All(Native), "All dedicated skill images are native 32px");
            Check(new[] { "Runtime_Bolt", "Runtime_Gem", "Runtime_Coin" }.All(key => Native(catalog.Find(key))), "Runtime fallback slices resolve to native images");

            string[] ids = native.Select(entry => GlobalObjectId.GetGlobalObjectIdSlow(entry.sprite).ToString()).OrderBy(id => id).ToArray();
            string beforeSkills = EditorJsonUtility.ToJson(skills);
            Pixel32ArtSetup.Install();
            var after = Resources.Load<Pixel32Catalog>("ProjectDM/Pixel32Catalog");
            Check(ids.SequenceEqual(after.entries.Where(entry => entry.sourceFileId != 0).Select(entry => GlobalObjectId.GetGlobalObjectIdSlow(entry.sprite).ToString()).OrderBy(id => id)),
                "Repeated export preserves every native sprite GUID/fileID");
            Check(beforeSkills == EditorJsonUtility.ToJson(skills), "Repeated export does not drift lightning anchors, hit diameters or meteor geometry");
            Debug.Log($"Native 32px: {checks} assertions PASS ({native.Length} PNG frames, full bindings and idempotent export).");
        }

        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Native 32px resource review");
            var camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 7.8f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .105f, .12f);
            void Show(Sprite sprite, float x, float y, float width = 2)
            {
                var actor = new GameObject(sprite.name, typeof(SpriteRenderer)); actor.transform.SetParent(root.transform);
                actor.transform.position = new Vector3(x, y); actor.transform.localScale = Vector3.one * width / sprite.bounds.size.x;
                actor.GetComponent<SpriteRenderer>().sprite = sprite;
            }
            var characters = Resources.Load<CharacterVisuals>("ProjectDM/Characters/CharacterVisuals").characters;
            for (int i = 0; i < 4; i++) Show(characters[i].idleSprite, (i - 1.5f) * 2.8f, 6);
            var stages = Resources.LoadAll<StageDefinition>("ProjectDM/Stages").OrderBy(stage => stage.stageId).ToArray();
            for (int stage = 0; stage < 2; stage++) for (int slot = 0; slot < 5; slot++)
                Show(stages[stage].monsters[slot].frames[0], (slot - 2) * 2.5f, 3.4f - stage * 2.3f);
            for (int stage = 0; stage < 2; stage++) Show(stages[stage].bossFrames[0], stage == 0 ? -3 : 3, -1.3f, 2.3f);
            var skills = Resources.Load<SkillVisualAssets>("ProjectDM/Skills/SkillVisuals"); var effects = new[] { skills.chain, skills.splash, skills.meteor, skills.frostShard };
            for (int i = 0; i < 4; i++) Show(effects[i], (i - 1.5f) * 2.8f, -3.7f, 1.8f);
            var drops = Enumerable.Range(1, 5).Select(i => $"ExperiencePickup_{i:00}").Concat(Enumerable.Range(1, 5).Select(i => $"CurrencyPickup_{i:00}"))
                .Concat(new[] { "ManaPickup", "ChestPickup" }).ToArray();
            for (int i = 0; i < drops.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ProjectDMPrefabFactory.PrefabFolder}/{drops[i]}.prefab");
                Show(prefab.GetComponentInChildren<SpriteRenderer>().sprite, (i - 5.5f) * 1.05f, -6, .95f);
            }
            var target = new RenderTexture(1000, 1200, 24); var pixels = new Texture2D(1000, 1200, TextureFormat.RGB24, false);
            var before = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1000, 1200), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/Pixel32-resources.png", pixels.EncodeToPNG());
            }
            finally
            { RenderTexture.active = before; camera.targetTexture = null; Object.DestroyImmediate(pixels); target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(root); }
        }
    }
}
