using UnityEngine;

namespace ProjectDM
{
    /// <summary>Shared pixel frames. Source crops leave the generated PNGs intact; nine slices preserve corners.</summary>
    internal static class PixelUiArt
    {
        internal sealed class Frame
        {
            internal readonly Texture2D texture;
            internal readonly Rect uv;
            internal readonly Vector2 sourceSize;
            internal readonly float sourceBorder;

            internal Frame(string name, float canvasWidth, float canvasHeight, Rect pixels, float border)
            {
                texture = Resources.Load<Texture2D>("ProjectDM/UI/PixelUI/" + name);
                uv = new Rect(pixels.x / canvasWidth, 1 - pixels.yMax / canvasHeight,
                    pixels.width / canvasWidth, pixels.height / canvasHeight);
                sourceSize = pixels.size;
                sourceBorder = border;
            }
        }

        internal static readonly Frame Result = new("panel_result", 1205, 1305, new Rect(90, 52, 1024, 1198), 74);
        internal static readonly Frame Panel = new("panel_tooltip", 2066, 761, new Rect(34, 128, 1998, 490), 90);
        internal static readonly Frame Primary = new("button_primary", 2146, 733, new Rect(144, 165, 1858, 402), 112);
        internal static readonly Frame Secondary = new("button_secondary", 2146, 733, new Rect(152, 158, 1842, 414), 112);
        internal static readonly Frame Bronze = new("card_bronze", 1024, 1536, new Rect(120, 184, 784, 1192), 96);
        internal static readonly Frame Silver = new("card_silver", 1024, 1536, new Rect(108, 198, 808, 1206), 96);
        internal static readonly Frame Gold = new("card_gold", 1024, 1536, new Rect(120, 168, 784, 1182), 96);
        internal static readonly Frame Mana = new("mana_frame", 2172, 724, new Rect(82, 256, 2008, 205), 96);
        internal static readonly Frame Node = new("growth_node", 1254, 1254, new Rect(376, 386, 504, 482), 70);
        internal static readonly Frame Tab = new("tab", 2172, 724, new Rect(246, 188, 1680, 348), 90);
        internal static readonly Frame Currency = new("currency_badge", 1920, 819, new Rect(233, 267, 1455, 282), 90);
        internal static readonly Frame Divider = new("divider", 2172, 724, new Rect(72, 332, 2028, 59), 0);

        internal static Frame Card(string grade) => grade == "gold" ? Gold : grade == "silver" ? Silver : Bronze;

        internal static void Draw(Rect rect, Frame frame, float border, Color tint)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0 || rect.height <= 0) return;
            if (frame.texture == null)
            {
                UiGraphics.DrawSolidRect(rect, new Color(.085f, .067f, .12f));
                UiGraphics.DrawOutline(rect, tint, 1);
                return;
            }
            DrawSlices(rect, frame.texture, frame.uv, frame.sourceSize, frame.sourceBorder, border, tint);
        }

        private static void DrawSlices(Rect rect, Texture2D texture, Rect uv, Vector2 sourceSize,
            float sourceBorder, float border, Color tint)
        {
            Color previous = GUI.color;
            GUI.color = previous * tint;
            if (sourceBorder <= 0 || border <= 0)
                GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
            else
            {
                float dx = Mathf.Min(border, rect.width * .5f), dy = Mathf.Min(border, rect.height * .5f);
                float sx = Mathf.Min(sourceBorder / sourceSize.x, .5f) * uv.width;
                float sy = Mathf.Min(sourceBorder / sourceSize.y, .5f) * uv.height;
                for (int row = 0; row < 3; row++) for (int column = 0; column < 3; column++)
                {
                    float x = column == 0 ? rect.x : column == 1 ? rect.x + dx : rect.xMax - dx;
                    float y = row == 0 ? rect.y : row == 1 ? rect.y + dy : rect.yMax - dy;
                    float w = column == 1 ? rect.width - 2 * dx : dx;
                    float h = row == 1 ? rect.height - 2 * dy : dy;
                    if (w <= 0 || h <= 0) continue;
                    float u = column == 0 ? uv.x : column == 1 ? uv.x + sx : uv.xMax - sx;
                    // GUI is top-down; texture UVs are bottom-up.
                    float v = row == 0 ? uv.yMax - sy : row == 1 ? uv.y + sy : uv.y;
                    GUI.DrawTextureWithTexCoords(new Rect(x, y, w, h), texture,
                        new Rect(u, v, column == 1 ? uv.width - 2 * sx : sx, row == 1 ? uv.height - 2 * sy : sy), true);
                }
            }
            GUI.color = previous;
        }

        private static Texture2D glow;
        private static Texture2D Glow
        {
            get
            {
                if (glow != null) return glow;
                const int size = 64;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(Mathf.Abs(x + .5f - 32), Mathf.Abs(y + .5f - 32));
                    // A small chamfer echoes the stepped frame without tinting the card's text or center.
                    float distance = Mathf.Max(Mathf.Max(p.x, p.y) - 16, (p.x + p.y - 29) * .7071f);
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Exp(-distance * distance / 22));
                }
                glow = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "Pixel UI border halo", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                glow.SetPixels(pixels); glow.Apply(false, true);
                return glow;
            }
        }

        internal static void DrawCardGlow(Rect rect, Color accent, bool hovered, int index)
        {
            if (Event.current.type != EventType.Repaint) return;
            float breath = .5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2 / 3.8f + index * .7f);
            accent.a = .19f + breath * .08f + (hovered ? .11f : 0);
            const float padding = 12;
            Rect halo = new Rect(rect.x - padding, rect.y - padding, rect.width + padding * 2, rect.height + padding * 2);
            DrawSlices(halo, Glow, new Rect(0, 0, 1, 1), new Vector2(64, 64), 24, 18, accent);
        }
    }
}
