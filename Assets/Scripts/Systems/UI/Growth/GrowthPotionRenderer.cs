using System;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>One reusable, masked potion render target. Independent of gameplay state and time scale.</summary>
    internal sealed class GrowthPotionRenderer : IDisposable
    {
        Material material;
        RenderTexture target;
        Texture2D fallback;
        Color[] fallbackPixels;
        Color[] vinePixels;
        string lastCharacter;
        Color lastAccent;
        float lastTime = -1;
        float lastProgress = -1;

        internal Texture Render(string character, Color accent, float time, float progress)
        {
            character = GrowthTreeEffects.Character(character);
            progress = Mathf.Clamp01(progress);
            var shader = material != null ? material.shader : Resources.Load<Shader>("ProjectDM/UI/GrowthPotion");
            if (shader == null || !shader.isSupported || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return RenderFallback(character, accent, time, progress);
            material ??= new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            if (target == null)
            {
                target = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGB32)
                { name = "Growth potion", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                target.Create();
            }
            if (lastTime == time && lastCharacter == character && lastAccent == accent && lastProgress == progress) return target;
            material.SetFloat("_Kind", character == "flame" ? 1 : character == "lightning" ? 2 : character == "frost" ? 3 : 0);
            material.SetFloat("_Clock", time); material.SetColor("_LiquidColor", accent);
            material.SetFloat("_Fill", progress); material.SetFloat("_Completed", progress >= 1 ? 1 : 0);
            RenderTexture previous = RenderTexture.active;
            try { Graphics.Blit(Texture2D.whiteTexture, target, material); }
            finally { RenderTexture.active = previous; }
            lastTime = time; lastCharacter = character; lastAccent = accent; lastProgress = progress;
            target.name = "Growth potion " + character; return target;
        }

        internal static float BottleDistance(Vector2 p)
        {
            return LocalBottleDistance(BottleSpace(p));
        }

        static Vector2 BottleSpace(Vector2 p)
        {
            p.y += .12f;
            return new Vector2(.573576f * p.x - .819152f * p.y, .819152f * p.x + .573576f * p.y - .12f);
        }

        static float LocalBottleDistance(Vector2 p)
        {
            float a = (p - new Vector2(0, -.12f)).magnitude - .58f;
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y - .43f)) - new Vector2(.12f, .19f);
            float b = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - .045f;
            float h = Mathf.Clamp01(.5f + .5f * (b - a) / .09f);
            return Mathf.Lerp(b, a, h) - .09f * h * (1 - h);
        }

        static readonly Vector2[] StemPoints = {
            new(.21f,.44f), new(-.10f,.27f), new(-.20f,.43f),
            new(-.20f,.43f), new(-.25f,.54f), new(-.23f,.66f),
            new(-.23f,.66f), new(-.28f,.84f), new(-.43f,.72f),
            new(-.43f,.72f), new(-.55f,.52f), new(-.36f,.51f),
            new(-.36f,.51f), new(-.23f,.50f), new(-.29f,.65f),
            new(.53f,-.22f), new(.39f,-.34f), new(.18f,-.35f),
            new(.18f,-.35f), new(.10f,-.42f), new(.23f,-.52f)
        };

        static Color VinePixel(Vector2 p)
        {
            float stem = 10;
            for (int curve = 0; curve < StemPoints.Length; curve += 3)
            {
                Vector2 previous = StemPoints[curve];
                for (int i = 1; i <= 8; i++)
                {
                    float t = i / 8f;
                    Vector2 next = Vector2.Lerp(Vector2.Lerp(StemPoints[curve], StemPoints[curve + 1], t), Vector2.Lerp(StemPoints[curve + 1], StemPoints[curve + 2], t), t);
                    Vector2 v = next - previous;
                    stem = Mathf.Min(stem, (p - previous - v * Mathf.Clamp01(Vector2.Dot(p - previous, v) / v.sqrMagnitude)).magnitude);
                    previous = next;
                }
            }
            if (stem < .033f)
            {
                float band = Mathf.Floor(Mathf.Clamp01(.55f + p.y * .5f - p.x * .6f) * 3) / 3;
                Color color = Color.Lerp(new Color(.23f,.46f,.035f), new Color(.58f,.83f,.12f), band);
                return new Color(Mathf.Round(color.r * 12) / 12, Mathf.Round(color.g * 12) / 12, Mathf.Round(color.b * 12) / 12);
            }
            return stem < .055f ? new Color(.045f,.10f,.025f) : Color.clear;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 v = b - a;
            return (p - a - v * Mathf.Clamp01(Vector2.Dot(p - a, v) / v.sqrMagnitude)).magnitude;
        }
        static float Ellipse(Vector2 p, float x, float y) => (new Vector2(p.x / x, p.y / y).magnitude - 1) * Mathf.Min(x, y);
        static float ElementSymbol(Vector2 p, string character, float time)
        {
            Vector2 q = (p - new Vector2(0, -.15f)) / .30f;
            if (character == "projectile")
            {
                float diamond = Mathf.Abs(Mathf.Abs(q.x) * 1.35f + Mathf.Abs(q.y) - .9f) / 1.68f;
                float ring = Mathf.Abs(new Vector2(q.x, q.y * .72f).magnitude - .9f);
                return Mathf.Clamp01(Mathf.Exp(-diamond * 23) + Mathf.Exp(-ring * 27) * .35f + Mathf.Clamp01(1 - Mathf.Abs(q.x) * 2.8f - Mathf.Abs(q.y) * 1.8f) * .9f);
            }
            if (character == "flame")
            {
                q.x += Mathf.Sin(q.y * 3 - time * 1.4f) * .065f;
                float flame = Mathf.Min(Ellipse(q - new Vector2(0,-.22f),.49f,.62f),
                    Mathf.Min(Ellipse(q - new Vector2(-.25f,.15f),.22f,.61f), Ellipse(q - new Vector2(.29f,-.13f),.22f,.48f)));
                return flame < 0 && Ellipse(q - new Vector2(.04f,-.42f),.20f,.29f) > 0 ? 1 : 0;
            }
            if (character == "lightning")
            {
                float d = Mathf.Min(Segment(q,new(.22f,.92f),new(-.34f,.10f)), Segment(q,new(-.34f,.10f),new(.23f,.10f)));
                d = Mathf.Min(d, Segment(q,new(.23f,.10f),new(-.25f,-.90f)));
                d = Mathf.Min(d, Segment(q,new(.12f,-.1f),new(.62f,-.28f)) * 1.5f);
                d = Mathf.Min(d, Segment(q,new(-.11f,.44f),new(-.57f,.59f)) * 1.5f);
                return Mathf.Exp(-d * 18) * (.88f + Mathf.Sin(time * 3.5f) * .12f);
            }
            q = (p - new Vector2(0,-.15f)) / .28f;
            float snow = 10;
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3 + Mathf.Sin(time * .35f) * .05f;
                Vector2 axis = new(Mathf.Cos(a), Mathf.Sin(a)), side = new(-axis.y, axis.x);
                snow = Mathf.Min(snow, Segment(q,Vector2.zero,axis));
                snow = Mathf.Min(snow, Segment(q,axis * .52f,axis * .77f + side * .22f));
                snow = Mathf.Min(snow, Segment(q,axis * .52f,axis * .77f - side * .22f));
            }
            return Mathf.Exp(-snow * .28f * 180 * .55f);
        }

        Texture RenderFallback(string character, Color accent, float time, float progress)
        {
            const int size = 128;
            if (fallback != null && lastCharacter == character && lastAccent == accent && lastProgress == progress && Mathf.Floor(lastTime * 12) == Mathf.Floor(time * 12)) return fallback;
            fallback ??= new Texture2D(size, size, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            fallbackPixels ??= new Color[size * size];
            bool buildVines = vinePixels == null;
            vinePixels ??= new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Vector2 p = new((x + .5f) / size * 2 - 1, (y + .5f) / size * 2 - 1);
                Vector2 glassPoint = new((Mathf.Floor((p.x + 1) * 32) + .5f) / 32 - 1, (Mathf.Floor((p.y + 1) * 32) + .5f) / 32 - 1);
                Vector2 bottlePoint = BottleSpace(glassPoint);
                float d = LocalBottleDistance(bottlePoint); Color color = Color.clear;
                if (progress >= 1) { color = accent; color.a = Mathf.Exp(-Mathf.Max(d, 0) * 11) * (.41f + Mathf.Sin(time * 1.6f) * .045f); }
                if (d < 0)
                {
                    color = new Color(.35f, .45f, .52f, .2f);
                    float surface = Mathf.Lerp(-.70f, .56f, progress) + Mathf.Sin(p.x * 7 + time * .8f) * .010f + Mathf.Sin(p.x * 11 - time * .6f) * .004f;
                    if (progress > 0 && d < -.035f && (progress >= 1 || p.y < surface))
                    {
                        color = accent * (.4f + Mathf.Clamp01((p.y + .76f) / .9f) * .6f); color.a = .95f;
                        float shine = character == "lightning" ? Mathf.Exp(-Mathf.Abs(p.x - Mathf.Sin(p.y * 40 + Mathf.Floor(time * 5)) * .18f) * 110) :
                            character == "flame" ? Mathf.Exp(-Mathf.Abs(p.x - Mathf.Sin(p.y * 13 - time * 2) * .14f) * 14) :
                            Mathf.Pow(Mathf.Max(0, Mathf.Sin(p.x * 30 + p.y * 22 - time)), 30);
                        float flowTime = time * .91f;
                        Vector2 uv = p * .5f + Vector2.one * .5f;
                        float warp = Mathf.Sin(uv.y * 8 + flowTime) * .8f + Mathf.Sin(uv.y * 17 - flowTime * .6f) * .22f;
                        float phase = uv.x * 3 * 6.283f - flowTime * 3 + warp;
                        float current = Mathf.Sin(phase) * .5f + .5f;
                        float ribbon = Mathf.Pow(Mathf.Max(0, Mathf.Sin(phase * .53f + uv.y * 7 - flowTime * .6f)), 10);
                        color += (accent * current * .17f + Color.Lerp(accent, Color.white, .65f) * ribbon * .19f) * .85f;
                        color = Color.Lerp(color, Color.white, shine * .12f); color.a = .96f;
                    }
                    if (d < -.035f)
                    {
                        Color symbolColor = character == "flame" ? new Color(1,.85f,.35f) : Color.Lerp(accent, Color.white, .75f);
                        float symbol = Mathf.Clamp01(ElementSymbol(p, character, time) * (.90f + Mathf.Sin(time * 1.8f) * .04f + (progress >= 1 ? .06f : 0)));
                        float alpha = symbol + color.a * (1 - symbol);
                        color = (symbolColor * symbol + color * color.a * (1 - symbol)) / Mathf.Max(.00001f, alpha);
                        color.a = alpha;
                    }
                    if (d >= -.034f) color = Color.Lerp(color, new Color(.68f,.82f,.9f,.7f), .8f);
                }
                int index = y * size + x;
                if (buildVines) vinePixels[index] = VinePixel(bottlePoint);
                if (vinePixels[index].a > 0) color = vinePixels[index];
                if (Mathf.Abs(bottlePoint.x) < .18f && Mathf.Abs(bottlePoint.y - .73f) < .08f) color = new Color(.5f,.375f,.125f);
                if (Mathf.Abs(bottlePoint.x) < .205f && Mathf.Abs(bottlePoint.y - .61f) < .04f) color = new Color(.75f,.625f,.375f);
                fallbackPixels[y * size + x] = color;
            }
            fallback.SetPixels(fallbackPixels); fallback.Apply(); fallback.name = "Growth potion " + character;
            lastTime = time; lastCharacter = character; lastAccent = accent; lastProgress = progress; return fallback;
        }

        static void Release(UnityEngine.Object value)
        { if (value == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
        public void Dispose()
        {
            if (target != null) target.Release(); Release(target); Release(material); Release(fallback);
            target = null; material = null; fallback = null; fallbackPixels = vinePixels = null; lastTime = lastProgress = -1;
        }
    }
}
