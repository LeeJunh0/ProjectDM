using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Pooled approved skill artwork; all timing/motion is presentation-only, with legacy fallbacks.</summary>
    internal sealed class PrototypeSkillVisuals : MonoBehaviour
    {
        private readonly Queue<SpriteRenderer> spritePool = new();
        private readonly List<Visual> active = new();
        private readonly Queue<TextMesh> textPool = new();
        private readonly List<FloatingText> texts = new();
        private readonly List<SpriteRenderer> frostShards = new();
        private Sprite bolt, gem;
        private SkillVisualAssets artwork;
        private Material defaultSpriteMaterial;

        private sealed class Visual
        {
            public SpriteRenderer renderer;
            public Color color;
            public Vector3 from, to, size;
            public float age, duration, expansionDuration, startScale = .2f;
            public bool falling, expanding, lightning, warning;
        }

        private sealed class FloatingText
        {
            public TextMesh mesh;
            public Vector3 origin;
            public float age;
        }

        internal void Configure(Sprite boltSprite, Sprite gemSprite, SkillVisualAssets assets = null)
        {
            bolt = boltSprite; gem = gemSprite; artwork = assets;
        }

        private SpriteRenderer Rent(Sprite sprite, bool warmTint = false)
        {
            if (active.Count + frostShards.Count >= (artwork != null ? Mathf.Max(1, artwork.maxActiveEffects) : 128)) return null;
            SpriteRenderer renderer;
            if (spritePool.Count > 0) renderer = spritePool.Dequeue();
            else
            {
                GameObject instance = new GameObject("스킬 이펙트");
                instance.transform.SetParent(transform, false);
                // Separate connection-axis scale from the diagonal artwork's local rotation.
                // This lets lightning widen without moving either authored endpoint.
                GameObject spriteObject = new GameObject("스프라이트");
                spriteObject.transform.SetParent(instance.transform, false);
                renderer = spriteObject.AddComponent<SpriteRenderer>();
                defaultSpriteMaterial ??= renderer.sharedMaterial;
            }
            renderer.sprite = sprite;
            renderer.sharedMaterial = warmTint && artwork != null && artwork.impactMaterial != null
                ? artwork.impactMaterial : defaultSpriteMaterial;
            renderer.sortingOrder = 6;
            Transform frame = renderer.transform.parent;
            frame.localPosition = Vector3.zero;
            frame.localRotation = Quaternion.identity;
            frame.localScale = Vector3.one;
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.rotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one;
            renderer.color = Color.white;
            renderer.flipX = renderer.flipY = false;
            renderer.gameObject.SetActive(true);
            return renderer;
        }

        private static Vector3 SpriteScale(Sprite sprite, float width, float height)
        {
            Vector2 size = sprite != null ? sprite.bounds.size : Vector2.one;
            return new Vector3(width / Mathf.Max(.01f, size.x), height / Mathf.Max(.01f, size.y), 1);
        }

        internal void BeginFrostOrbit(int count, float radius)
        {
            EndFrostOrbit();
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer renderer = Rent(gem ?? bolt);
                if (renderer == null) break; // Visual budget never changes combat's shard count.
                renderer.transform.localScale = SpriteScale(renderer.sprite, radius * 2, radius * 2);
                renderer.color = new Color(.65f, .95f, 1, .95f);
                frostShards.Add(renderer);
            }
        }
        internal void MoveFrostShard(int index, Vector3 position, float angle)
        {
            if (index >= frostShards.Count) return;
            frostShards[index].transform.position = position;
            frostShards[index].transform.rotation = Quaternion.Euler(0, 0, angle);
        }
        internal void EndFrostOrbit()
        {
            foreach (SpriteRenderer shard in frostShards) { shard.gameObject.SetActive(false); spritePool.Enqueue(shard); }
            frostShards.Clear();
        }

        internal void Link(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .0001f) return;
            if (artwork != null && artwork.chain != null)
            {
                Sprite sprite = artwork.chain;
                Vector3 localStart = Anchor(sprite, artwork.chainStart), localEnd = Anchor(sprite, artwork.chainEnd);
                Vector3 authoredDirection = localEnd - localStart;
                if (authoredDirection.sqrMagnitude > .0001f)
                {
                    SpriteRenderer renderer = Rent(sprite);
                    if (renderer == null) return;
                    float scale = delta.magnitude / authoredDirection.magnitude;
                    Quaternion artworkRotation = Quaternion.Euler(0, 0,
                        -Mathf.Atan2(authoredDirection.y, authoredDirection.x) * Mathf.Rad2Deg);
                    Transform frame = renderer.transform.parent;
                    frame.position = from;
                    frame.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    frame.localScale = new Vector3(scale, scale * Mathf.Max(.1f, artwork.chainWidthMultiplier), 1);
                    renderer.transform.localRotation = artworkRotation;
                    renderer.transform.localPosition = -(artworkRotation * localStart);
                    Color tint = new Color(1, 1, 1, Mathf.Clamp01(artwork.chainOpacity));
                    renderer.color = tint;
                    active.Add(new Visual { renderer = renderer, color = tint,
                        size = renderer.transform.localScale, duration = Mathf.Max(.01f, artwork.chainDuration), lightning = true });
                    return;
                }
            }
            // Three connected strokes create a readable zigzag with the default projectile sprite.
            Vector3 direction = to - from;
            Vector3 perpendicular = new Vector3(-direction.y, direction.x).normalized * .14f;
            Vector3 a = from + direction / 3f + perpendicular;
            Vector3 b = from + direction * (2f / 3f) - perpendicular;
            Stroke(from, a); Stroke(a, b); Stroke(b, to);
        }

        private void Stroke(Vector3 from, Vector3 to)
        {
            SpriteRenderer renderer = Rent(bolt);
            if (renderer == null) return;
            Vector3 direction = to - from;
            renderer.transform.position = (from + to) * .5f;
            renderer.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            Vector3 size = SpriteScale(bolt, Mathf.Max(.05f, direction.magnitude), .1f);
            renderer.transform.localScale = size;
            Color color = new Color(.7f, .85f, 1f);
            renderer.color = color;
            active.Add(new Visual { renderer = renderer, color = color, size = size, duration = .18f });
        }

        // Placeholder fan made from the existing projectile sprite. Its rays use the actual hit range/angle.
        internal void FlameCone(Vector3 origin, Vector2 direction, float range, float fullAngle)
        {
            float heading = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            for (int i = 0; i <= 8; i++)
            {
                SpriteRenderer renderer = Rent(bolt, true);
                if (renderer == null) break;
                float angle = heading - fullAngle * .5f + fullAngle * i / 8f;
                Vector3 ray = Quaternion.Euler(0, 0, angle) * Vector3.right;
                renderer.transform.position = origin + ray * range * .5f;
                renderer.transform.rotation = Quaternion.Euler(0, 0, angle);
                Vector3 size = SpriteScale(bolt, range, .16f);
                renderer.transform.localScale = size;
                Color color = new Color(1, .35f, .06f, .65f);
                renderer.color = color;
                active.Add(new Visual { renderer = renderer, color = color, size = size, duration = .22f });
            }
        }

        internal void Burst(Vector3 center, float radius, Color color, float duration)
        {
            bool generated = artwork != null && artwork.splash != null;
            Expand(center, radius, generated ? artwork.splash : gem,
                generated ? new Color(1, 1, 1, Mathf.Clamp01(artwork.splashOpacity)) : new Color(color.r, color.g, color.b, .35f),
                generated ? artwork.splashDuration : duration, false);
        }

        internal void MeteorImpact(Vector3 center, float radius)
        {
            bool generated = artwork != null && artwork.splash != null && artwork.impactMaterial != null;
            Color color = generated ? artwork.impactColor : new Color(1, .45f, .12f);
            color.a = generated ? Mathf.Clamp01(artwork.impactOpacity) : .35f;
            Expand(center, radius, generated ? artwork.splash : gem, color,
                generated ? artwork.impactDuration : .4f, generated);
        }

        private void Expand(Vector3 center, float radius, Sprite sprite, Color color, float duration, bool warmTint)
        {
            SpriteRenderer renderer = Rent(sprite, warmTint);
            if (renderer == null) return;
            renderer.transform.position = center;
            float diameter = artwork != null && sprite == artwork.splash ? Mathf.Clamp(artwork.burstArtworkDiameter, .1f, 1) : 1;
            Vector3 size = SpriteScale(sprite, radius * 2 / diameter, radius * 2 / diameter);
            float start = artwork != null ? Mathf.Clamp(artwork.burstStartScale, .01f, 1) : .2f;
            renderer.transform.localScale = size * start;
            renderer.color = color;
            float lifetime = Mathf.Max(.01f, duration);
            active.Add(new Visual { renderer = renderer, color = color, size = size,
                duration = lifetime, expansionDuration = Mathf.Clamp(artwork != null ? artwork.burstExpansionDuration : .06f,
                    .001f, lifetime * .5f), expanding = true, startScale = start });
        }

        internal void Meteor(Vector3 center, float radius, float delay)
        {
            // Motion uses the existing damage deadline, not a second combat timer.
            bool generated = artwork != null && artwork.meteor != null;
            bool warmMarker = generated && artwork.splash != null && artwork.impactMaterial != null;
            Sprite markerSprite = warmMarker ? artwork.splash : gem;
            SpriteRenderer marker = Rent(markerSprite, warmMarker);
            if (marker != null)
            {
                marker.transform.position = center;
                float diameter = warmMarker ? Mathf.Clamp(artwork.burstArtworkDiameter, .1f, 1) : 1;
                Vector3 markerSize = SpriteScale(markerSprite, radius * 2 / diameter, radius * 2 / diameter);
                marker.transform.localScale = markerSize;
                marker.sortingOrder = 1;
                Color warning = generated ? artwork.impactColor : new Color(1, .4f, .1f);
                warning.a = generated ? Mathf.Clamp01(artwork.warningOpacity) : .22f;
                marker.color = warning;
                active.Add(new Visual { renderer = marker, color = warning, size = markerSize,
                    duration = Mathf.Max(.01f, delay), warning = true });
            }
            Sprite sprite = generated ? artwork.meteor : bolt;
            SpriteRenderer falling = Rent(sprite);
            if (falling == null) return;
            falling.transform.rotation = Quaternion.Euler(0, 0, generated ? artwork.meteorRotation : -90);
            float width = generated ? Mathf.Max(.01f, artwork.meteorWidth) : .9f;
            float height = generated ? width * sprite.rect.height / sprite.rect.width : .55f;
            Vector3 size = SpriteScale(sprite, width, height);
            falling.transform.localScale = size;
            Vector3 offset = generated ? falling.transform.rotation * Vector3.Scale(Anchor(sprite, artwork.meteorAnchor), size) : Vector3.zero;
            Vector3 end = center - offset;
            falling.transform.position = end + Vector3.up * (generated ? Mathf.Max(0, artwork.meteorFallHeight) : 3);
            falling.color = generated ? Color.white : new Color(1f, .55f, .2f);
            active.Add(new Visual { renderer = falling, color = falling.color, size = size,
                from = falling.transform.position, to = end, duration = Mathf.Max(.01f, delay), falling = true });
        }

        // Normalized anchors remain correct after import resizing and with non-central sprite pivots.
        internal static Vector3 Anchor(Sprite sprite, Vector2 normalized) => new(
            (normalized.x * sprite.rect.width - sprite.pivot.x) / sprite.pixelsPerUnit,
            (normalized.y * sprite.rect.height - sprite.pivot.y) / sprite.pixelsPerUnit, 0);

        internal void ManaAbsorb(Vector3 position, float amount)
        {
            TextMesh text;
            if (textPool.Count > 0) text = textPool.Dequeue();
            else
            {
                GameObject instance = new GameObject("마나 흡수 숫자");
                instance.transform.SetParent(transform, false);
                text = instance.AddComponent<TextMesh>();
                text.fontSize = 40;
                text.characterSize = .045f;
                text.anchor = TextAnchor.MiddleCenter;
                text.GetComponent<MeshRenderer>().sortingOrder = 10;
            }
            text.gameObject.SetActive(true);
            text.text = "+" + amount.ToString("0.##") + " MP";
            text.color = new Color(.4f, .8f, 1f);
            text.transform.position = position + Vector3.up * .45f;
            texts.Add(new FloatingText { mesh = text, origin = text.transform.position });
        }

        internal void Tick(float dt)
        {
            if (dt <= 0) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Visual visual = active[i];
                visual.age += dt;
                float progress = Mathf.Clamp01(visual.age / visual.duration);
                if (visual.falling) visual.renderer.transform.position = Vector3.Lerp(visual.from, visual.to, progress * progress);
                if (visual.expanding)
                {
                    float expansion = Mathf.Clamp01(visual.age / visual.expansionDuration);
                    visual.renderer.transform.localScale = visual.size * Mathf.Lerp(visual.startScale, 1, 1 - Mathf.Pow(1 - expansion, 3));
                }
                Color color = visual.color;
                if (visual.warning) color.a *= .55f + .45f * progress;
                else if (visual.expanding) color.a *= 1 - Mathf.Clamp01((visual.age - visual.expansionDuration) /
                    Mathf.Max(.001f, visual.duration - visual.expansionDuration));
                else if (!visual.falling) color.a *= 1f - progress;
                if (visual.lightning) color.a *= .7f + .3f * Mathf.Abs(Mathf.Sin(visual.age * 90));
                visual.renderer.color = color;
                if (progress < 1f) continue;
                visual.renderer.gameObject.SetActive(false);
                spritePool.Enqueue(visual.renderer);
                active.RemoveAt(i);
            }
            for (int i = texts.Count - 1; i >= 0; i--)
            {
                FloatingText text = texts[i];
                text.age += dt;
                text.mesh.transform.position = text.origin + Vector3.up * text.age * .7f;
                text.mesh.color = new Color(.4f, .8f, 1f, Mathf.Clamp01(1f - text.age / .7f));
                if (text.age < .7f) continue;
                text.mesh.gameObject.SetActive(false);
                textPool.Enqueue(text.mesh);
                texts.RemoveAt(i);
            }
        }

        internal void Clear()
        {
            EndFrostOrbit();
            foreach (Visual visual in active) { visual.renderer.gameObject.SetActive(false); spritePool.Enqueue(visual.renderer); }
            active.Clear();
            foreach (FloatingText text in texts) { text.mesh.gameObject.SetActive(false); textPool.Enqueue(text.mesh); }
            texts.Clear();
        }
    }
}
