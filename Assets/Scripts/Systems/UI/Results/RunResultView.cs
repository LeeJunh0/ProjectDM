using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Displays run results and owns result artwork and count-up presentation.</summary>
    internal sealed class RunResultView
    {
        private readonly GameSettings settings;
        private readonly GameTables tables;
        private readonly WorldArtAssets art;
        private readonly RunController run;
        private readonly BossMilestone boss;
        private readonly PlayerCombatSystem playerCombat;
        private readonly UiGraphics graphics;

        internal RunResultView(GameSettings settings, GameTables tables, WorldArtAssets art, RunController run, BossMilestone boss, PlayerCombatSystem playerCombat, UiGraphics graphics)
        {
            this.settings = settings;
            this.tables = tables;
            this.art = art;
            this.run = run;
            this.boss = boss;
            this.playerCombat = playerCombat;
            this.graphics = graphics;
            resultPanelTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_ResultPanel_v1");
            resultGlowTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_ResultGlowOrange_v1");
            permanentKillTrackTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_TimerTrack_v1");
            permanentKillFillTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_TimerFill_v1");
        }

        internal Texture2D resultPanelTexture;

        internal Texture2D resultGlowTexture;
        internal Texture2D permanentKillTrackTexture;
        internal Texture2D permanentKillFillTexture;

        internal static readonly Rect ResultPanelUv = new Rect(56f / 1205f, 59f / 1305f, 1095f / 1205f, 1188f / 1305f);

        internal void DrawRunResults()
        {
            UiGraphics.DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(.015f, .018f, .04f, .6f));
            // The approved preview has a tall panel; scale the complete composition from 1920 x 1080.
            const float panelWidth = 720f;
            const float panelHeight = 780f;
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = previousMatrix * Matrix4x4.TRS(new Vector3((Screen.width - panelWidth * scale) * .5f, (Screen.height - panelHeight * scale) * .5f, 0f), Quaternion.identity, Vector3.one * scale);
            bool returnToMap = false;
            bool restart = false;
            try
            {
                UiGraphics.DrawResultArtwork(new Rect(0f, 0f, panelWidth, panelHeight), resultPanelTexture, ResultPanelUv, Color.white);
                float presentationElapsed = Mathf.Max(0f, Time.unscaledTime - run.resultPresentationOpenedAt);
                DrawResultEdgeGlow(panelWidth, panelHeight, presentationElapsed);
                Color ivory = new Color(.96f, .94f, .87f);
                Color subdued = new Color(.49f, .48f, .48f);
                GUIStyle heading = new GUIStyle(graphics.titleStyle) { fontSize = 36, alignment = TextAnchor.MiddleCenter, normal = { textColor = ivory } };
                GUIStyle body = new GUIStyle(graphics.statStyle) { fontSize = 26, alignment = TextAnchor.MiddleLeft, normal = { textColor = ivory } };
                GUIStyle value = new GUIStyle(body) { alignment = TextAnchor.MiddleRight };
                GUIStyle section = new GUIStyle(body) { fontSize = 30, fontStyle = FontStyle.Bold };
                GUIStyle mutedBody = new GUIStyle(body) { normal = { textColor = subdued } };
                GUIStyle mutedValue = new GUIStyle(value) { normal = { textColor = subdued } };
                GUI.Label(new Rect(44f, 24f, 632f, 48f), boss.bossArrived ? "스테이지 보스 도달" : playerCombat.playerDefeated ? "플레이어 쓰러짐" : run.mana.IsDepleted ? "마나 고갈" : "진행 종료", heading);
                UiGraphics.DrawResultDivider(92f);
                GUI.Label(new Rect(44f, 116f, 632f, 34f), "획득 재화", new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.58f, .55f, .50f) } });

                GUIStyle currencyStyle = new GUIStyle(heading) { fontSize = 68, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(.92f, .68f, .34f) } };
                // Reserve every reachable compact format so the coin/value group stays fixed during count-up.
                string amount = "+" + CompactNumber.Format(AnimatedResultCount(run.runCurrency, 0, presentationElapsed));
                float amountWidth = CurrencyWidth(currencyStyle, run.runCurrency);
                // Fit unusually large balances without letting the text escape the panel.
                while (currencyStyle.fontSize > 24 && amountWidth > 500f)
                { currencyStyle.fontSize--; amountWidth = CurrencyWidth(currencyStyle, run.runCurrency); }
                amountWidth = Mathf.Min(500f, amountWidth);
                float currencyX = (panelWidth - amountWidth - 82f) * .5f;
                UiGraphics.DrawResultSprite(new Rect(currencyX, 163f, 64f, 64f), art.coinSprite);
                GUI.Label(new Rect(currencyX + 82f, 151f, amountWidth + 4f, 90f), amount, currencyStyle);
                UiGraphics.DrawResultDivider(258f);

                int totalKills = 0;
                foreach (int count in run.monsterKillCounts) totalKills = (int)Math.Min(int.MaxValue, (long)totalKills + count);
                GUI.Label(new Rect(62f, 280f, 350f, 42f), "이번 런 총 처치", section);
                GUI.Label(new Rect(446f, 280f, 210f, 42f), $"{AnimatedResultCount(totalKills, 1, presentationElapsed):N0}", new GUIStyle(value) { fontSize = 30, fontStyle = FontStyle.Bold });
                GUI.Label(new Rect(136f, 322f, 520f, 18f), "이번 런 처치 · 영구 처치 스택", new GUIStyle(mutedBody) { fontSize = 16 });
                for (int i = 0; i < run.monsterKillCounts.Length; i++)
                {
                    float rowY = 342f + i * 57f;
                    RunResultPresentation.PermanentKillProgress progress = GetPermanentKillProgress(i);
                    int displayedTotal = AnimatedPermanentKillCount(progress, i + 2, presentationElapsed);
                    bool empty = !progress.available;
                    Color previousColor = GUI.color;
                    GUI.color = previousColor * (empty ? new Color(.5f, .5f, .5f) : Color.white);
                    DrawMonsterResultIcon(new Rect(74f, rowY + 3f, 36f, 36f), (MonsterKind)i);
                    GUI.color = previousColor;
                    DrawFittedLabel(new Rect(136f, rowY, 285f, 30f), art.MonsterName(i, tables.balance), empty ? mutedBody : body, 18);
                    DrawFittedLabel(new Rect(426f, rowY, 230f, 30f), $"이번 런 +{AnimatedResultCount(run.monsterKillCounts[i], i + 2, presentationElapsed):N0}",
                        new GUIStyle(empty ? mutedValue : value) { fontSize = 22 }, 12);
                    string cumulative = progress.HasTarget
                        ? $"누적 {displayedTotal:N0} / {progress.target:N0}" : $"누적 {displayedTotal:N0}";
                    GUIStyle detail = new GUIStyle(empty ? mutedBody : body) { fontSize = 18 };
                    DrawFittedLabel(new Rect(136f, rowY + 30f, 285f, 20f), cumulative, detail, 12);
                    string status = PermanentKillStatus(i, progress, displayedTotal);
                    GUIStyle statusStyle = new GUIStyle(detail) { alignment = TextAnchor.MiddleRight,
                        normal = { textColor = progress.NewlyUnlocked && displayedTotal >= progress.target ? new Color(.98f, .72f, .35f) : subdued } };
                    DrawFittedLabel(new Rect(426f, rowY + 30f, 230f, 20f), status, statusStyle, 12);
                    if (progress.HasTarget) DrawPermanentKillBar(new Rect(136f, rowY + 51f, 520f, 6f), progress, displayedTotal);
                }
                UiGraphics.DrawResultDivider(632f);
                returnToMap = graphics.DrawResultButton(new Rect(38f, 658f, 310f, 78f), "돌아가기", false);
                restart = graphics.DrawResultButton(new Rect(372f, 658f, 310f, 78f), "다시 시작", true);
            }
            finally { GUI.matrix = previousMatrix; }
            if (returnToMap) run.ReturnToGrowthMap();
            else if (restart) run.BeginRun();
        }

        internal RunResultPresentation.PermanentKillProgress GetPermanentKillProgress(int kind)
        {
            int target = kind < tables.balance.monsters.Length - 1 ? tables.balance.monsters[kind].nextUnlockKills : 0;
            return RunResultPresentation.PermanentKills(run.permanentKillsAtRunStart[kind], run.resultPermanentKillCounts[kind],
                run.monsterKillCounts[kind], target, kind <= run.resultMonsterStage);
        }

        internal int AnimatedPermanentKillCount(RunResultPresentation.PermanentKillProgress progress, int sequence, float elapsedTime)
            => RunResultPresentation.AnimatedPermanentKills(progress, elapsedTime, settings.resultCountUpDuration,
                sequence * Mathf.Max(0, settings.resultCountUpStagger));

        internal string PermanentKillStatus(int kind, RunResultPresentation.PermanentKillProgress progress, int displayedTotal)
        {
            if (!progress.available) return "미해금";
            if (!progress.HasTarget) return "최종 몬스터 · 다음 해금 없음";
            if (displayedTotal >= progress.target) return progress.NewlyUnlocked
                ? "이번 런: " + tables.balance.monsters[kind + 1].name + " 해금!"
                : tables.balance.monsters[kind + 1].name + " 해금 완료";
            return "다음: " + tables.balance.monsters[kind + 1].name;
        }

        private static void DrawFittedLabel(Rect rect, string text, GUIStyle source, int minimumSize)
        {
            GUIStyle style = new GUIStyle(source) { wordWrap = false, clipping = TextClipping.Clip };
            while (style.fontSize > minimumSize && style.CalcSize(new GUIContent(text)).x > rect.width) style.fontSize--;
            GUI.Label(rect, text, style);
        }

        private static float CurrencyWidth(GUIStyle style, int target)
        {
            // During count-up, 999,999 can be wider than the final 1M. Reserve every reachable format.
            char widestDigit = '0'; float widest = 0;
            for (char digit = '0'; digit <= '9'; digit++)
            {
                float width = style.CalcSize(new GUIContent(digit.ToString())).x;
                if (width > widest) { widest = width; widestDigit = digit; }
            }
            string digits = new string(widestDigit, Math.Min(999999, Math.Max(0, target)).ToString().Length);
            if (digits.Length > 3) digits = digits.Insert(digits.Length - 3, ",");
            float result = style.CalcSize(new GUIContent("+" + digits)).x;
            string mantissa = "+" + new string(widestDigit, 3) + "." + new string(widestDigit, 2);
            string[] units = { "M", "B" }; double threshold = 1000000;
            foreach (string unit in units)
            {
                if (target < threshold) break;
                result = Mathf.Max(result, style.CalcSize(new GUIContent(mantissa + unit)).x);
                threshold *= 1000;
            }
            return Mathf.Max(result, style.CalcSize(new GUIContent("+" + CompactNumber.Format(target))).x);
        }

        private void DrawPermanentKillBar(Rect rect, RunResultPresentation.PermanentKillProgress progress, int displayedTotal)
        {
            Color tint = progress.available ? Color.white : new Color(.45f, .45f, .45f, .55f);
            UiGraphics.DrawResultArtwork(rect, permanentKillTrackTexture, RunHudView.TimerTrackUv, tint);
            Rect fill = new Rect(rect.x + 2, rect.y + 1, rect.width - 4, rect.height - 2);
            void DrawFill(double fraction, Color color)
            {
                if (fraction <= 0) return;
                Rect part = fill; part.width *= (float)fraction;
                Rect uv = RunHudView.TimerFillUv; uv.width *= (float)fraction;
                if (permanentKillFillTexture != null) UiGraphics.DrawTimerTexture(part, permanentKillFillTexture, uv, color);
                else UiGraphics.DrawSolidRect(part, color);
            }
            DrawFill(RunResultPresentation.PermanentKillFraction(progress, displayedTotal), new Color(1f, .82f, .55f));
            DrawFill(RunResultPresentation.PermanentKillFraction(progress, progress.before), new Color(.55f, .55f, .55f));
            // Draw-only: no slider handle, GUI input control, or persistence writes.
        }

        internal int AnimatedResultCount(int target, int sequence, float elapsedTime)
        {
            if (settings.resultCountUpDuration <= 0f) return target;
            return RunResultPresentation.CountUp(target, elapsedTime, settings.resultCountUpDuration, sequence * Mathf.Max(0f, settings.resultCountUpStagger));
        }

        internal void DrawResultEdgeGlow(float panelWidth, float panelHeight, float elapsedTime)
        {
            if (resultGlowTexture == null || settings.resultGlowIntensity <= 0f) return;
            // Unscaled time keeps the count-up and glow playing while run.EndRun pauses gameplay.
            float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedTime / .35f));
            float pulse = settings.resultGlowPulseDuration > 0f
                ? .9f + .1f * Mathf.Sin(elapsedTime * Mathf.PI * 2f / settings.resultGlowPulseDuration)
                : 1f;
            Color tint = new Color(1f, 1f, 1f, settings.resultGlowIntensity * fadeIn * pulse);
            Rect uv = new Rect(0f, 0f, 1f, 1f);
            // Draw beneath all labels and buttons: their colors and hit areas remain unchanged.
            UiGraphics.DrawTimerTexture(new Rect(-70f, -85f, panelWidth + 140f, 170f), resultGlowTexture, uv, tint);
            UiGraphics.DrawTimerTexture(new Rect(-70f, panelHeight - 85f, panelWidth + 140f, 170f), resultGlowTexture, uv, tint);
        }

        internal void DrawMonsterResultIcon(Rect rect, MonsterKind kind)
        {
            Sprite sprite = art.MonsterSprite(kind);
            UiGraphics.DrawResultSprite(rect, sprite);
        }
    }
}
