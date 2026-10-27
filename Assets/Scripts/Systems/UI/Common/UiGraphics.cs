using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Provides shared UI styles, artwork crops and drawing primitives.</summary>
    internal sealed class UiGraphics
    {
        internal GUIStyle titleStyle;

        internal GUIStyle statStyle;

        internal GUIStyle cardStyle;

        internal static void DrawTimerTexture(Rect rect, Texture2D texture, Rect uv, Color tint)
        {
            Color previousColor = GUI.color;
            GUI.color = previousColor * tint;
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
            GUI.color = previousColor;
        }

        internal static void DrawResultDivider(float y)
        {
            PixelUiArt.Draw(new Rect(46f, y - 3, 628f, 8f), PixelUiArt.Divider, 0, new Color(1, 1, 1, .7f));
        }

        internal static void DrawStaticLabel(Rect rect, string text, GUIStyle style)
        {
            // GUI.Label selects the inherited hover state from the pointer position, even for plain text.
            // Draw the normal state directly without registering a control, focus or tooltip.
            if (Event.current.type == EventType.Repaint)
                style.Draw(rect, new GUIContent(text), false, false, false, false);
        }

        internal bool DrawResultButton(Rect rect, string label, bool primary)
        {
            return DrawButton(rect, label, primary, Mathf.Clamp(Mathf.RoundToInt(rect.height * .36f), 12, 28));
        }

        internal bool DrawButton(Rect rect, string label, bool primary = false, int fontSize = 14)
            => DrawControl(rect, label, primary ? PixelUiArt.Primary : PixelUiArt.Secondary, Color.white, fontSize);

        internal bool DrawTab(Rect rect, string label, bool selected, Color accent, int fontSize = 12)
            => DrawControl(rect, label, PixelUiArt.Tab, selected ? Color.Lerp(Color.white, accent, .35f) : new Color(.72f, .68f, .78f), fontSize);

        private bool DrawControl(Rect rect, string label, PixelUiArt.Frame frame, Color tint, int fontSize)
        {
            bool hovered = GUI.enabled && rect.Contains(Event.current.mousePosition);
            if (hovered) tint *= 1.15f;
            tint.a = GUI.enabled ? 1 : .45f;
            PixelUiArt.Draw(rect, frame, Mathf.Min(10, rect.height * .25f), tint);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            var style = new GUIStyle(statStyle)
            {
                fontSize = fontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip, wordWrap = false,
                normal = { textColor = new Color(.96f, .92f, .81f) }
            };
            while (style.fontSize > 9 && style.CalcSize(new GUIContent(label)).x > rect.width - 18) style.fontSize--;
            GUI.Label(new Rect(rect.x + 9, rect.y, rect.width - 18, rect.height), label, style);
            return clicked;
        }

        internal static void DrawResultSprite(Rect rect, Sprite sprite, Vector2 contentOffset = default)
        {
            if (sprite == null) return;
            Rect pixels = sprite.textureRect;
            Texture2D texture = sprite.texture;
            Rect uv = new Rect(pixels.x / texture.width, pixels.y / texture.height, pixels.width / texture.width, pixels.height / texture.height);
            float aspect = pixels.width / pixels.height;
            Rect image = aspect > 1f
                ? new Rect(rect.x, rect.center.y - rect.width / aspect * .5f, rect.width, rect.width / aspect)
                : new Rect(rect.center.x - rect.height * aspect * .5f, rect.y, rect.height * aspect, rect.height);
            image.position -= Vector2.Scale(image.size, contentOffset);
            GUI.DrawTextureWithTexCoords(image, texture, uv);
        }

        internal static string MonsterDisplayName(MonsterKind kind)
        {
            return kind switch { MonsterKind.Skeleton => "스켈레톤", MonsterKind.Goblin => "고블린", MonsterKind.Mushroom => "버섯", MonsterKind.Boar => "멧돼지", _ => "슬라임" };
        }

        internal void SetupGuiStyles()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(.94f, .82f, .58f) }
            };
            statStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = new Color(.83f, .8f, .88f) }
            };
            cardStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }

        internal void DrawLoadingMessage(string message)
        {
            GUI.Label(new Rect(0, Screen.height * .45f, Screen.width, 42), message,
                new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter });
        }

        internal static void DrawSolidRect(Rect rect, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        internal static void DrawOutline(Rect rect, Color color, float thickness)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }
    }
}
