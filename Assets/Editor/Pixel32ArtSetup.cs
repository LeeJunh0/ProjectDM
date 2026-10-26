using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectDM.Editor
{
    /// <summary>Deterministic native-resolution export after artwork editing; preserves sources, timing and world anchors.</summary>
    public static class Pixel32ArtSetup
    {
        public const int Size = 32;
        public const string Folder = "Assets/GameContent/Art/Pixel32";
        public const string CatalogPath = "Assets/Resources/ProjectDM/Pixel32Catalog.asset";
        public static string BossSource(string biome) => $"Assets/GameContent/Art/Monsters/ProjectDM_{biome}_Retro_BlackEyes_v3.png";
        private static bool applying;
        private sealed class Group { internal string name; internal Sprite[] frames; }
        private static Pixel32Catalog Catalog => AssetDatabase.LoadAssetAtPath<Pixel32Catalog>(CatalogPath);

        public static Sprite Resolve(Sprite source)
        {
            if (source == null || AssetDatabase.GetAssetPath(source).StartsWith(Folder + "/")) return source;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id)) return source;
            return Catalog?.entries.FirstOrDefault(entry => entry.sourceGuid == guid && entry.sourceFileId == id)?.sprite ?? source;
        }

        internal static Sprite Original(Sprite sprite)
        {
            var entry = Catalog?.entries.FirstOrDefault(item => item.sprite == sprite && item.sourceFileId != 0);
            if (entry == null) return sprite;
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(entry.sourceGuid)).OfType<Sprite>()
                .First(source => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string _, out long id) && id == entry.sourceFileId);
        }

        [MenuItem("Project DM/Export All Gameplay Artwork To Native 32px")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이 종료 후 실행하세요.");
            var groups = new List<Group>(); var seen = new HashSet<Sprite>();
            void Add(string name, IEnumerable<Sprite> sprites)
            {
                Sprite[] frames = sprites.Where(sprite => sprite != null).Select(Original).Distinct().Where(sprite => !seen.Contains(sprite)).ToArray();
                if (frames.Length == 0) return;
                foreach (Sprite frame in frames) seen.Add(frame);
                Group existing = groups.FirstOrDefault(group => group.name == name);
                if (existing != null) existing.frames = existing.frames.Concat(frames).ToArray();
                else groups.Add(new Group { name = name, frames = frames });
            }
            foreach (string name in GameplayArtPaths.CharacterNames) Add("Characters/" + name,
                Sprites($"{GameplayArtPaths.Characters}/ProjectDM_{name}_Hooded_Walk_v1.png"));
            foreach (string biome in GameplayArtPaths.Biomes)
            {
                Sprite[] sprites = Sprites(GameplayArtPaths.MonsterWalkSheet(biome));
                for (int row = 0; row < 5; row++) Add($"Monsters/{biome}/{row}", sprites.Where(sprite => sprite.name.StartsWith($"Walk_{biome}_{row}_")));
                Add($"Monsters/{biome}/Boss", Sprites(GameplayArtPaths.MonsterBossSheet(biome)).Where(sprite => sprite.name.StartsWith($"Retro_{biome}_5_")));
            }
            foreach (string path in new[] {
                "Assets/GameContent/Art/ProjectDM_ExperienceCurrency_5Types_4Frame_v1.png",
                "Assets/GameContent/Art/ProjectDM_Pickups_4Frame_v1.png",
                "Assets/GameContent/Art/ProjectDM_ManaPickup_v1.png",
                "Assets/GameContent/Art/ProjectDM_Sprites_TopDown_v2.png",
                "Assets/GameContent/Art/ProjectDM_ExtraMonsters_16Bit_v1.png" })
                foreach (var group in Sprites(path).GroupBy(sprite => Prefix(sprite.name))) Add("CollectiblesAndProjectiles/" + group.Key, group);
            var skills = Resources.Load<SkillVisualAssets>("ProjectDM/Skills/SkillVisuals");
            foreach (Sprite sprite in new[] { skills.chain, skills.splash, skills.meteor, skills.frostShard })
                Add("Skills/" + Original(sprite).name, new[] { sprite });
            // Legacy fallback controllers/catalogs remain usable at the same native resolution.
            foreach (AnimationClip clip in Clips())
                foreach (var group in AnimationUtility.GetObjectReferenceCurveBindings(clip)
                    .SelectMany(binding => AnimationUtility.GetObjectReferenceCurve(clip, binding)).Select(key => key.value).OfType<Sprite>()
                    .Select(Original).GroupBy(sprite => AssetDatabase.GetAssetPath(sprite)))
                    Add("Fallback/" + Path.GetFileNameWithoutExtension(group.Key), group);
            var spriteCatalog = AssetDatabase.LoadAssetAtPath<ProjectDMSpriteCatalog>("Assets/GameContent/Configuration/ProjectDMSpriteCatalog.asset");
            var serialized = new SerializedObject(spriteCatalog); var property = serialized.GetIterator();
            var fallback = new List<Sprite>();
            while (property.Next(true)) if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is Sprite sprite) fallback.Add(Original(sprite));
            foreach (var group in fallback.GroupBy(sprite => AssetDatabase.GetAssetPath(sprite))) Add("Fallback/" + Path.GetFileNameWithoutExtension(group.Key), group);

            EnsureFolder(Folder);
            Pixel32Catalog catalog = Catalog;
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<Pixel32Catalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            var entries = new List<Pixel32Catalog.Entry>();
            var sources = new Dictionary<string, Texture2D>();
            try
            {
                foreach (Group group in groups)
                {
                    string output = Folder + "/" + group.name; EnsureFolder(output);
                    Color32[] firstPose = null;
                    // A common square in WORLD coordinates preserves relative limb and foot motion.
                    float half = group.frames.Max(sprite => Mathf.Max(
                        Mathf.Max(sprite.pivot.x, sprite.rect.width - sprite.pivot.x),
                        Mathf.Max(sprite.pivot.y, sprite.rect.height - sprite.pivot.y)) / sprite.pixelsPerUnit);
                    float worldSize = half * 2 * 32f / 30f; // one native transparent pixel around the entire animation envelope
                    // The generated rear poses are larger than the front poses. Normalize only
                    // that direction, using one factor and one foot anchor for its whole cycle.
                    Sprite[] front = group.frames.Where(frame => frame.name.Contains("_Down_")).ToArray();
                    Sprite[] rear = group.frames.Where(frame => frame.name.Contains("_Up_")).ToArray();
                    float rearScale = 1f, rearFoot = 0f;
                    if (group.name.StartsWith("Characters/") && front.Length == 4 && rear.Length == 4)
                    {
                        float MedianHeight(Sprite[] frames) => frames.Select(frame => frame.rect.height - 4f).OrderBy(height => height).Skip(1).Take(2).Average();
                        rearScale = Mathf.Min(1f, MedianHeight(front) / MedianHeight(rear));
                        rearFoot = rear.Min(frame => (2f - frame.pivot.y) / frame.pixelsPerUnit);
                    }
                    foreach (Sprite source in group.frames)
                    {
                        string sourcePath = AssetDatabase.GetAssetPath(source);
                        string bossSource = source.name.StartsWith("Retro_Meadow_5_") ? BossSource("Meadow") :
                            source.name.StartsWith("Retro_Dungeon_5_") ? BossSource("Dungeon") : null;
                        string samplePath = bossSource != null && File.Exists(bossSource) ? bossSource : sourcePath;
                        if (!sources.TryGetValue(samplePath, out Texture2D raw))
                        {
                            raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                            if (!raw.LoadImage(File.ReadAllBytes(samplePath))) throw new InvalidOperationException("Invalid source " + samplePath);
                            sources.Add(samplePath, raw);
                        }
                        Color32[] pixels = raw.GetPixels32();
                        Color32[] result = new Color32[Size * Size];
                        float sourceX = (float)raw.width / source.texture.width, sourceY = (float)raw.height / source.texture.height;
                        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                        {
                            float localX = ((x + .5f) / Size - .5f) * worldSize;
                            float localY = ((y + .5f) / Size - .5f) * worldSize;
                            if (source.name.Contains("_Up_") && rearScale < 1f)
                            {
                                localX /= rearScale;
                                localY = rearFoot + (localY - rearFoot) / rearScale;
                            }
                            float sx = source.pivot.x + localX * source.pixelsPerUnit;
                            float sy = source.pivot.y + localY * source.pixelsPerUnit;
                            if (sx < 0 || sy < 0 || sx >= source.rect.width || sy >= source.rect.height) continue;
                            int px = Mathf.Clamp(Mathf.FloorToInt((source.rect.x + sx) * sourceX), 0, raw.width - 1);
                            int py = Mathf.Clamp(Mathf.FloorToInt((source.rect.y + sy) * sourceY), 0, raw.height - 1);
                            Color32 color = pixels[py * raw.width + px];
                            if (color.a < 128) continue;
                            color.a = 255; result[y * Size + x] = color;
                        }
                        if (group.name.StartsWith("Monsters/")) PreserveEyeGlints(source, raw, pixels, result, worldSize);
                        RectInt? fixedHead = group.name == "Monsters/Meadow/1" ? new RectInt(7, 8, 18, 12) :
                            group.name == "Monsters/Meadow/3" ? new RectInt(0, 13, 32, 19) : (RectInt?)null;
                        // These two approved frontal heads are immutable animation regions. Feet,
                        // arms outside the face and the sprout leaves retain their walking frames.
                        if (firstPose != null && fixedHead.HasValue)
                        {
                            RectInt region = fixedHead.Value;
                            for (int y = region.yMin; y < region.yMax; y++) for (int x = region.xMin; x < region.xMax; x++)
                                result[y * Size + x] = firstPose[y * Size + x];
                        }
                        if (firstPose == null) firstPose = (Color32[])result.Clone();
                        string path = output + "/" + source.name + ".png";
                        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                        bool changed;
                        try
                        {
                            texture.SetPixels32(result); texture.Apply(); byte[] png = texture.EncodeToPNG();
                            changed = !File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(png);
                            if (changed) File.WriteAllBytes(path, png);
                        }
                        finally { Object.DestroyImmediate(texture); }
                        Import(path, Size / worldSize, changed);
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id);
                        entries.Add(new Pixel32Catalog.Entry { key = group.name + "/" + source.name, sourceGuid = guid,
                            sourceFileId = id, sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path), worldSize = worldSize });
                    }
                }
            }
            finally { foreach (Texture2D raw in sources.Values) Object.DestroyImmediate(raw); }
            void Alias(string key, string spriteName)
            {
                var match = entries.First(entry => entry.sprite.name == spriteName);
                entries.Add(new Pixel32Catalog.Entry { key = key, sprite = match.sprite, worldSize = match.worldSize });
            }
            Alias("Runtime_Bolt", "Projectile_Bolt_0"); Alias("Runtime_Gem", "Experience_Gem"); Alias("Runtime_Coin", "Gold_Coin");
            for (int slot = 0; slot < 3; slot++) for (int frame = 0; frame < 2; frame++) Alias($"Fallback_{slot + 2}_{frame}", $"Existing_{slot}_{frame}");
            catalog.entries = entries.ToArray(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            ApplyInstalledArtwork();
            Debug.Log($"Native 32px: {entries.Count(entry => entry.sourceFileId != 0)} PNG frames exported and bound.");
        }

        internal static void ApplyInstalledArtwork()
        {
            if (applying || Catalog == null || Catalog.entries.Length == 0) return;
            applying = true;
            try
            {
                var skillArt = Resources.Load<SkillVisualAssets>("ProjectDM/Skills/SkillVisuals");
                Vector2 Anchor(Sprite source, Vector2 normalized)
                {
                    Sprite target = Resolve(source);
                    Vector2 world = (Vector2.Scale(normalized, source.rect.size) - source.pivot) / source.pixelsPerUnit;
                    return new Vector2(world.x / target.bounds.size.x + .5f, world.y / target.bounds.size.y + .5f);
                }
                if (skillArt != null)
                {
                    if (Resolve(skillArt.chain) != skillArt.chain)
                    { skillArt.chainStart = Anchor(skillArt.chain, skillArt.chainStart); skillArt.chainEnd = Anchor(skillArt.chain, skillArt.chainEnd); }
                    if (Resolve(skillArt.meteor) != skillArt.meteor)
                    {
                        skillArt.meteorAnchor = Anchor(skillArt.meteor, skillArt.meteorAnchor);
                        skillArt.meteorWidth *= Resolve(skillArt.meteor).bounds.size.x / skillArt.meteor.bounds.size.x;
                    }
                    if (Resolve(skillArt.splash) != skillArt.splash) skillArt.burstArtworkDiameter *= skillArt.splash.bounds.size.x / Resolve(skillArt.splash).bounds.size.x;
                    if (Resolve(skillArt.frostShard) != skillArt.frostShard) skillArt.frostArtworkDiameter *= skillArt.frostShard.bounds.size.x / Resolve(skillArt.frostShard).bounds.size.x;
                    EditorUtility.SetDirty(skillArt);
                }
                foreach (string path in new[] { "Assets/Resources/ProjectDM/Stages/Stage1_Meadow.asset", "Assets/Resources/ProjectDM/Stages/Stage2_Dungeon.asset",
                    GameplayArtPaths.CharacterCatalog, "Assets/Resources/ProjectDM/Skills/SkillVisuals.asset",
                    "Assets/GameContent/Configuration/ProjectDMSpriteCatalog.asset" })
                    ReplaceReferences(AssetDatabase.LoadMainAssetAtPath(path));
                foreach (AnimationClip clip in Clips())
                    foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    {
                        var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding); bool changed = false;
                        for (int i = 0; i < keys.Length; i++) if (keys[i].value is Sprite source)
                        { Sprite target = Resolve(source); if (target == source) continue; keys[i].value = target; changed = true; }
                        if (changed) { AnimationUtility.SetObjectReferenceCurve(clip, binding, keys); EditorUtility.SetDirty(clip); }
                    }
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { GameplayArtPaths.Prefabs }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid); GameObject root = PrefabUtility.LoadPrefabContents(path); bool changed = false;
                    try
                    {
                        foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                        { Sprite target = Resolve(renderer.sprite); if (target == renderer.sprite) continue; renderer.sprite = target; changed = true; }
                        foreach (PickupAmbientVfx effect in root.GetComponentsInChildren<PickupAmbientVfx>(true))
                        { effect.Configure(); changed = true; }
                        if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                AssetDatabase.SaveAssets();
            }
            finally { applying = false; }
        }

        private static void ReplaceReferences(Object target)
        {
            if (target == null) return;
            var serialized = new SerializedObject(target); var property = serialized.GetIterator(); bool changed = false;
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is Sprite sprite)
                { Sprite resolved = Resolve(sprite); if (resolved == sprite) continue; property.objectReferenceValue = resolved; changed = true; }
            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static Sprite[] Sprites(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();

        private static void PreserveEyeGlints(Sprite source, Texture2D raw, Color32[] pixels, Color32[] output, float worldSize)
        {
            float ratioX = (float)raw.width / source.texture.width, ratioY = (float)raw.height / source.texture.height;
            int left = Mathf.FloorToInt(source.rect.xMin * ratioX), bottom = Mathf.FloorToInt(source.rect.yMin * ratioY);
            int width = Mathf.Min(raw.width - left, Mathf.CeilToInt(source.rect.width * ratioX));
            int height = Mathf.Min(raw.height - bottom, Mathf.CeilToInt(source.rect.height * ratioY));
            var visited = new bool[width * height]; var pending = new Queue<int>();
            bool White(Color32 color) => color.a >= 200 && color.r >= 220 && color.g >= 220 && color.b >= 220;
            float sourcePixelsPerNativePixel = worldSize * source.pixelsPerUnit / Size;
            for (int index = 0; index < visited.Length; index++)
            {
                int originX = index % width, originY = index / width;
                if (visited[index] || !White(pixels[(bottom + originY) * raw.width + left + originX])) continue;
                visited[index] = true; pending.Enqueue(index);
                int count = 0, sumX = 0, sumY = 0, minX = width, maxX = 0, minY = height, maxY = 0;
                while (pending.Count > 0)
                {
                    int next = pending.Dequeue(), x = next % width, y = next / width;
                    count++; sumX += x; sumY += y; minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    if (x > 0) Visit(next - 1); if (x + 1 < width) Visit(next + 1);
                    if (y > 0) Visit(next - width); if (y + 1 < height) Visit(next + width);
                }
                // Retain tiny white islands inside black pupils as ONE native pixel. Large white
                // spots, tusks and the ghost body remain governed by normal nearest sampling.
                float area = count / (sourcePixelsPerNativePixel * sourcePixelsPerNativePixel * ratioX * ratioY);
                if (count < 3 || area > 1.8f) continue;
                int radius = Mathf.Max(2, Mathf.CeilToInt(Mathf.Sqrt(count)));
                int black = 0, visible = 0;
                for (int y = Math.Max(0, minY - radius); y <= Math.Min(height - 1, maxY + radius); y++)
                    for (int x = Math.Max(0, minX - radius); x <= Math.Min(width - 1, maxX + radius); x++)
                    {
                        Color32 color = pixels[(bottom + y) * raw.width + left + x];
                        if (color.a < 128) continue;
                        visible++; if (color.r < 65 && color.g < 65 && color.b < 65) black++;
                    }
                if (black < visible * .4f) continue;
                float sx = (left + (float)sumX / count + .5f) / ratioX - source.rect.x - source.pivot.x;
                float sy = (bottom + (float)sumY / count + .5f) / ratioY - source.rect.y - source.pivot.y;
                int nx = Mathf.FloorToInt((sx / source.pixelsPerUnit / worldSize + .5f) * Size);
                int ny = Mathf.FloorToInt((sy / source.pixelsPerUnit / worldSize + .5f) * Size);
                if (nx < 1 || ny < 1 || nx >= Size - 1 || ny >= Size - 1) continue;
                Color32 current = output[ny * Size + nx];
                int darkest = Math.Min(current.r, Math.Min(current.g, current.b)), brightest = Math.Max(current.r, Math.Max(current.g, current.b));
                if (current.a == 255 && brightest < 240 && brightest - darkest < 24) output[ny * Size + nx] = new Color32(240, 240, 240, 255);

                void Visit(int at)
                {
                    if (visited[at] || !White(pixels[(bottom + at / width) * raw.width + left + at % width])) return;
                    visited[at] = true; pending.Enqueue(at);
                }
            }
        }
        private static string Prefix(string name) => name.LastIndexOf('_') >= 0 && int.TryParse(name.Substring(name.LastIndexOf('_') + 1), out _) ? name.Substring(0, name.LastIndexOf('_')) : name;
        private static IEnumerable<AnimationClip> Clips() => AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/GameContent/Animation" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid)));
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static void Import(string path, float ppu, bool changed)
        {
            if (changed) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!changed && importer != null && Mathf.Approximately(importer.spritePixelsPerUnit, ppu) &&
                importer.filterMode == FilterMode.Point && !importer.mipmapEnabled && !importer.isReadable && importer.maxTextureSize == 32) return;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 32; importer.isReadable = false;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f); settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
    }
}
