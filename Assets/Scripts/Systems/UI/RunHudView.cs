using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Displays the timer, current statistics, health and navigation controls.</summary>
    internal sealed class RunHudView
    {
        private readonly GameSettings settings;
        private readonly SaveService save;
        private readonly GameTables tables;
        private readonly RunController run;
        private readonly EnemySystem enemiesSystem;
        private readonly CombatStats stats;
        private readonly LevelUpController selection;
        private readonly PlayerCombatSystem playerCombat;
        private readonly UiGraphics graphics;

        internal RunHudView(GameSettings settings, SaveService save, GameTables tables, RunController run, EnemySystem enemiesSystem, CombatStats stats, LevelUpController selection, PlayerCombatSystem playerCombat, UiGraphics graphics)
        {
            this.settings = settings;
            this.save = save;
            this.tables = tables;
            this.run = run;
            this.enemiesSystem = enemiesSystem;
            this.stats = stats;
            this.selection = selection;
            this.playerCombat = playerCombat;
            this.graphics = graphics;
            timerIconTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_TimerIcon_v1");
            timerTrackTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_TimerTrack_v1");
            timerFillTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_TimerFill_v1");
        }

        internal GUIStyle timerTrackStyle;

        internal Texture2D timerIconTexture;

        internal Texture2D timerTrackTexture;

        internal Texture2D timerFillTexture;

        // UV crops select the artwork within the generated transparent canvases, preserving source PNGs.
        private static readonly Rect TimerIconUv = new Rect(129f / 1254f, 39f / 1254f, 994f / 1254f, 1152f / 1254f);

        internal static readonly Rect TimerTrackUv = new Rect(40f / 2172f, 309f / 724f, 2092f / 2172f, 119f / 724f);

        internal static readonly Rect TimerFillUv = new Rect(40f / 1774f, 411f / 887f, 1694f / 1774f, 65f / 887f);

        internal void DrawNavigationControls()
        {
            if (GUI.Button(new Rect(Screen.width - 190, 20, 165, 36), "성장 지도  [TAB]")) run.ToggleMetaTree();
            if (settings.enablePrototypeDebug && !selection.choosingUpgrade && !run.showMetaTree &&
                GUI.Button(new Rect(Screen.width - 190, 64, 165, 30), "디버그 레벨업")) selection.TriggerDebugLevelUp();
        }

        internal void DrawRunHudInformation()
        {
            float width = Mathf.Min(650f, Screen.width - 44f);
            float x = Screen.width - width - 22f;
            float y = Screen.height - 78f;
            GUIStyle informationStyle = new GUIStyle(graphics.statStyle)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = Screen.width < 650 ? 12 : 15
            };
            GUI.Label(new Rect(x, y, width, 28f), $"레벨 {selection.level}    경험치 {selection.experience:0.##}/{selection.experienceToNext}    획득 재화 +{run.runCurrency}    영구 재화 {save.metaCurrency}", informationStyle);
            GUI.Label(new Rect(x, y + 28f, width, 26f), $"공격력 {stats.MainDamage:0.##}  |  공격 간격 {stats.AttackInterval:0.00}초  |  필드 {enemiesSystem.enemies.Count}/{enemiesSystem.FieldTarget}  |  보충 +{run.timeRewardsThisRun:0.##}초", informationStyle);
        }

        internal void DrawRunTimer()
        {
            float width = Mathf.Min(480f, Mathf.Max(140f, Screen.width - 234f));
            float y = 24f;
            float x = 22f;
            float remaining = Mathf.Max(0f, run.activeRunDuration - run.elapsed);
            float fraction = run.remainingTimeFraction;
            Color accent = remaining <= 10f ? new Color(.96f, .38f, .23f) : new Color(.95f, .72f, .28f);

            Color artworkTint = remaining <= 10f ? new Color(1f, .52f, .35f) : Color.white;
            if (timerIconTexture != null)
            {
                UiGraphics.DrawTimerTexture(new Rect(x, y - 3f, 26f, 30f), timerIconTexture, TimerIconUv, artworkTint);
            }
            Rect track = new Rect(x + 36f, y, width - 119f, 24f);
            DrawTimerTrack(track);
            Rect fill = new Rect(track.x + 5f, track.y + 4f, Mathf.Max(0f, track.width - 10f), track.height - 8f);
            if (timerFillTexture != null && fraction > 0f)
            {
                Rect fillUv = TimerFillUv;
                fillUv.width *= fraction;
                fill.width *= fraction;
                UiGraphics.DrawTimerTexture(fill, timerFillTexture, fillUv, artworkTint);
            }

            // Display-only slider: no handle, keyboard focus or pointer interaction.
            timerTrackStyle ??= new GUIStyle(GUIStyle.none) { fixedHeight = 24f };
            bool wasEnabled = GUI.enabled;
            GUI.enabled = false;
            GUI.HorizontalSlider(track, fraction, 0f, 1f, timerTrackStyle, GUIStyle.none);
            GUI.enabled = wasEnabled;

            GUIStyle timeStyle = new GUIStyle(graphics.statStyle)
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = accent }
            };
            GUI.Label(new Rect(track.xMax + 12f, y - 3f, 71f, 30f), remaining.ToString("00.00", System.Globalization.CultureInfo.InvariantCulture), timeStyle);
        }

        internal void DrawTimerTrack(Rect rect)
        {
            if (timerTrackTexture == null) return;
            // Three horizontal slices keep the end caps intact when the HUD width changes.
            float capWidth = Mathf.Min(8f, rect.width * .5f);
            float uvCapWidth = 36f / 2172f;
            UiGraphics.DrawTimerTexture(new Rect(rect.x, rect.y, capWidth, rect.height), timerTrackTexture,
                new Rect(TimerTrackUv.x, TimerTrackUv.y, uvCapWidth, TimerTrackUv.height), Color.white);
            UiGraphics.DrawTimerTexture(new Rect(rect.x + capWidth, rect.y, rect.width - capWidth * 2f, rect.height), timerTrackTexture,
                new Rect(TimerTrackUv.x + uvCapWidth, TimerTrackUv.y, TimerTrackUv.width - uvCapWidth * 2f, TimerTrackUv.height), Color.white);
            UiGraphics.DrawTimerTexture(new Rect(rect.xMax - capWidth, rect.y, capWidth, rect.height), timerTrackTexture,
                new Rect(TimerTrackUv.xMax - uvCapWidth, TimerTrackUv.y, uvCapWidth, TimerTrackUv.height), Color.white);
        }

        internal void DrawPlayerHealth()
        {
            if (!settings.enablePlayerContactDamage) return;
            Rect bar = new Rect(22, Screen.height - 42, Mathf.Min(200, Screen.width - 44), 10);
            UiGraphics.DrawSolidRect(bar, new Color(.06f, .03f, .04f, .9f));
            UiGraphics.DrawSolidRect(new Rect(bar.x + 1, bar.y + 1, (bar.width - 2) * Mathf.Clamp01(playerCombat.playerHealth / tables.balance.playerHealth), 8), new Color(.85f, .25f, .22f));
            GUI.Label(new Rect(bar.x, bar.y - 24, bar.width, 22), $"체력 {playerCombat.playerHealth:0.##}/{tables.balance.playerHealth:0.##}",
                new GUIStyle(graphics.statStyle) { fontSize = 14, normal = { textColor = new Color(1, .78f, .74f) } });
        }
    }
}
