using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectDM.Editor
{
    /// <summary>Imports the approved hooded mages without changing the legacy atlases or their identities.</summary>
    public static class ProjectDMCharacterArtSetup
    {
        public const string ArtFolder = "Assets/GameContent/Art/Characters";
        public const string AnimationFolder = "Assets/GameContent/Animation/Characters";
        public const string CatalogPath = "Assets/Resources/ProjectDM/Characters/CharacterVisuals.asset";
        public static readonly string[] Ids = { "projectile", "flame", "lightning", "frost" };
        public static readonly string[] Names = { "Missile", "Flame", "Lightning", "Frost" };
        public static readonly string[] Directions = { "Down", "Left", "Right", "Up" };

        [MenuItem("Project DM/Import Hooded Character Artwork")]
        public static void Install()
        {
            EnsureFolder(AnimationFolder);
            EnsureFolder("Assets/Resources/ProjectDM/Characters");
            CharacterVisuals catalog = AssetDatabase.LoadAssetAtPath<CharacterVisuals>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterVisuals>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var entries = new List<CharacterVisuals.Entry>();
            for (int i = 0; i < Ids.Length; i++)
            {
                string path = $"{ArtFolder}/ProjectDM_{Names[i]}_Hooded_Walk_v1.png";
                ImportSheet(path, Names[i]);
                Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
                string controllerPath = $"{AnimationFolder}/{Names[i]}.controller";
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)
                    ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                AnimatorStateMachine machine = controller.layers[0].stateMachine;
                Sprite first = sprites[$"{Names[i]}_Down_0"];
                AnimatorState idle = SetState(machine, "Idle_Down", MakeClip(Names[i] + "_Idle_Down", new[] { first }, 1));
                machine.defaultState = idle;
                foreach (string direction in Directions)
                {
                    Sprite[] frames = Enumerable.Range(0, 4).Select(frame => sprites[$"{Names[i]}_{direction}_{frame}"]).ToArray();
                    SetState(machine, "Walk_" + direction, MakeClip(Names[i] + "_Walk_" + direction, frames, 8));
                }
                EditorUtility.SetDirty(machine);
                EditorUtility.SetDirty(controller);
                entries.Add(new CharacterVisuals.Entry { characterId = Ids[i], idleSprite = first, controller = controller });
            }
            catalog.characters = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Hooded mages: four identities, 64 sliced walking frames, four controllers installed.");
        }

        private static void ImportSheet(string path, string name)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Invalid PNG: " + path);
                // Generated sheets have slightly uneven gutters. Slice their actual opaque islands,
                // rather than cutting off a hood or boot with a nominal equal-cell rectangle.
                List<RectInt> islands = FindFrames(source);
                if (islands.Count != 16) throw new InvalidOperationException($"Expected 16 isolated frames in {path}; found {islands.Count}.");
                islands = islands.OrderByDescending(rect => rect.center.y).ToList();
                var ordered = new List<RectInt>();
                for (int row = 0; row < 4; row++) ordered.AddRange(islands.Skip(row * 4).Take(4).OrderBy(rect => rect.center.x));
                float referenceHeight = ordered.Select(rect => rect.height).OrderBy(height => height).ElementAt(8);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = referenceHeight / 2.8f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.isReadable = false;
                TextureImporterSettings settings = new();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);
                var factories = new SpriteDataProviderFactories();
                factories.Init();
                ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                Dictionary<string, SpriteRect> previous = provider.GetSpriteRects().ToDictionary(rect => rect.name);
                var slices = new List<SpriteRect>();
                for (int index = 0; index < 16; index++)
                {
                    RectInt bounds = ordered[index];
                    string sliceName = $"{name}_{Directions[index / 4]}_{index % 4}";
                    // Head center keeps the off-center staff from shifting the body left/right.
                    float headCenter = HeadCenter(source, bounds);
                    Rect rect = new Rect(Mathf.Max(0, bounds.x - 2), Mathf.Max(0, bounds.y - 2),
                        Mathf.Min(source.width, bounds.xMax + 2) - Mathf.Max(0, bounds.x - 2),
                        Mathf.Min(source.height, bounds.yMax + 2) - Mathf.Max(0, bounds.y - 2));
                    slices.Add(new SpriteRect
                    {
                        name = sliceName, rect = rect, alignment = SpriteAlignment.Custom,
                        pivot = new Vector2((headCenter - rect.x) / rect.width,
                            (bounds.yMin + referenceHeight * .5f - rect.y) / rect.height),
                        spriteID = previous.TryGetValue(sliceName, out SpriteRect old) ? old.spriteID : GUID.Generate()
                    });
                }
                provider.SetSpriteRects(slices.ToArray());
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(
                    slices.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
                provider.Apply();
                importer.SaveAndReimport();
            }
            finally { Object.DestroyImmediate(source); }
        }

        private static float HeadCenter(Texture2D source, RectInt bounds)
        {
            int minimum = source.width, maximum = 0;
            for (int y = bounds.yMin + Mathf.RoundToInt(bounds.height * .65f); y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                    if (source.GetPixel(x, y).a > .8f) { minimum = Mathf.Min(minimum, x); maximum = Mathf.Max(maximum, x); }
            return (minimum + maximum) * .5f;
        }

        private static List<RectInt> FindFrames(Texture2D source)
        {
            int width = source.width, height = source.height;
            Color32[] pixels = source.GetPixels32();
            var visited = new bool[pixels.Length];
            var pending = new Queue<int>();
            var frames = new List<RectInt>();
            for (int pixel = 0; pixel < pixels.Length; pixel++)
            {
                if (visited[pixel] || pixels[pixel].a < 160) continue;
                visited[pixel] = true;
                pending.Enqueue(pixel);
                int area = 0, left = width, right = 0, bottom = height, top = 0;
                while (pending.Count > 0)
                {
                    int current = pending.Dequeue(), x = current % width, y = current / width;
                    area++; left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                    if (x > 0) Visit(current - 1);
                    if (x + 1 < width) Visit(current + 1);
                    if (y > 0) Visit(current - width);
                    if (y + 1 < height) Visit(current + width);
                }
                if (area > width * height / 1000) frames.Add(new RectInt(left, bottom, right - left + 1, top - bottom + 1));
            }
            return frames;

            void Visit(int pixel)
            {
                if (visited[pixel] || pixels[pixel].a < 160) return;
                visited[pixel] = true; pending.Enqueue(pixel);
            }
        }

        private static AnimationClip MakeClip(string name, Sprite[] frames, float frameRate)
        {
            string path = $"{AnimationFolder}/{name}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            clip.frameRate = frameRate;
            // Unity holds the last sprite key for one sample interval, then loopTime wraps it.
            var keys = Enumerable.Range(0, frames.Length).Select(index => new ObjectReferenceKeyframe
                { time = index / frameRate, value = frames[index] }).ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.stopTime = frames.Length / frameRate;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorState SetState(AnimatorStateMachine machine, string name, Motion clip)
        {
            AnimatorState state = machine.states.Select(child => child.state).FirstOrDefault(child => child.name == name) ?? machine.AddState(name);
            state.motion = clip;
            return state;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        public static void RunBatch()
        {
            try
            {
                Install();
                CharacterArtRegression.RunMenu();
                CharacterRegression.RunMenu();
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/CharacterArt-result.txt", "PASS: four hooded characters, alpha slices, animation loops, runtime selection and personal-magic regression.\n");
                CharacterArtRegression.Render();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/CharacterArt-result.txt", "FAIL: " + error);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}
