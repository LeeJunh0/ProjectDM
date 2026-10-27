using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Decorative, non-colliding scenery with batched, per-sprite occlusion fading.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class StageSceneryRenderer : MonoBehaviour
    {
        private StageSceneryLayout layout;
        private Bounds field;
        private SpriteQuadBatch scenery, flames;
        private readonly List<int> torchIndices = new();
        private sealed class Occluder
        {
            internal int handle, level = 16;
            internal Bounds bounds;
            internal Vector2 center, size;
            internal float cosine, sine;
            internal Sprite sprite;
            internal Color tint;
            internal float opacity = 1f;
        }
        private sealed class AlphaMask { internal Color32[] pixels; internal int width, height; }
        private readonly Dictionary<Texture2D, AlphaMask> alphaMasks = new();
        private readonly List<Occluder> occluders = new();
        private static Sprite glowSprite;
        public int RenderedCount => scenery?.Count ?? 0;
        public StageSceneryLayout Layout => layout;

        public void Configure(StageSceneryLayout source, Bounds fieldBounds)
        {
            layout = source; field = fieldBounds;
            if (layout == null) { Release(); return; }
            torchIndices.Clear();
            occluders.Clear();
            Shader shader = Resources.Load<Shader>("ProjectDM/SceneryDither");
            scenery ??= new SpriteQuadBatch("스테이지 장식 · 일괄 배치", 4, transform, gameObject.hideFlags, shader, true);
            scenery.Begin();
            for (int i = 0; i < layout.placements.Count; i++)
            {
                var item = layout.placements[i];
                var prop = layout.Find(item.propId);
                if (prop?.sprite == null) continue;
                if (prop.flame) torchIndices.Add(i);
                Vector2 size = SpriteSize(prop, item);
                Vector2 position = layout.ToWorld(item.position, field);
                int handle = scenery.Add(prop.sprite, position, size, item.rotation, prop.tint);
                float radians = item.rotation * Mathf.Deg2Rad;
                float width = Mathf.Abs(size.x), height = Mathf.Abs(size.y);
                float cos = Mathf.Abs(Mathf.Cos(radians)), sin = Mathf.Abs(Mathf.Sin(radians));
                occluders.Add(new Occluder { handle = handle, tint = prop.tint,
                    center = position, size = size, cosine = Mathf.Cos(radians), sine = Mathf.Sin(radians), sprite = prop.sprite,
                    bounds = new Bounds(position, new Vector3(width * cos + height * sin, width * sin + height * cos, 1)) });
                CacheAlpha(prop.sprite.texture);
            }
            scenery.End();
            flames ??= new SpriteQuadBatch("횃불 빛 · 불티", 5, transform, gameObject.hideFlags);
            flames.Clear();
            RenderFlames(Application.isPlaying ? Time.time : 0);
        }

        public static Vector2 SpriteSize(StageSceneryLayout.Prop prop, StageSceneryLayout.Placement item)
        {
            float height = prop.height * item.scale;
            return new Vector2(height * prop.sprite.rect.width / prop.sprite.rect.height * (item.flipX ? -1 : 1), height);
        }

        internal void UpdateOcclusion(SpriteRenderer player, float deltaTime, Camera view = null)
        {
            if (scenery == null || deltaTime <= 0) return;
            bool limitChecks = view != null && view.orthographic;
            float viewHeight = limitChecks ? view.orthographicSize * 2f : 0f;
            Bounds nearbyView = limitChecks
                ? new Bounds(new Vector3(view.transform.position.x, view.transform.position.y, 0),
                    new Vector3(viewHeight * view.aspect + 12f, viewHeight + 12f, 2f)) : default;
            foreach (var prop in occluders)
            {
                // This only skips overlap checks well outside the camera; every sprite remains rendered.
                if (limitChecks && !prop.bounds.Intersects(nearbyView)) continue;
                bool covered = player != null && player.enabled && CoversPoint(prop, player.bounds.center);
                prop.opacity = Mathf.MoveTowards(prop.opacity, covered ? .25f : 1f, deltaTime * 4f);
                int level = Mathf.RoundToInt(prop.opacity * 16f);
                if (level == prop.level) continue;
                prop.level = level;
                Color color = prop.tint; color.a *= level / 16f;
                scenery.SetColor(prop.handle, color);
            }
            scenery.CommitColors();
        }

        private void CacheAlpha(Texture2D texture)
        {
            if (texture == null || alphaMasks.ContainsKey(texture)) return;
            AlphaMask mask = new AlphaMask { width = texture.width, height = texture.height };
            if (texture.isReadable) mask.pixels = texture.GetPixels32();
            alphaMasks.Add(texture, mask);
        }

        private bool CoversPoint(Occluder prop, Vector3 playerCenter)
        {
            Vector2 relative = (Vector2)playerCenter - prop.center;
            float x = relative.x * prop.cosine + relative.y * prop.sine;
            float y = -relative.x * prop.sine + relative.y * prop.cosine;
            float u = x / prop.size.x + .5f, v = y / prop.size.y + .5f;
            if (u < 0 || u >= 1 || v < 0 || v >= 1) return false;
            if (!alphaMasks.TryGetValue(prop.sprite.texture, out var mask) || mask.pixels == null)
                return Mathf.Abs(u - .5f) < .3f && Mathf.Abs(v - .5f) < .3f;
            Rect rect = prop.sprite.textureRect;
            int px = Mathf.Clamp(Mathf.FloorToInt(rect.x + u * rect.width), 0, mask.width - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt(rect.y + v * rect.height), 0, mask.height - 1);
            return mask.pixels[py * mask.width + px].a >= 96;
        }

        private void Update()
        {
            if (Application.isPlaying) RenderFlames(Time.time);
        }

        public void RenderFlames(float time)
        {
            if (flames == null || layout == null || torchIndices.Count == 0) return;
            if (glowSprite == null)
            {
                var texture = GrowthTreeEffects.GlowTexture;
                glowSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, texture.width);
                glowSprite.name = "횃불 부드러운 빛";
                glowSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            flames.Begin();
            foreach (int i in torchIndices)
            {
                var item = layout.placements[i]; var prop = layout.Find(item.propId);
                if (prop == null || !prop.flame || prop.sprite == null) continue;
                Vector2 center = layout.ToWorld(item.position, field);
                float height = prop.height * item.scale;
                Vector2 flame = center + (Vector2)(Quaternion.Euler(0, 0, item.rotation) * Vector3.up) * height * .26f;
                float flicker = .92f + Mathf.Sin(time * 7.1f + i * 2.3f) * .05f + Mathf.Sin(time * 11.3f + i) * .03f;
                flames.Add(glowSprite, flame, Vector2.one * height * 1.7f * flicker, 0, new Color(1, .45f, .1f, .22f * flicker));
                flames.Add(glowSprite, flame, Vector2.one * height * .32f * flicker, 0, new Color(1, .78f, .3f, .48f));
                for (int spark = 0; spark < 2; spark++)
                {
                    float age = Mathf.Repeat(time * .42f + i * .37f + spark * .5f, 1);
                    Vector2 offset = new Vector2(Mathf.Sin(time * 2 + i + spark) * .07f, age * .4f) * height;
                    flames.Add(glowSprite, flame + offset, Vector2.one * height * .065f, 0, new Color(1, .6f, .16f, (1 - age) * .55f));
                }
            }
            flames.End();
        }

        private void Release() { scenery?.Dispose(); flames?.Dispose(); scenery = flames = null; }
        private void OnEnable() { if (layout != null) Configure(layout, field); }
        private void OnDisable() => Release();
    }
}
