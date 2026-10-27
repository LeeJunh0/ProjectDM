using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Displays upgrade cards and owns their entry and hover animation state.</summary>
    internal sealed class LevelUpView
    {
        private readonly GameSettings settings;
        private readonly WorldArtAssets art;
        private readonly LevelUpController selection;
        private readonly UiGraphics graphics;

        internal LevelUpView(GameSettings settings, WorldArtAssets art, LevelUpController selection, UiGraphics graphics)
        {
            this.settings = settings;
            this.art = art;
            this.selection = selection;
            this.graphics = graphics;
        }

        internal readonly float[] upgradeCardHoverTilts = new float[3];

        internal readonly bool[] upgradeCardWasHovered = new bool[3];

        internal void DrawRunUpgradeSelection()
        {
            UiGraphics.DrawSolidRect(new Rect(0, 0, Screen.width, Screen.height), new Color(.015f, .02f, .06f, .76f));

            GUIStyle headingStyle = new GUIStyle(graphics.titleStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 34,
                normal = { textColor = new Color(.96f, .92f, 1f) }
            };
            GUIStyle subtitleStyle = new GUIStyle(graphics.statStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = new Color(.67f, .71f, .86f) }
            };
            GUI.Label(new Rect(0, 74f, Screen.width, 46f), "레벨업", headingStyle);
            GUI.Label(new Rect(0, 118f, Screen.width, 26f), $"이번 런에 적용할 효과 선택  ·  남은 선택 {selection.pendingRunUpgrades}", subtitleStyle);
            var chances = selection.GradeChances;
            GUI.Label(new Rect(0, 140f, Screen.width, 22f), $"행운 {selection.Luck:0.##}  ·  금 {chances.gold * 100:0.#}%  은 {chances.silver * 100:0.#}%  동 {chances.bronze * 100:0.#}%", subtitleStyle);

            bool compactLayout = Screen.width < 820;
            float gap = compactLayout ? 10f : 14f;
            float cardWidth = compactLayout
                ? Mathf.Min(460f, Screen.width - 40f)
                : Mathf.Min(236f, (Screen.width - 92f) / selection.runUpgradeChoices.Length);
            float cardHeight = compactLayout
                ? Mathf.Min(178f, Mathf.Max(108f, (Screen.height - 208f - gap * 2f) / selection.runUpgradeChoices.Length))
                : cardWidth * 1.5f;
            float groupHeight = compactLayout
                ? cardHeight * selection.runUpgradeChoices.Length + gap * (selection.runUpgradeChoices.Length - 1)
                : cardHeight;
            float startX = compactLayout
                ? (Screen.width - cardWidth) * .5f
                : (Screen.width - (cardWidth * selection.runUpgradeChoices.Length + gap * (selection.runUpgradeChoices.Length - 1))) * .5f;
            float startY = Mathf.Max(164f, (Screen.height - groupHeight) * .5f + 34f);

            for (int i = 0; i < selection.runUpgradeChoices.Length; i++)
            {
                Rect targetCardRect = compactLayout
                    ? new Rect(startX, startY + i * (cardHeight + gap), cardWidth, cardHeight)
                    : new Rect(startX + i * (cardWidth + gap), startY, cardWidth, cardHeight);
                float cardProgress = Mathf.Clamp01((Time.unscaledTime - selection.upgradeSelectionOpenedAt - i * .075f) / .28f);
                float arrival = EaseOutBack(cardProgress);
                float belowScreenY = Screen.height + cardHeight + 36f;
                Rect animatedCardRect = targetCardRect;
                animatedCardRect.y = Mathf.LerpUnclamped(belowScreenY, targetCardRect.y, arrival);
                DrawRunUpgradeCard(animatedCardRect, selection.runUpgradeChoices[i], i, compactLayout, cardProgress >= .96f);
            }
        }

        internal void DrawRunUpgradeCard(Rect cardRect, RunUpgradePresentation presentation, int index, bool compactLayout, bool isInteractive)
        {
            bool hovered = isInteractive && cardRect.Contains(Event.current.mousePosition);
            UpdateUpgradeCardHover(index, hovered);
            float tilt = upgradeCardHoverTilts[index];
            Matrix4x4 previousGuiMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(tilt, cardRect.center);
            GUIUtility.ScaleAroundPivot(Vector2.one * (hovered ? settings.upgradeCardHoverScale : 1f), cardRect.center);
            PixelUiArt.DrawCardGlow(cardRect, presentation.accent, hovered, index);
            PixelUiArt.Draw(cardRect, PixelUiArt.Card(presentation.grade), compactLayout ? 14 : 24,
                hovered ? new Color(1.08f, 1.08f, 1.08f) : Color.white);

            bool longTitle = presentation.title.Length > 12;
            GUIStyle nameStyle = new GUIStyle(graphics.titleStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = compactLayout ? 16 : (longTitle ? 18 : 22),
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(.93f, .84f, .7f) }
            };
            GUIStyle descriptionStyle = new GUIStyle(graphics.statStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = compactLayout ? 13 : 14,
                wordWrap = true,
                normal = { textColor = new Color(.88f, .88f, .88f) }
            };
            GUIStyle footerStyle = new GUIStyle(graphics.statStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(.68f, .68f, .68f) }
            };

            float horizontalPadding = compactLayout ? 30f : 34f;
            float titleY = compactLayout ? 10f : 27f;
            float titleHeight = compactLayout ? 22f : 52f;
            float iconY = compactLayout ? 33f : 91f;
            float iconHeight = compactLayout ? 30f : 82f;
            float upperDividerY = compactLayout ? 69f : 186f;
            float lowerDividerY = cardRect.height - (compactLayout ? 31f : 52f);
            while (nameStyle.fontSize > 12 && nameStyle.CalcHeight(new GUIContent(presentation.title), cardRect.width - horizontalPadding * 2f) > titleHeight)
                nameStyle.fontSize--;
            GUI.Label(new Rect(cardRect.x + horizontalPadding, cardRect.y + titleY, cardRect.width - horizontalPadding * 2f, titleHeight), presentation.title, nameStyle);
            UiGraphics.DrawResultSprite(new Rect(cardRect.center.x - iconHeight * .5f, cardRect.y + iconY, iconHeight, iconHeight), art.PrototypeIcon(presentation.icon));
            DrawCardDivider(cardRect, horizontalPadding, upperDividerY, presentation.accent);
            GUI.Label(new Rect(cardRect.x + horizontalPadding, cardRect.y + upperDividerY + 12f, cardRect.width - horizontalPadding * 2f, lowerDividerY - upperDividerY - 18f), presentation.description, descriptionStyle);
            DrawCardDivider(cardRect, horizontalPadding, lowerDividerY, presentation.accent);
            GUI.Label(new Rect(cardRect.x + horizontalPadding, cardRect.y + lowerDividerY + 7f, cardRect.width - horizontalPadding * 2f, 18f), $"{presentation.category}  ·  이번 런  [{index + 1}]", footerStyle);
            GUI.matrix = previousGuiMatrix;

            if (isInteractive && GUI.Button(cardRect, GUIContent.none, GUIStyle.none))
            {
                selection.ChooseRunUpgrade(presentation.upgrade);
            }
        }

        internal void UpdateUpgradeCardHover(int index, bool hovered)
        {
            if (hovered && !upgradeCardWasHovered[index])
            {
                PlayUpgradeCardHoverSound();
            }

            upgradeCardWasHovered[index] = hovered;
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            float targetTilt = hovered ? settings.upgradeCardHoverLeftTiltAngle : 0f;
            upgradeCardHoverTilts[index] = Mathf.MoveTowards(
                upgradeCardHoverTilts[index],
                targetTilt,
                settings.upgradeCardHoverTiltSpeed * Time.unscaledDeltaTime);
        }

        internal void PlayUpgradeCardHoverSound()
        {
            // Audio hook: assign and play the future hover clip here. Intentionally silent for UI debugging.
        }

        internal static float EaseOutBack(float progress)
        {
            const float overshoot = 1.45f;
            float offset = progress - 1f;
            return 1f + (overshoot + 1f) * offset * offset * offset + overshoot * offset * offset;
        }

        private static void DrawCardDivider(Rect rect, float padding, float y, Color accent)
        {
            // Grade-colored rules keep the small card dividers legible at game resolution.
            UiGraphics.DrawSolidRect(new Rect(rect.x + padding, rect.y + y, rect.width - padding * 2, 1),
                new Color(accent.r, accent.g, accent.b, .4f));
        }

        internal void ResetHover()
        {
            Array.Clear(upgradeCardHoverTilts, 0, upgradeCardHoverTilts.Length);
            Array.Clear(upgradeCardWasHovered, 0, upgradeCardWasHovered.Length);
        }
    }
}
