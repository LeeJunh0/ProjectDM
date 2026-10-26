using System;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>One short shield flash and damped HUD impulse per accepted mana hit.</summary>
    internal sealed class ManaHitFeedback : IDisposable
    {
        const float Duration = .55f;
        float age = Duration, strength;
        Texture2D shieldMask;

        internal float Age => age;
        internal float Intensity => strength * Mathf.Exp(-age * 6f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, Duration, age)));
        internal Vector2 Offset => new Vector2(Mathf.Sin(age * 83f) * 5f, Mathf.Sin(age * 107f + .7f) * 2.5f) * Intensity;

        internal void Notify(float lostMana, float maximum)
        {
            if (lostMana <= 0) return;
            age = 0;
            strength = Mathf.Clamp(.65f + lostMana / Mathf.Max(1, maximum), .65f, 1f);
        }

        internal void Tick(float unscaledDeltaTime) => age = Mathf.Min(Duration, age + Mathf.Max(0, unscaledDeltaTime));
        internal void Reset() { age = Duration; strength = 0; }

        internal Texture2D ShieldMask
        {
            get
            {
                if (shieldMask != null) return shieldMask;
                const int size = 256;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    Vector2 p = new((x + .5f) / size * 2 - 1, (y + .5f) / size * 2 - 1);
                    // Rounded falloff keeps the center clear and avoids a hard rectangular frame.
                    float edge = Mathf.Pow(Mathf.Pow(p.x, 4) + Mathf.Pow(p.y, 4), .25f);
                    float veil = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.7f, 1.17f, edge));
                    Color color = Color.Lerp(new Color(.035f, .23f, .9f), new Color(.18f, .72f, 1), veil);
                    color.a = veil * .86f;
                    pixels[y * size + x] = color;
                }
                shieldMask = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "Mana shield hit", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                shieldMask.SetPixels(pixels); shieldMask.Apply(); return shieldMask;
            }
        }

        internal void DrawShield(Rect screen)
        {
            if (Event.current?.type != EventType.Repaint || Intensity < .002f) return;
            Color previous = GUI.color;
            try
            {
                GUI.color = new Color(1, 1, 1, Intensity * .65f);
                GUI.DrawTexture(screen, ShieldMask, ScaleMode.StretchToFill, true);
            }
            finally { GUI.color = previous; }
        }

        public void Dispose()
        {
            if (shieldMask == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(shieldMask); else UnityEngine.Object.DestroyImmediate(shieldMask);
            shieldMask = null;
        }
    }
}
