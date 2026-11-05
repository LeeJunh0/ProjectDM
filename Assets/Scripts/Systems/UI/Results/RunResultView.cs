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
        private readonly RelicForgeMotion forgeMotion = new();
        private float lastForgeResult = float.NegativeInfinity;

        internal RunResultView(GameSettings settings, GameTables tables, WorldArtAssets art, RunController run, BossMilestone boss, PlayerCombatSystem playerCombat, UiGraphics graphics)
        {
            this.settings = settings;
            this.tables = tables;
            this.art = art;
            this.run = run;
            this.boss = boss;
            this.playerCombat = playerCombat;
            this.graphics = graphics;
            resultGlowTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_ResultGlowOrange_v1");
            permanentKillFillTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_TimerFill_v1");
        }

        internal Texture2D resultGlowTexture;
        internal Texture2D permanentKillFillTexture;

        internal void DrawRunResults()
        {
            if (run.relicForge.IsOpen) { DrawRelicForge(); return; }
            UiGraphics.DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(.015f, .018f, .04f, .6f));
            // The approved preview has a tall panel; scale the complete composition from 1920 x 1080.
            const float panelWidth = 720f;
            const float panelHeight = 910f;
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = previousMatrix * Matrix4x4.TRS(new Vector3((Screen.width - panelWidth * scale) * .5f, (Screen.height - panelHeight * scale) * .5f, 0f), Quaternion.identity, Vector3.one * scale);
            bool returnToMap = false;
            bool restart = false;
            try
            {
                PixelUiArt.Draw(new Rect(0f, 0f, panelWidth, panelHeight), PixelUiArt.Result, 28, Color.white);
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
                UiGraphics.DrawStaticLabel(new Rect(44f, 24f, 632f, 48f), boss.bossArrived ? "스테이지 보스 도달" : playerCombat.playerDefeated ? "피격으로 마나 고갈" : run.mana.IsDepleted ? "마나 고갈" : "진행 종료", heading);
                UiGraphics.DrawResultDivider(92f);
                UiGraphics.DrawStaticLabel(new Rect(44f, 116f, 632f, 34f), "획득 재화", new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.58f, .55f, .50f) } });

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
                UiGraphics.DrawStaticLabel(new Rect(currencyX + 82f, 151f, amountWidth + 4f, 90f), amount, currencyStyle);
                UiGraphics.DrawResultDivider(258f);

                int totalKills = 0;
                foreach (int count in run.monsterKillCounts) totalKills = (int)Math.Min(int.MaxValue, (long)totalKills + count);
                UiGraphics.DrawStaticLabel(new Rect(62f, 280f, 350f, 42f), "이번 런 총 처치", section);
                UiGraphics.DrawStaticLabel(new Rect(446f, 280f, 210f, 42f), $"{AnimatedResultCount(totalKills, 1, presentationElapsed):N0}", new GUIStyle(value) { fontSize = 30, fontStyle = FontStyle.Bold });
                UiGraphics.DrawStaticLabel(new Rect(136f, 322f, 520f, 18f), "이번 런 처치 · 영구 처치 스택", new GUIStyle(mutedBody) { fontSize = 16 });
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
                var summaryStyle = new GUIStyle(body) { fontSize = 21, alignment = TextAnchor.MiddleCenter };
                string experienceComparison = run.previousResult == null ? "" : " · " + RunResultPresentation.Change(run.runExperience, run.previousResult.experience);
                UiGraphics.DrawStaticLabel(new Rect(44f, 638f, 632f, 30f), "획득 경험치 " + CompactNumber.Format(run.runExperience) + experienceComparison, summaryStyle);
                if (run.previousResult != null)
                    UiGraphics.DrawStaticLabel(new Rect(44f, 670f, 632f, 26f), "동일 타입·스테이지 대비  재화 " + RunResultPresentation.Change(run.runCurrency, run.previousResult.coins)
                        + " · 처치 " + RunResultPresentation.Change(totalKills, run.previousResult.kills), new GUIStyle(summaryStyle) { fontSize = 19 });
                UiGraphics.DrawResultDivider(704f);
                if (graphics.DrawResultButton(new Rect(38f, 720f, 644f, 50f), "귀환 작업실 · 연금 가마", false)) run.ChangeScreen(() => run.relicForge.Open());
                returnToMap = graphics.DrawResultButton(new Rect(38f, 790f, 310f, 78f), "돌아가기", false);
                restart = graphics.DrawResultButton(new Rect(372f, 790f, 310f, 78f), "다시 시작", true);
            }
            finally { GUI.matrix = previousMatrix; }
            if (returnToMap) run.ReturnToGrowthMap();
            else if (restart) run.BeginRun();
        }

        private void DrawRelicForge()
        {
            var forge = run.relicForge;
            if (lastForgeResult != run.resultPresentationOpenedAt)
            { forgeMotion.Clear(); lastForgeResult = run.resultPresentationOpenedAt; }
            float now = Time.unscaledTime;
            bool busy = forgeMotion.Busy(now);
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            Matrix4x4 previous = GUI.matrix;
            DrawWorkshopRoom();
            GUI.matrix = previous * Matrix4x4.TRS(new Vector3((Screen.width - 1920 * scale) * .5f, (Screen.height - 1080 * scale) * .5f), Quaternion.identity, Vector3.one * scale);
            try
            {
                var count = new GUIStyle(graphics.statStyle) { fontSize = 28, alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(.94f, .89f, .8f) } };
                UiGraphics.DrawResultSprite(new Rect(1650, 64, 40, 40), art.coinSprite);
                UiGraphics.DrawStaticLabel(new Rect(1702, 64, 150, 40), CompactNumber.Format(forge.Gold), count);
                forgeMotion.DrawFragmentIcon(new Rect(1650, 118, 40, 44));
                UiGraphics.DrawStaticLabel(new Rect(1702, 120, 150, 40), CompactNumber.Format(forgeMotion.VisibleFragments(forge.Fragments, now)), count);

                // Leave headroom for the falling coins and crystal burst above the kettle.
                forgeMotion.Draw(new Rect(-264, -500, 2448, 1814.4f), now, forge.Pending > 0);

                bool enabled = GUI.enabled;
                try
                {
                    GUI.enabled = enabled && !busy;
                    if (busy) graphics.DrawResultButton(new Rect(1450, 820, 380, 70), "응축 중…", false);
                    else if (forge.Rolls == 0)
                    {
                        GUI.enabled = enabled && forge.BlockReason == null;
                        if (graphics.DrawResultButton(new Rect(1450, 820, 380, 70), ForgeEntryLabel(forge), true)) BeginForgeMotion(true);
                        GUI.enabled = enabled;
                        if (graphics.DrawResultButton(new Rect(1450, 910, 380, 70), "작업실 나가기", false)) LeaveWorkshop();
                    }
                    else if (forge.CanContinue)
                    {
                        if (graphics.DrawResultButton(new Rect(1450, 820, 380, 70), "조각 " + forge.Pending + "개 꺼내기", false)) { forge.Claim(); forgeMotion.Clear(); }
                        if (graphics.DrawResultButton(new Rect(1450, 910, 380, 70), (forge.Rolls == 1 ? 2 : 5) + "개로 응축 · 50%", true)) BeginForgeMotion(false);
                    }
                    else if (graphics.DrawResultButton(new Rect(1450, 910, 380, 70), "작업실 나가기", false)) LeaveWorkshop();
                }
                finally { GUI.enabled = enabled; }
            }
            finally { GUI.matrix = previous; }
        }

        private static void DrawWorkshopRoom()
        {
            UiGraphics.DrawSolidRect(new Rect(0, 0, Screen.width, Screen.height), new Color(.055f, .043f, .066f));
            float unit = Mathf.Max(.1f, Screen.height / 1080f), floor = Screen.height * .685f, rowHeight = 68 * unit;
            UiGraphics.DrawSolidRect(new Rect(0, floor, Screen.width, Screen.height - floor), new Color(.095f, .066f, .073f));
            UiGraphics.DrawSolidRect(new Rect(0, floor, Screen.width, 4 * unit), new Color(.16f, .105f, .10f));
            for (int row = 0; row < 6; row++)
            {
                float y = floor + row * rowHeight;
                UiGraphics.DrawSolidRect(new Rect(0, y, Screen.width, 2 * unit), new Color(.074f, .052f, .06f));
                for (float x = (row % 2 == 0 ? 120 : 275) * unit; x < Screen.width; x += 310 * unit)
                    UiGraphics.DrawSolidRect(new Rect(x, y, 2 * unit, rowHeight), new Color(.079f, .054f, .06f));
            }
        }

        private static string ForgeEntryLabel(RelicForge forge) => forge.Stake <= 0 ? "원정 수입 없음" :
            forge.Stake < forge.Minimum ? "최소 " + CompactNumber.Format(forge.Minimum) + " 금화 필요" :
            forge.Gold < forge.Stake ? "금화 부족" : forge.BlockReason != null ? "참가 불가" :
            CompactNumber.Format(forge.Stake) + " 금화 넣기 · 50%";

        private void BeginForgeMotion(bool deposit)
        {
            int before = run.relicForge.Fragments;
            bool accepted = deposit ? run.relicForge.Begin() : run.relicForge.Continue();
            if (accepted) forgeMotion.Begin(run.relicForge.LastSucceeded, deposit, before, Time.unscaledTime, run.relicForge.Stake);
        }
        private void LeaveWorkshop() => run.ChangeScreen(() => { run.relicForge.Close(); forgeMotion.Clear(); });

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
                ? "이번 런: " + art.MonsterName(kind + 1, tables.balance) + " 해금!"
                : art.MonsterName(kind + 1, tables.balance) + " 해금 완료";
            return "다음: " + art.MonsterName(kind + 1, tables.balance);
        }

        private static void DrawFittedLabel(Rect rect, string text, GUIStyle source, int minimumSize)
        {
            GUIStyle style = new GUIStyle(source) { wordWrap = false, clipping = TextClipping.Clip };
            while (style.fontSize > minimumSize && style.CalcSize(new GUIContent(text)).x > rect.width) style.fontSize--;
            UiGraphics.DrawStaticLabel(rect, text, style);
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
            UiGraphics.DrawSolidRect(rect, new Color(.045f, .035f, .08f));
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
            PixelUiArt.Draw(rect, PixelUiArt.Mana, Mathf.Min(4, rect.height * .25f), tint);
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
