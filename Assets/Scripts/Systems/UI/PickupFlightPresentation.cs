using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Screen-space pickup flights and short-lived pixel sparkles; never owns rewards.</summary>
    internal sealed class PickupFlightPresentation
    {
        private readonly GameSettings settings;
        private readonly List<Flight> flights = new(128);
        private readonly List<Spark> sparks = new(512);
        private readonly List<Settlement> settling = new(64);
        private readonly Dictionary<Sprite, Rect> contentUvs = new();
        private int serial;
        internal float CurrencyPulse { get; private set; }
        internal float ManaPulse { get; private set; }
        internal float ExperiencePulse { get; private set; }
        internal int ActiveCount => flights.Count;
        internal int SparkCount => sparks.Count;
        internal double PendingCurrency { get; private set; }
        internal double PendingMana { get; private set; }
        internal double PendingExperience { get; private set; }

        private struct Flight
        {
            internal Vector2 start;
            internal Sprite sprite;
            internal PickupKind kind;
            internal float age, nextSpark;
            internal Route route;
            internal int seed;
            internal double amount;
        }

        internal struct Route
        {
            internal int kind;
            internal float firstAlong, secondAlong, firstSide, secondSide, initialSpeed, duration;
        }

        private struct Spark
        {
            internal Vector2 origin, velocity;
            internal PickupKind kind;
            internal float age, lifetime, size;
            internal int seed;
        }

        private struct Settlement
        {
            internal PickupKind kind;
            internal double amount;
            internal float age;
        }

        internal static float FillProgress(float age) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .28f));

        internal PickupFlightPresentation(GameSettings settings) { this.settings = settings; }

        [System.Serializable] private sealed class BoundsTable { public BoundsEntry[] entries; }
        [System.Serializable] private sealed class BoundsEntry { public string name; public float x, y, width, height; }
        private static Dictionary<string, Rect> knownBounds;

        // Tiny sprites use full-rectangle meshes even with Tight selected. These bounds are
        // measured offline from alpha, avoiding both transparent padding and runtime GPU readback.
        internal static Rect ContentUv(Sprite sprite)
        {
            if (knownBounds == null)
            {
                knownBounds = new Dictionary<string, Rect>();
                TextAsset data = Resources.Load<TextAsset>("ProjectDM/UI/PickupSpriteBounds");
                BoundsTable table = data == null ? null : JsonUtility.FromJson<BoundsTable>(data.text);
                if (table?.entries != null) foreach (BoundsEntry entry in table.entries)
                    knownBounds[entry.name] = new Rect(entry.x, entry.y, entry.width, entry.height);
            }
            if (knownBounds.TryGetValue(sprite.name, out Rect bounds))
            {
                Rect pixels = sprite.textureRect;
                return new Rect((pixels.x + bounds.x * pixels.width) / sprite.texture.width,
                    (pixels.y + bounds.y * pixels.height) / sprite.texture.height,
                    bounds.width * pixels.width / sprite.texture.width, bounds.height * pixels.height / sprite.texture.height);
            }
            Vector2 min = Vector2.one, max = Vector2.zero;
            foreach (Vector2 uv in sprite.uv) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        internal static float VisualSize(float screenHeight) => 38f * Mathf.Clamp(screenHeight / 1080f, .85f, 1.4f);
        internal static float Opacity(float progress) => 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.88f, 1, progress));

        private static Texture2D sparkHalo;
        private static Texture2D SparkHalo
        {
            get
            {
                if (sparkHalo != null) return sparkHalo;
                const int size = 16;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x + .5f - size * .5f, y + .5f - size * .5f) / (size * .5f);
                    float alpha = Mathf.Exp(-point.sqrMagnitude * 4.5f) * Mathf.Clamp01(1 - point.magnitude);
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
                sparkHalo = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "Pickup trail light", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                sparkHalo.SetPixels(pixels); sparkHalo.Apply(false, true);
                return sparkHalo;
            }
        }

        internal static float SparkOpacity(float age, float lifetime, int seed)
        {
            float progress = Mathf.Clamp01(age / Mathf.Max(.001f, lifetime));
            float twinkle = .78f + .22f * Mathf.Sin(age * 42f + seed);
            return .95f * Mathf.Pow(1 - progress, .65f) * twinkle;
        }

        internal void Show(Vector3 world, Sprite sprite, PickupKind kind, double amount = 0)
        {
            Camera camera = settings.gameplayCamera;
            if (camera == null || sprite == null || (kind != PickupKind.Currency && kind != PickupKind.Mana && kind != PickupKind.Experience)) return;
            Vector3 point = camera.WorldToScreenPoint(world);
            if (point.z <= 0 || !camera.pixelRect.Contains(point)) return;
            if (!contentUvs.ContainsKey(sprite)) contentUvs.Add(sprite, ContentUv(sprite));
            int seed = ++serial;
            amount = System.Math.Max(0, amount);
            Pending(kind, amount);
            flights.Add(new Flight
            {
                start = new Vector2(point.x / Screen.width, 1f - point.y / Screen.height),
                sprite = sprite, kind = kind, seed = seed, route = CreateRoute(seed), amount = amount
            });
        }

        private void Pending(PickupKind kind, double amount)
        {
            if (kind == PickupKind.Currency) PendingCurrency = System.Math.Max(0, PendingCurrency + amount);
            else if (kind == PickupKind.Mana) PendingMana = System.Math.Max(0, PendingMana + amount);
            else PendingExperience = System.Math.Max(0, PendingExperience + amount);
        }

        private static float Variation(int seed, uint salt)
        {
            // Local hashes must not consume the random sequence used for drops or upgrade choices.
            uint value = unchecked((uint)seed * 747796405u + salt);
            value ^= value >> 16; value = unchecked(value * 2246822519u); value ^= value >> 13;
            return (value & 0x00ffffffu) / 16777215f;
        }

        internal static Route CreateRoute(int seed)
        {
            int kind = seed % 3;
            float side = Variation(seed, 19) < .5f ? -1 : 1;
            float firstSide = kind == 0 ? Mathf.Lerp(.12f, .22f, Variation(seed, 37))
                : Mathf.Lerp(.4f, .65f, Variation(seed, 37));
            float secondSide = kind == 2 ? -Mathf.Lerp(.32f, .58f, Variation(seed, 61))
                : firstSide * Mathf.Lerp(.65f, 1.15f, Variation(seed, 61));
            return new Route
            {
                kind = kind, firstAlong = Mathf.Lerp(.18f, .32f, Variation(seed, 83)),
                secondAlong = Mathf.Lerp(.65f, .82f, Variation(seed, 107)),
                firstSide = firstSide * side, secondSide = secondSide * side,
                initialSpeed = Mathf.Lerp(.5f, .95f, Variation(seed, 149)),
                duration = Mathf.Lerp(.56f, .92f, Variation(seed, 181))
            };
        }

        private static Vector2 ControlPoint(Vector2 start, Vector2 delta, float along, float side, Vector2 screen)
        {
            Vector2 point = start + delta * along;
            Vector2 offset = new Vector2(-delta.y, delta.x).normalized * Mathf.Min(500, delta.magnitude) * side;
            float fit = 1;
            if (offset.x > 0) fit = Mathf.Min(fit, (screen.x - 12 - point.x) / offset.x);
            else if (offset.x < 0) fit = Mathf.Min(fit, (12 - point.x) / offset.x);
            if (offset.y > 0) fit = Mathf.Min(fit, (screen.y - 12 - point.y) / offset.y);
            else if (offset.y < 0) fit = Mathf.Min(fit, (12 - point.y) / offset.y);
            return point + offset * Mathf.Clamp01(fit);
        }

        internal static Vector2 Sample(Vector2 start, Vector2 end, float progress, Route route, Vector2 screen)
        {
            float t = Mathf.Clamp01(progress);
            // Nonzero initial velocity: no launch, hold or ease-in pause before flying.
            float u = route.initialSpeed * t + (1 - route.initialSpeed) * t * t;
            Vector2 delta = end - start;
            Vector2 first = ControlPoint(start, delta, route.firstAlong, route.firstSide, screen);
            Vector2 second = ControlPoint(start, delta, route.secondAlong, route.secondSide, screen);
            float v = 1 - u;
            return v * v * v * start + 3 * v * v * u * first + 3 * v * u * u * second + u * u * u * end;
        }

        private static Vector2 Destination(PickupKind kind, Vector2 currency, Vector2 mana, Vector2 experience)
            => kind == PickupKind.Currency ? currency : kind == PickupKind.Mana ? mana : experience;

        internal void Tick(float dt, Vector2 screen, Vector2 currencyTarget, Vector2 manaTarget, Vector2 experienceTarget)
        {
            if (dt <= 0 || screen.x <= 0 || screen.y <= 0) return;
            CurrencyPulse *= Mathf.Exp(-12f * dt);
            ManaPulse *= Mathf.Exp(-12f * dt);
            ExperiencePulse *= Mathf.Exp(-12f * dt);
            for (int i = settling.Count - 1; i >= 0; i--)
            {
                Settlement item = settling[i];
                float before = FillProgress(item.age);
                item.age += dt;
                Pending(item.kind, -item.amount * (FillProgress(item.age) - before));
                if (item.age >= .28f) { settling[i] = settling[settling.Count - 1]; settling.RemoveAt(settling.Count - 1); }
                else settling[i] = item;
            }
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                Spark spark = sparks[i]; spark.age += dt;
                if (spark.age >= spark.lifetime) { sparks[i] = sparks[sparks.Count - 1]; sparks.RemoveAt(sparks.Count - 1); }
                else sparks[i] = spark;
            }
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                Flight flight = flights[i]; flight.age += dt;
                Vector2 target = Destination(flight.kind, currencyTarget, manaTarget, experienceTarget);
                Vector2 start = Vector2.Scale(flight.start, screen);
                while (flight.nextSpark < Mathf.Min(flight.age, flight.route.duration))
                {
                    float at = flight.nextSpark;
                    int seed = flight.seed * 37 + Mathf.RoundToInt(at * 1000);
                    Vector2 point = Sample(start, target, at / flight.route.duration, flight.route, screen);
                    AddSpark(point, screen, flight.kind, seed, flight.age - at, false);
                    flight.nextSpark += .024f;
                }
                if (flight.age >= flight.route.duration)
                {
                    settling.Add(new Settlement { kind = flight.kind, amount = flight.amount });
                    if (flight.kind == PickupKind.Currency) CurrencyPulse = 1;
                    else if (flight.kind == PickupKind.Mana) ManaPulse = 1;
                    else ExperiencePulse = 1;
                    for (int s = 0; s < 5; s++) AddSpark(target, screen, flight.kind, flight.seed * 31 + s, 0, true);
                    flights[i] = flights[flights.Count - 1]; flights.RemoveAt(flights.Count - 1);
                }
                else flights[i] = flight;
            }
        }

        private void AddSpark(Vector2 point, Vector2 screen, PickupKind kind, int seed, float age, bool burst)
        {
            float a = Mathf.Sin(seed * 2.39996f), b = Mathf.Cos(seed * 3.173f);
            float life = .34f + Mathf.Abs(b) * .16f;
            if (age >= life) return;
            point += new Vector2(a, b) * (burst ? 3 : 4);
            sparks.Add(new Spark
            {
                origin = new Vector2(point.x / screen.x, point.y / screen.y),
                velocity = burst ? new Vector2(a, b) * 26f : new Vector2(a * 9f, -8f - Mathf.Abs(b) * 8f),
                kind = kind, seed = seed, age = age, lifetime = life, size = seed % 4 == 0 ? 5 : 4
            });
        }

        private static Color Tint(PickupKind kind) => kind == PickupKind.Currency
            ? new Color(1f, .78f, .34f) : kind == PickupKind.Mana ? new Color(.38f, .84f, 1f) : new Color(.61f, .55f, 1f);

        internal void Draw(Vector2 screen, Vector2 currencyTarget, Vector2 manaTarget, Vector2 experienceTarget)
        {
            if (Event.current.type != EventType.Repaint) return;
            foreach (Spark spark in sparks)
            {
                float p = spark.age / spark.lifetime;
                float opacity = SparkOpacity(spark.age, spark.lifetime, spark.seed);
                Color color = Color.Lerp(Tint(spark.kind), Color.white, spark.seed % 5 == 0 ? .9f : .65f);
                color.a = opacity;
                Vector2 point = Vector2.Scale(spark.origin, screen) + spark.velocity * spark.age;
                float x = Mathf.Round(point.x), y = Mathf.Round(point.y);
                if (spark.seed % 3 != 0)
                {
                    float diameter = spark.size * (spark.seed % 5 == 0 ? 7 : 6);
                    Color previous = GUI.color;
                    Color light = Tint(spark.kind); light.a = opacity * .8f;
                    GUI.color = previous * light;
                    GUI.DrawTexture(new Rect(point.x - diameter * .5f, point.y - diameter * .5f, diameter, diameter), SparkHalo);
                    GUI.color = previous;
                }
                UiGraphics.DrawSolidRect(new Rect(x, y, spark.size, spark.size), color);
                if (spark.seed % 7 == 0 && p < .6f)
                {
                    color.a *= .5f;
                    UiGraphics.DrawSolidRect(new Rect(x - 2, y + 1, spark.size + 4, 1), color);
                    UiGraphics.DrawSolidRect(new Rect(x + 1, y - 2, 1, spark.size + 4), color);
                }
            }
            foreach (Flight flight in flights)
            {
                float progress = Mathf.Clamp01(flight.age / flight.route.duration);
                Vector2 target = Destination(flight.kind, currencyTarget, manaTarget, experienceTarget);
                Vector2 point = Sample(Vector2.Scale(flight.start, screen), target, progress, flight.route, screen);
                Rect uv = contentUvs[flight.sprite];
                Texture2D texture = flight.sprite.texture;
                Vector2 pixels = new Vector2(uv.width * texture.width, uv.height * texture.height);
                Vector2 size = pixels / Mathf.Max(pixels.x, pixels.y) * VisualSize(screen.y);
                Color previous = GUI.color;
                Color fade = previous; fade.a *= Opacity(progress); GUI.color = fade;
                GUI.DrawTextureWithTexCoords(new Rect(point - size * .5f, size), texture, uv);
                GUI.color = previous;
            }
        }

        internal void Clear()
        {
            flights.Clear(); sparks.Clear(); serial = 0;
            settling.Clear();
            CurrencyPulse = ManaPulse = ExperiencePulse = 0;
            PendingCurrency = PendingMana = PendingExperience = 0;
        }
    }
}
