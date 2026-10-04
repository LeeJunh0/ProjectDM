using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Provides shared UI styles, artwork crops and drawing primitives.</summary>
    internal sealed class UiGraphics
    {
        internal UiGraphics()
        {
            resultSecondaryButtonTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_ResultButtonSecondary_v1");
            resultPrimaryButtonTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_ResultButtonPrimary_v1");
        }

        internal Texture2D resultSecondaryButtonTexture;

        internal Texture2D resultPrimaryButtonTexture;

        internal static readonly Rect ResultSecondaryButtonUv = new Rect(83f / 2036f, 177f / 772f, 1870f / 2036f, 416f / 772f);

        internal static readonly Rect ResultPrimaryButtonUv = new Rect(91f / 2146f, 156f / 733f, 1964f / 2146f, 421f / 733f);

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
            DrawSolidRect(new Rect(46f, y, 628f, 2f), new Color(.55f, .50f, .43f, .65f));
        }

        internal static void DrawResultArtwork(Rect rect, Texture2D texture, Rect uv, Color tint)
        {
            if (texture != null) DrawTimerTexture(rect, texture, uv, tint);
            else
            {
                DrawSolidRect(rect, new Color(.055f, .065f, .095f));
                DrawOutline(rect, new Color(.72f, .52f, .25f), 2f);
            }
        }

        internal bool DrawResultButton(Rect rect, string label, bool primary)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            DrawResultArtwork(rect, primary ? resultPrimaryButtonTexture : resultSecondaryButtonTexture,
                primary ? ResultPrimaryButtonUv : ResultSecondaryButtonUv, hovered ? new Color(1.12f, 1.12f, 1.12f) : Color.white);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.Label(rect, label, new GUIStyle(statStyle) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.96f, .94f, .87f) } });
            return clicked;
        }

        internal static void DrawResultSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null) return;
            Rect pixels = sprite.textureRect;
            Texture2D texture = sprite.texture;
            Rect uv = new Rect(pixels.x / texture.width, pixels.y / texture.height, pixels.width / texture.width, pixels.height / texture.height);
            float aspect = pixels.width / pixels.height;
            Rect image = aspect > 1f
                ? new Rect(rect.x, rect.center.y - rect.width / aspect * .5f, rect.width, rect.width / aspect)
                : new Rect(rect.center.x - rect.height * aspect * .5f, rect.y, rect.height * aspect, rect.height);
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
                normal = { textColor = new Color(0.8f, 0.48f, 1f) }
            };
            statStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = new Color(0.72f, 0.82f, 1f) }
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
