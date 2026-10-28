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
        private readonly WorldArtAssets art;
        private readonly PickupFlightPresentation pickupFlights;
        private readonly Dictionary<string, Texture2D> portraits = new();

        internal RunHudView(GameSettings settings, SaveService save, GameTables tables, RunController run, EnemySystem enemiesSystem, CombatStats stats, LevelUpController selection, UiGraphics graphics, ManaBarRenderer manaBar, WorldArtAssets art)
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
            this.art = art;
            pickupFlights = new PickupFlightPresentation(settings);
            timerFillTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_TimerFill_v1");
            foreach (string type in new[] { "projectile", "flame", "frost", "lightning" })
                portraits[type] = Resources.Load<Texture2D>("ProjectDM/UI/Portraits/" + type);
        }

        internal Texture2D timerFillTexture;

        // UV crops select the artwork within the generated transparent canvases, preserving source PNGs.
        internal static readonly Rect TimerFillUv = new Rect(40f / 1774f, 411f / 887f, 1694f / 1774f, 65f / 887f);

        internal void DrawNavigationControls()
        {
            Rect navigation = NavigationHudRect(new Vector2(Screen.width, Screen.height));
            if (graphics.DrawButton(navigation, "성장 지도  [TAB]")) run.ToggleMetaTree();
            if (settings.enablePrototypeDebug && !selection.choosingUpgrade && !run.showMetaTree &&
                graphics.DrawButton(new Rect(navigation.x, navigation.yMax + 8, navigation.width, 30), "디버그 레벨업")) selection.TriggerDebugLevelUp();
        }

        internal void DrawCurrencyHud()
        {
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            Rect area = CurrencyHudRect(screen);
            Rect icon = CurrencyIconRect(screen);
            Vector2 size = icon.size * (1 + pickupFlights.CurrencyPulse * .14f);
            UiGraphics.DrawResultSprite(new Rect(icon.center - size * .5f, size), art.coinSprite);
            float textX = area.x + 52f, textWidth = area.width - 52f;
            var amount = new GUIStyle(graphics.statStyle)
            {
                fontSize = 24, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft, wordWrap = false,
                normal = { textColor = new Color(1f, .86f, .53f) }
            };
            string value = CompactNumber.Format(Math.Floor(Math.Max(0, save.metaCurrency - pickupFlights.PendingCurrency + .000001)));
            while (amount.fontSize > 12 && amount.CalcSize(new GUIContent(value)).x > textWidth) amount.fontSize--;
            UiGraphics.DrawStaticLabel(new Rect(textX, area.y, textWidth, area.height), value, amount);
        }

        internal static Rect CurrencyHudRect(Vector2 screen)
        {
            float width = Mathf.Min(240f, Mathf.Max(0, screen.x - 44f));
            return new Rect(screen.x - width - 22f, screen.x < 600 ? 62f : 20f, width, 44f);
        }

        internal static Rect CurrencyIconRect(Vector2 screen)
        {
            Rect area = CurrencyHudRect(screen);
            return new Rect(area.x, area.center.y - 20f, 40f, 40f);
        }

        internal static Rect NavigationHudRect(Vector2 screen)
        {
            Rect currency = CurrencyHudRect(screen);
            return new Rect(currency.xMax - 165f, currency.yMax + 10f, 165f, 36f);
        }

        // Pickup-flight destinations in top-left GUI screen coordinates.
        internal Vector2 CurrencyPickupTarget => CurrencyIconRect(new Vector2(Screen.width, Screen.height)).center;
        internal Vector2 ManaPickupTarget => ManaTrackRect(new Vector2(Screen.width, Screen.height)).center + manaBar.Hit.Offset;
        internal Vector2 ExperiencePickupTarget => ExperienceBadgeRect(new Vector2(Screen.width, Screen.height)).center;

        internal void ShowPickupFlight(Vector3 world, Sprite sprite, PickupKind kind, double amount)
        {
            if (!run.runEnded && !run.showMetaTree && run.transition?.Active != true) pickupFlights.Show(world, sprite, kind, amount);
        }

        internal void TickPickupFeedback(float dt)
        {
            if (run.runEnded || run.showMetaTree || run.transition?.Active == true) { pickupFlights.Clear(); return; }
            pickupFlights.Tick(dt, new Vector2(Screen.width, Screen.height), CurrencyPickupTarget, ManaPickupTarget, ExperiencePickupTarget);
        }

        internal void ResetPickupFeedback() => pickupFlights.Clear();

        internal void DrawPickupFlights()
        {
            if (!run.runEnded && !run.showMetaTree)
                pickupFlights.Draw(new Vector2(Screen.width, Screen.height), CurrencyPickupTarget, ManaPickupTarget, ExperiencePickupTarget);
        }

        internal void DrawRunHudInformation()
        {
            DrawExperienceBar();
            float width = Mathf.Min(650f, Screen.width - 44f);
            float x = Screen.width - width - 22f;
            float y = PortraitHudRect(new Vector2(Screen.width, Screen.height)).y - 56f;
            GUIStyle informationStyle = new GUIStyle(graphics.statStyle)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = Screen.width < 650 ? 12 : 15
            };
            GUI.Label(new Rect(x, y + 28f, width, 26f), $"공격력 {stats.MainDamage:0.##}  |  공격 간격 {stats.AttackInterval:0.00}초  |  필드 {enemiesSystem.enemies.Count}/{enemiesSystem.FieldTarget}  |  마나 흡수 +{run.mana.AbsorbedThisRun:0.##} MP", informationStyle);
        }

        internal void DrawExperienceBar()
        {
            ExperienceDisplay(out int level, out double experience, out int threshold);
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            Rect track = ExperienceTrackRect(screen);
            Color accent = new Color(.78f, .71f, 1f);
            // Keep the level inside the full-height experience rail.
            Rect fill = new Rect(track.x + 4, track.y + 3, Mathf.Max(0, track.width - 8), track.height - 6);
            UiGraphics.DrawSolidRect(fill, new Color(.045f, .035f, .08f));
            float fraction = Mathf.Clamp01((float)(experience / Mathf.Max(1, threshold)));
            float filledWidth = fill.width * fraction;
            UiGraphics.DrawSolidRect(new Rect(fill.x, fill.y, filledWidth, fill.height), new Color(.38f, .25f, .68f));
            UiGraphics.DrawSolidRect(new Rect(fill.x, fill.y, filledWidth, 2), new Color(.76f, .64f, 1f, .85f));
            UiGraphics.DrawSolidRect(new Rect(fill.x, fill.yMax - 1, filledWidth, 1), new Color(.22f, .15f, .38f));
            PixelUiArt.Draw(track, PixelUiArt.Mana, 7, new Color(.8f, .74f, .92f));
            if (pickupFlights.ExperiencePulse > .002f)
                UiGraphics.DrawSolidRect(fill, new Color(.8f, .7f, 1f, pickupFlights.ExperiencePulse * .3f));
            if (fraction > 0 && fraction < 1)
                UiGraphics.DrawSolidRect(new Rect(fill.x + filledWidth - 2, fill.y, 2, fill.height), new Color(.92f, .84f, 1f, .85f));

            Rect badge = ExperienceBadgeRect(screen);
            var style = new GUIStyle(graphics.statStyle)
                { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, normal = { textColor = new Color(.96f, .93f, 1f) } };
            UiGraphics.DrawStaticLabel(new Rect(badge.x + 1, badge.y + 1, badge.width, badge.height), $"Lv. {level}",
                new GUIStyle(style) { normal = { textColor = new Color(.08f, .045f, .13f) } });
            UiGraphics.DrawStaticLabel(badge, $"Lv. {level}", style);
        }

        internal void ExperienceDisplay(out int level, out double experience, out int threshold)
        {
            level = selection.level;
            experience = selection.experience - pickupFlights.PendingExperience;
            while (experience < 0 && level > 1) experience += PrototypeRules.XpThreshold(tables.balance, --level);
            experience = Math.Max(0, experience);
            threshold = PrototypeRules.XpThreshold(tables.balance, level);
        }

        internal void DrawManaBar()
        {
            string character = run.characters?.SelectedId ?? "projectile";
            if (portraits.TryGetValue(character, out Texture2D portrait) && portrait != null)
                GUI.DrawTexture(PortraitHudRect(new Vector2(Screen.width, Screen.height)), portrait, ScaleMode.ScaleToFit, true);
            Rect area = ManaHudRect(new Vector2(Screen.width, Screen.height));
            area.position += manaBar.Hit.Offset;
            float y = area.y;
            float remaining = run.runEnded ? run.mana.Current : Mathf.Max(0, run.mana.Current - (float)pickupFlights.PendingMana);
            float fraction = remaining / run.mana.Maximum;
            float impact = manaBar.Hit.Intensity;
            Color accent = Color.Lerp(Color.Lerp(manaBar.CurrentColor, Color.white, .35f), new Color(.6f, .92f, 1), impact);
            Rect track = ManaTrackRect(new Vector2(Screen.width, Screen.height));
            track.position += manaBar.Hit.Offset;
            Rect fill = new Rect(track.x + 7f, track.y + 4f, Mathf.Max(0f, track.width - 14f), track.height - 8f);
            UiGraphics.DrawSolidRect(fill, new Color(.045f, .035f, .08f));
            Texture liquid = Event.current.type == EventType.Repaint
                ? manaBar.Render(Mathf.CeilToInt(fill.width), Mathf.CeilToInt(fill.height), fraction) : null;
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
            if (pickupFlights.ManaPulse > .002f)
                UiGraphics.DrawSolidRect(new Rect(track.x + 7, track.y + 4, track.width - 14, track.height - 8),
                    new Color(.55f, .91f, 1f, pickupFlights.ManaPulse * .22f));

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
            float textWidth = Mathf.Max(20, track.width - 48);
            while (timeStyle.fontSize > 10 && timeStyle.CalcSize(new GUIContent(text)).x > textWidth) timeStyle.fontSize--;
            UiGraphics.DrawStaticLabel(new Rect(track.x + 10, y + 1, 28, 30), "MP", new GUIStyle(graphics.statStyle)
                { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = accent } });
            UiGraphics.DrawStaticLabel(new Rect(track.xMax - textWidth - 10, y + 1, textWidth, 30), text, timeStyle);
        }

        internal void DrawManaShieldHit() => manaBar.Hit.DrawShield(new Rect(0, 0, Screen.width, Screen.height));

        internal static Rect PortraitHudRect(Vector2 screen)
            => new(16, ExperienceTrackRect(screen).y - 176, 176, 176);

        internal static Rect ManaHudRect(Vector2 screen) => new(PortraitHudRect(screen).xMax + 12,
            ExperienceTrackRect(screen).y - 32, Mathf.Min(340, Mathf.Max(24, screen.x - 220)), 32);

        internal static Rect ManaTrackRect(Vector2 screen)
        {
            Rect area = ManaHudRect(screen);
            return area;
        }

        internal static Rect ExperienceTrackRect(Vector2 screen)
        {
            return new Rect(0, Mathf.Max(0, screen.y - 30f), Mathf.Max(0, screen.x), 30f);
        }

        internal static Rect ExperienceBadgeRect(Vector2 screen)
            => new Rect(screen.x * .5f - 48f, ExperienceTrackRect(screen).y, 96f, 30f);

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
