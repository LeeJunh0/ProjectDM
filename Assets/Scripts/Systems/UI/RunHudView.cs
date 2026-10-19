using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Displays display-only mana, current statistics, health and navigation controls.</summary>
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
        private readonly ManaBarRenderer manaBar;

        internal RunHudView(GameSettings settings, SaveService save, GameTables tables, RunController run, EnemySystem enemiesSystem, CombatStats stats, LevelUpController selection, PlayerCombatSystem playerCombat, UiGraphics graphics, ManaBarRenderer manaBar)
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
            this.manaBar = manaBar;
            timerTrackTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_TimerTrack_v1");
            timerFillTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_TimerFill_v1");
        }

        internal GUIStyle timerTrackStyle;

        internal Texture2D timerTrackTexture;

        internal Texture2D timerFillTexture;

        // UV crops select the artwork within the generated transparent canvases, preserving source PNGs.
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
            GUI.Label(new Rect(x, y + 28f, width, 26f), $"공격력 {stats.MainDamage:0.##}  |  공격 간격 {stats.AttackInterval:0.00}초  |  필드 {enemiesSystem.enemies.Count}/{enemiesSystem.FieldTarget}  |  마나 흡수 +{run.mana.AbsorbedThisRun:0.##} MP", informationStyle);
        }

        internal void DrawManaBar()
        {
            Rect area = ManaHudRect(new Vector2(Screen.width, Screen.height));
            float width = area.width;
            float y = area.y;
            float x = area.x;
            float remaining = run.mana.Current;
            float fraction = run.mana.Fraction;
            Color accent = Color.Lerp(manaBar.CurrentColor, Color.white, .35f);
            GUI.Label(new Rect(x, y - 3f, 34f, 30f), "MP", new GUIStyle(graphics.statStyle)
                { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = accent } });
            Rect track = new Rect(x + 36f, y, Mathf.Max(24, width - 174f), area.height);
            DrawTimerTrack(track);
            Rect fill = new Rect(track.x + 5f, track.y + 4f, Mathf.Max(0f, track.width - 10f), track.height - 8f);
            Texture liquid = Event.current.type == EventType.Repaint
                ? manaBar.Render(Mathf.CeilToInt(fill.width), Mathf.CeilToInt(fill.height)) : null;
            if (liquid != null) GUI.DrawTexture(fill, liquid, ScaleMode.StretchToFill, true);
            else if (fraction > 0f && Event.current.type == EventType.Repaint)
            {
                Rect fillUv = TimerFillUv;
                fillUv.width *= fraction;
                fill.width *= fraction;
                UiGraphics.DrawSolidRect(fill, manaBar.CurrentColor);
                if (timerFillTexture != null) UiGraphics.DrawTimerTexture(fill, timerFillTexture, fillUv, new Color(1, 1, 1, .12f));
            }

            // Display-only slider: no handle, keyboard focus or pointer interaction.
            timerTrackStyle ??= new GUIStyle(GUIStyle.none) { fixedHeight = 32f };
            bool wasEnabled = GUI.enabled;
            GUI.enabled = false;
            GUI.HorizontalSlider(track, fraction, 0f, 1f, timerTrackStyle, GUIStyle.none);
            GUI.enabled = wasEnabled;

            GUIStyle timeStyle = new GUIStyle(graphics.statStyle)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = accent }
            };
            string text = remaining.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "/" +
                run.mana.Maximum.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            while (timeStyle.fontSize > 10 && timeStyle.CalcSize(new GUIContent(text)).x > 126) timeStyle.fontSize--;
            GUI.Label(new Rect(track.xMax + 10f, y - 3f, 126f, 30f), text, timeStyle);
        }

        // Bottom-left above health. Even at small sizes its bottom stays above right-side run information.
        internal static Rect ManaHudRect(Vector2 screen) => new(22, Mathf.Max(110, screen.y - 118),
            Mathf.Min(480, Mathf.Max(200, screen.x - 44)), 32);

        internal static Rect ElapsedHudRect(Vector2 screen) => new(22, 20, Mathf.Min(240, Mathf.Max(120, screen.x - 234)), 32);

        internal static string ElapsedTimeText(float elapsed)
        {
            // mm:ss. This is elapsed play time, never remaining mana or the boss countdown.
            double safe = Math.Max(0, elapsed);
            long seconds = (long)Math.Floor(safe);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        internal void DrawElapsedTime()
        {
            Rect area = ElapsedHudRect(new Vector2(Screen.width, Screen.height));
            GUIStyle style = new GUIStyle(graphics.statStyle)
            {
                fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(.9f, .92f, .96f) }
            };
            GUI.Label(area, "경과 " + ElapsedTimeText(run.elapsed), style);
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
            UiGraphics.DrawSolidRect(new Rect(bar.x + 1, bar.y + 1, (bar.width - 2) * Mathf.Clamp01(playerCombat.playerHealth / playerCombat.MaxHealth), 8), new Color(.85f, .25f, .22f));
            GUI.Label(new Rect(bar.x, bar.y - 24, bar.width, 22), $"체력 {playerCombat.playerHealth:0.##}/{playerCombat.MaxHealth:0.##}",
                new GUIStyle(graphics.statStyle) { fontSize = 14, normal = { textColor = new Color(1, .78f, .74f) } });
        }
    }
}
