using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Temporary pooled effects using the existing bolt/gem sprites, not generated artwork.</summary>
    internal sealed class PrototypeSkillVisuals : MonoBehaviour
    {
        private readonly Queue<SpriteRenderer> spritePool = new();
        private readonly List<Visual> active = new();
        private readonly Queue<TextMesh> textPool = new();
        private readonly List<FloatingText> texts = new();
        private Sprite bolt, gem;

        private sealed class Visual
        {
            public SpriteRenderer renderer;
            public Color color;
            public Vector3 from, to, size;
            public float age, duration;
            public bool falling, expanding;
        }

        private sealed class FloatingText
        {
            public TextMesh mesh;
            public Vector3 origin;
            public float age;
        }

        internal void Configure(Sprite boltSprite, Sprite gemSprite) { bolt = boltSprite; gem = gemSprite; }

        private SpriteRenderer Rent(Sprite sprite)
        {
            SpriteRenderer renderer;
            if (spritePool.Count > 0) renderer = spritePool.Dequeue();
            else
            {
                GameObject instance = new GameObject("임시 스킬 연출");
                instance.transform.SetParent(transform, false);
                renderer = instance.AddComponent<SpriteRenderer>();
            }
            renderer.sprite = sprite;
            renderer.sortingOrder = 6;
            renderer.transform.rotation = Quaternion.identity;
            renderer.gameObject.SetActive(true);
            return renderer;
        }

        private static Vector3 SpriteScale(Sprite sprite, float width, float height)
        {
            Vector2 size = sprite != null ? sprite.bounds.size : Vector2.one;
            return new Vector3(width / Mathf.Max(.01f, size.x), height / Mathf.Max(.01f, size.y), 1);
        }

        internal void Link(Vector3 from, Vector3 to)
        {
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
            Vector3 direction = to - from;
            renderer.transform.position = (from + to) * .5f;
            renderer.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            Vector3 size = SpriteScale(bolt, Mathf.Max(.05f, direction.magnitude), .1f);
            renderer.transform.localScale = size;
            Color color = new Color(.7f, .85f, 1f);
            renderer.color = color;
            active.Add(new Visual { renderer = renderer, color = color, size = size, duration = .18f });
        }

        internal void Burst(Vector3 center, float radius, Color color, float duration)
        {
            // Default gem flash marks the damaged area. Replace with effect art later without changing combat.
            SpriteRenderer renderer = Rent(gem);
            renderer.transform.position = center;
            Vector3 size = SpriteScale(gem, radius * 2, radius * 2);
            renderer.transform.localScale = size * .2f;
            color.a = .35f;
            renderer.color = color;
            active.Add(new Visual { renderer = renderer, color = color, size = size, duration = duration, expanding = true });
        }

        internal void Meteor(Vector3 center, float radius, float delay)
        {
            // A low-opacity ground marker and a falling default bolt, synchronized with the damage deadline.
            SpriteRenderer marker = Rent(gem);
            marker.transform.position = center;
            Vector3 markerSize = SpriteScale(gem, radius * 2, radius * 2);
            marker.transform.localScale = markerSize;
            Color warning = new Color(1f, .4f, .1f, .22f);
            marker.color = warning;
            active.Add(new Visual { renderer = marker, color = warning, size = markerSize, duration = Mathf.Max(.01f, delay) });
            SpriteRenderer falling = Rent(bolt);
            falling.transform.rotation = Quaternion.Euler(0, 0, -90);
            Vector3 size = SpriteScale(bolt, .9f, .55f);
            falling.transform.localScale = size;
            falling.transform.position = center + Vector3.up * 3f;
            falling.color = new Color(1f, .55f, .2f);
            active.Add(new Visual { renderer = falling, color = falling.color, size = size,
                from = falling.transform.position, to = center, duration = Mathf.Max(.01f, delay), falling = true });
        }

        internal void TimeReward(Vector3 position, float seconds)
        {
            TextMesh text;
            if (textPool.Count > 0) text = textPool.Dequeue();
            else
            {
                GameObject instance = new GameObject("시간 보충 숫자");
                instance.transform.SetParent(transform, false);
                text = instance.AddComponent<TextMesh>();
                text.fontSize = 40;
                text.characterSize = .045f;
                text.anchor = TextAnchor.MiddleCenter;
                text.GetComponent<MeshRenderer>().sortingOrder = 10;
            }
            text.gameObject.SetActive(true);
            text.text = "+" + seconds.ToString("0.##") + "s";
            text.color = new Color(.65f, 1f, .65f);
            text.transform.position = position + Vector3.up * .45f;
            texts.Add(new FloatingText { mesh = text, origin = text.transform.position });
        }

        internal void Tick(float dt)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Visual visual = active[i];
                visual.age += dt;
                float progress = Mathf.Clamp01(visual.age / visual.duration);
                if (visual.falling) visual.renderer.transform.position = Vector3.Lerp(visual.from, visual.to, progress * progress);
                if (visual.expanding) visual.renderer.transform.localScale = visual.size * Mathf.Lerp(.2f, 1, progress);
                Color color = visual.color;
                if (!visual.falling) color.a *= 1f - progress;
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
                text.mesh.color = new Color(.65f, 1f, .65f, Mathf.Clamp01(1f - text.age / .7f));
                if (text.age < .7f) continue;
                text.mesh.gameObject.SetActive(false);
                textPool.Enqueue(text.mesh);
                texts.RemoveAt(i);
            }
        }

        internal void Clear()
        {
            foreach (Visual visual in active) { visual.renderer.gameObject.SetActive(false); spritePool.Enqueue(visual.renderer); }
            active.Clear();
            foreach (FloatingText text in texts) { text.mesh.gameObject.SetActive(false); textPool.Enqueue(text.mesh); }
            texts.Clear();
        }
    }
}
