using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Displays mana, current statistics and navigation controls.</summary>
    internal sealed class RunHudView
    {
        private readonly GameSettings settings;
        private readonly SaveService save;
        private readonly GameTables tables;
        private readonly RunController run;
        private readonly EnemySystem enemiesSystem;
        private readonly CombatStats stats;
        private readonly LevelUpController selection;
        private readonly UiGraphics graphics;
        private readonly ManaBarRenderer manaBar;

        internal RunHudView(GameSettings settings, SaveService save, GameTables tables, RunController run, EnemySystem enemiesSystem, CombatStats stats, LevelUpController selection, UiGraphics graphics, ManaBarRenderer manaBar)
        {
            this.settings = settings;
            this.save = save;
            this.tables = tables;
            this.run = run;
            this.enemiesSystem = enemiesSystem;
            this.stats = stats;
            this.selection = selection;
            this.graphics = graphics;
            this.manaBar = manaBar;
            timerFillTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_TimerFill_v1");
        }

        internal Texture2D timerFillTexture;

        // UV crops select the artwork within the generated transparent canvases, preserving source PNGs.
        internal static readonly Rect TimerFillUv = new Rect(40f / 1774f, 411f / 887f, 1694f / 1774f, 65f / 887f);

        internal void DrawNavigationControls()
        {
            if (graphics.DrawButton(new Rect(Screen.width - 190, 20, 165, 36), "성장 지도  [TAB]")) run.ToggleMetaTree();
            if (settings.enablePrototypeDebug && !selection.choosingUpgrade && !run.showMetaTree &&
                graphics.DrawButton(new Rect(Screen.width - 190, 64, 165, 30), "디버그 레벨업")) selection.TriggerDebugLevelUp();
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
            GUI.Label(new Rect(x, y, width, 28f), $"레벨 {selection.level}    경험치 {CompactNumber.Format(selection.experience)}/{CompactNumber.Format(selection.experienceToNext)}    획득 재화 +{CompactNumber.Format(run.runCurrency)}    영구 재화 {CompactNumber.Format(save.metaCurrency)}", informationStyle);
            GUI.Label(new Rect(x, y + 28f, width, 26f), $"공격력 {stats.MainDamage:0.##}  |  공격 간격 {stats.AttackInterval:0.00}초  |  필드 {enemiesSystem.enemies.Count}/{enemiesSystem.FieldTarget}  |  마나 흡수 +{run.mana.AbsorbedThisRun:0.##} MP", informationStyle);
        }

        internal void DrawManaBar()
        {
            Rect area = ManaHudRect(new Vector2(Screen.width, Screen.height));
            area.position += manaBar.Hit.Offset;
            float width = area.width;
            float y = area.y;
            float x = area.x;
            float remaining = run.mana.Current;
            float fraction = run.mana.Fraction;
            float impact = manaBar.Hit.Intensity;
            Color accent = Color.Lerp(Color.Lerp(manaBar.CurrentColor, Color.white, .35f), new Color(.6f, .92f, 1), impact);
            UiGraphics.DrawStaticLabel(new Rect(x, y - 3f, 34f, 30f), "MP", new GUIStyle(graphics.statStyle)
                { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = accent } });
            Rect track = new Rect(x + 36f, y, Mathf.Max(24, width - 174f), area.height);
            Rect fill = new Rect(track.x + 7f, track.y + 4f, Mathf.Max(0f, track.width - 14f), track.height - 8f);
            UiGraphics.DrawSolidRect(fill, new Color(.045f, .035f, .08f));
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
            // The transparent opening preserves the flowing liquid beneath the pixel frame.
            PixelUiArt.Draw(track, PixelUiArt.Mana, 8, Color.white);

            if (impact > .002f)
            {
                Color shield = new Color(.38f, .82f, 1, impact * .85f);
                UiGraphics.DrawSolidRect(new Rect(track.x, track.y, track.width, 2), shield);
                UiGraphics.DrawSolidRect(new Rect(track.x, track.yMax - 2, track.width, 2), shield);
                UiGraphics.DrawSolidRect(new Rect(track.x, track.y, 2, track.height), shield);
                UiGraphics.DrawSolidRect(new Rect(track.xMax - 2, track.y, 2, track.height), shield);
            }

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
            UiGraphics.DrawStaticLabel(new Rect(track.xMax + 10f, y - 3f, 126f, 30f), text, timeStyle);
        }

        internal void DrawManaShieldHit() => manaBar.Hit.DrawShield(new Rect(0, 0, Screen.width, Screen.height));

        // Bottom-left, above the run information even at small sizes.
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
            UiGraphics.DrawStaticLabel(area, "경과 " + ElapsedTimeText(run.elapsed), style);
        }

    }
}
