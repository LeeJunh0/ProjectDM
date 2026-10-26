using System;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Decorative, unscaled-time UI effects. No scene objects or gameplay randomness.</summary>
    internal static class GrowthTreeEffects
    {
        static Texture2D glow;
        internal static string Character(string id) => id == "flame" || id == "lightning" || id == "frost" ? id : "projectile";
        internal static Color Accent(string id) => Character(id) switch
        { "flame" => new(1f, .38f, .12f), "lightning" => new(.72f, .58f, 1f), "frost" => new(.42f, .85f, 1f), _ => new(.96f, .8f, .47f) };

        internal static Texture2D GlowTexture
        {
            get
            {
                if (glow != null) return glow;
                const int size = 64;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float distance = new Vector2((x + .5f) / size * 2 - 1, (y + .5f) / size * 2 - 1).magnitude;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - distance), 2.4f));
                }
                glow = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "Growth soft light", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                glow.SetPixels(pixels); glow.Apply(); return glow;
            }
        }

        internal static void DrawSymbol(Vector2 center, float zoom, string character, Color accent, float time, Texture bottle, float completion)
        {
            if (Event.current?.type != EventType.Repaint || bottle == null) return;
            // 1.5x the original size, with a readable minimum when viewing the whole map.
            float scale = Mathf.Clamp(zoom * 1.45f, .65f, 1.2f), radius = 72f * scale;
            float breath = 1f + Mathf.Sin(time * 1.8f) * .045f;
            float completedGlow = completion >= 1 ? .34f + Mathf.Sin(time * 1.6f) * .045f : 0;
            Light(center + Vector2.down * radius * .15f, radius * (1.65f + completedGlow) * breath, WithAlpha(accent, .23f + completedGlow));
            // Completed potions gain a stronger colored halo around the clear central emblem.
            Image(center + Vector2.up * (Mathf.Sin(time * 1.2f) * 1.5f * scale), radius * 1.28f, bottle, Color.white, 0);
        }

        internal static float PathLength(Vector2[] points)
        {
            float length = 0;
            if (points != null) for (int i = 1; i < points.Length; i++) length += Vector2.Distance(points[i - 1], points[i]);
            return length;
        }

        internal static Vector2 PointOnPath(Vector2[] points, float distance)
        {
            if (points == null || points.Length == 0) return Vector2.zero;
            distance = Mathf.Max(0, distance);
            for (int i = 1; i < points.Length; i++)
            {
                float segment = Vector2.Distance(points[i - 1], points[i]);
                if (segment < .0001f) continue;
                if (distance <= segment) return Vector2.Lerp(points[i - 1], points[i], distance / segment);
                distance -= segment;
            }
            return points[points.Length - 1];
        }

        internal static float InwardDistance(float time, float length, int pulse, int count, float scale, float seed)
        {
            return length > .001f ? Mathf.Repeat((pulse + seed) * length / Mathf.Max(1, count) - time * 70f * scale, length) : 0;
        }

        // Paths run from the point closer to ROOT to the outer node. Highlights travel backward.
        internal static void DrawInwardFlow(Vector2[] points, Color accent, float zoom, float time, float seed)
        {
            if (Event.current?.type != EventType.Repaint) return;
            float length = PathLength(points), scale = Mathf.Clamp(zoom, .08f, 1.2f);
            if (length < 2) return;
            int count = Mathf.Clamp(Mathf.CeilToInt(length / (95f * scale)), 1, 8);
            for (int i = 0; i < count; i++)
            {
                float distance = InwardDistance(time, length, i, count, scale, seed);
                float fade = Mathf.Clamp01(distance / (8 * scale)) * Mathf.Clamp01((length - distance) / (8 * scale));
                for (int tail = 3; tail >= 0; tail--)
                {
                    float start = Mathf.Min(length, distance + tail * 5 * scale), end = Mathf.Min(length, start + 5 * scale);
                    Line(PointOnPath(points, start), PointOnPath(points, end), WithAlpha(accent, fade * (1 - tail * .23f) * .8f), 2.5f * scale);
                }
                Vector2 head = PointOnPath(points, distance);
                Light(head, 6f * scale, WithAlpha(accent, fade));
                Light(head, 2.5f * scale, WithAlpha(Color.white, fade));
            }
        }

        static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }
        static void Light(Vector2 center, float radius, Color color) => Image(center, radius, GlowTexture, color, 0);
        static void Image(Vector2 center, float radius, Texture texture, Color color, float rotation)
        {
            Matrix4x4 matrix = GUI.matrix; Color previous = GUI.color;
            try
            {
                GUI.color = color;
                if (rotation != 0) GUIUtility.RotateAroundPivot(rotation, center);
                GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), texture);
            }
            finally { GUI.color = previous; GUI.matrix = matrix; }
        }
        static void Line(Vector2 from, Vector2 to, Color color, float width)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < .0001f) return;
            Matrix4x4 matrix = GUI.matrix;
            try
            {
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
                UiGraphics.DrawSolidRect(new Rect(from.x, from.y - width * .5f, delta.magnitude, Mathf.Max(.5f, width)), color);
            }
            finally { GUI.matrix = matrix; }
        }
    }
}
