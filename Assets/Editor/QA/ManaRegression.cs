using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    /// <summary>Mana, run-clock and legacy table/save checks. Memory-only persistence, no scene writes.</summary>
    public static class ManaRegression
    {
        private static int checks;
        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            checks++;
        }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;

        [MenuItem("Project DM/검증/마나 런 검증")]
        public static void RunMenu() { Verify(); Debug.Log($"Mana regression: {checks} assertions PASS."); }
        public static void RunBatch()
        {
            try
            {
                Verify(); Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/ManaRegression-result.txt", $"PASS: {checks} assertions; attack-only cost/no passive drain, capped recovery, pause, game over, independent boss clock, legacy JSON/growth/save compatibility.\n");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/ManaRegression-result.txt", "FAIL: " + exception);
                Debug.LogException(exception); EditorApplication.Exit(1);
            }
        }

        private static void Verify()
        {
            checks = 0;
            float previousScale = Time.timeScale;
            try
            {
                Check(RunHudView.ElapsedTimeText(0) == "00:00", "Elapsed HUD starts at zero");
                Check(RunHudView.ElapsedTimeText(65.25f) == "01:05", "Elapsed HUD is minutes/seconds without fractional digits");
                Check(RunHudView.ElapsedTimeText(59.99f) == "00:59", "Fractional seconds do not round up prematurely");
                Check(RunHudView.ElapsedTimeText(60) == "01:00", "Whole minute rolls over correctly");
                Check(RunHudView.ElapsedTimeText(180) == "03:00", "Boss elapsed milestone displays independently of MP");
                Check(RunHudView.ElapsedTimeText(-1) == "00:00", "Elapsed HUD cannot display a negative time");
                foreach (Vector2 screen in new[] { new Vector2(1920, 1080), new Vector2(1280, 720), new Vector2(960, 540), new Vector2(724, 400) })
                {
                    Rect manaArea = RunHudView.ManaHudRect(screen), elapsedArea = RunHudView.ElapsedHudRect(screen);
                    Rect healthArea = new Rect(22, screen.y - 66, 200, 34);
                    Rect infoArea = new Rect(screen.x - Mathf.Min(650, screen.x - 44) - 22, screen.y - 78, Mathf.Min(650, screen.x - 44), 54);
                    Rect manaContents = new Rect(manaArea.x, manaArea.y - 3, manaArea.width, manaArea.height + 3);
                    Check(manaContents.xMin >= 0 && manaContents.xMax <= screen.x && manaContents.yMin > screen.y / 2 && manaContents.yMax < screen.y, "MP stays bottom-left and inside supported game views");
                    Check(!manaContents.Overlaps(healthArea) && !manaContents.Overlaps(infoArea), "MP avoids health and right-side information at " + screen);
                    Check(elapsedArea.yMax < 60 && !elapsedArea.Overlaps(new Rect(screen.x - 190, 20, 165, 36)), "Elapsed time stays upper-left clear of notices and navigation");
                }
                GameSettings settings = new GameSettings();
                GameTables tables = GameTables.LoadResources(settings);
                SaveService save = new SaveService(tables, new MemoryProgressStore(), true); save.Load();
                ProgressionService growth = new ProgressionService(save, tables); growth.ResetSnapshot();
                RunMana mana = new RunMana(); mana.Reset(40, 1);
                bool choosing = false;
                RunController run = new RunController(settings, save, tables, growth, null,
                    () => { }, () => { }, () => { }, () => { }, () => choosing = false, () => choosing, mana);
                Check(Near(mana.Current, 40) && Near(mana.Maximum, 40) && Near(mana.Fraction, 1), "A new run starts at full MP");
                Check(Near(run.TickClock(5), 5) && Near(mana.Current, 40) && Near(run.elapsed, 5), "Time advances without any passive MP drain");
                for (int i = 0; i < 5; i++) Check(mana.SpendForBasicAttack(), "Each actual volley pays its cost");
                Check(Near(mana.Absorb(1), 1) && Near(mana.Current, 36) && Near(run.elapsed, 5), "One MP absorption does not advance boss clock");
                Check(Near(mana.Absorb(100), 4) && Near(mana.Current, 40) && Near(mana.AbsorbedThisRun, 5), "Overfill clamps and counts only actual recovery");
                Check(mana.Absorb(1) == 0 && Near(mana.AbsorbedThisRun, 5), "Full mana wastes recovery without misleading gain statistics");
                run.showMetaTree = true;
                Check(run.TickClock(5) == 0 && Near(mana.Current, 40) && Near(run.elapsed, 5), "Growth map pauses mana and boss clock");
                run.showMetaTree = false; choosing = true;
                Check(run.TickClock(5) == 0 && Near(mana.Current, 40), "Level-up choice pauses mana");
                choosing = false;
                Check(run.TickClock(-1) == 0 && Near(mana.Current, 40), "Negative delta cannot restore mana");
                mana.Reset(1, 2); run.elapsed = 0;
                Check(Near(run.TickClock(100), 100) && Near(mana.Current, 1), "Long idle frames do not consume MP");
                Check(!mana.SpendForBasicAttack() && mana.IsDepleted, "Unaffordable attack depletes a fractional remainder instead of soft-locking the run");
                Check(mana.Absorb(10) == 0, "A late kill cannot revive zero MP");
                int bossCalls = 0;
                run.CompleteIfDue(() => bossCalls++);
                Check(run.runEnded && bossCalls == 0 && save.progress.completedRuns == 1 && Time.timeScale == 0, "Zero MP ends run and opens paused results");
                run.CompleteIfDue(() => bossCalls++); run.EndRun();
                Check(save.progress.completedRuns == 1 && run.TickClock(1) == 0, "Game over records once and no longer drains");
                run.runEnded = false; run.elapsed = 0; mana.Reset(1000, 1);
                run.CompleteIfDue(() => bossCalls++);
                Check(bossCalls == 0, "A huge mana pool does not immediately cause boss arrival");
                run.TickClock(tables.balance.bossElapsedSeconds);
                run.CompleteIfDue(() => bossCalls++);
                Check(bossCalls == 1 && Near(run.elapsed, tables.balance.bossElapsedSeconds) && mana.Current > 0,
                    "Only actual elapsed boss limit triggers arrival, regardless of remaining MP");
                run.elapsed = tables.balance.bossElapsedSeconds - 1; mana.Reset(1, 1);
                run.TickClock(1); mana.SpendForBasicAttack(); run.CompleteIfDue(() => bossCalls++);
                Check(bossCalls == 2, "Boss arrival retains priority when depletion and elapsed limit coincide");
                mana.Reset(40, 1);
                Check(mana.AbsorbedThisRun == 0 && mana.Current == mana.Maximum, "Restart clears MP recovery statistics and refills capacity");
                PrototypeBalance legacy = JsonUtility.FromJson<PrototypeBalance>("{\"initialSeconds\":55,\"timeRewardChance\":0.17,\"timeRewardSeconds\":2}");
                Check(Near(legacy.ResolvedInitialMana, 55) && Near(legacy.ResolvedManaAbsorbChance, .17f) && Near(legacy.ResolvedManaAbsorbAmount, 2), "Old custom JSON time values migrate semantically to MP");
                PrototypeBalance current = JsonUtility.FromJson<PrototypeBalance>("{\"initialMana\":70,\"manaAbsorbChance\":0.2,\"manaAbsorbAmount\":3,\"initialSeconds\":55}");
                Check(Near(current.ResolvedInitialMana, 70) && Near(current.ResolvedManaAbsorbChance, .2f) && Near(current.ResolvedManaAbsorbAmount, 3), "Explicit mana keys override legacy fields");
                save.progress.SetRank("time", 2); save.progress.SetRank("extension", 3);
                growth.ResetSnapshot();
                Check(Near(growth.PermanentAmount("time"), 4) && Near(growth.PermanentAmount("extension"), .006f), "Existing purchased time/extension ranks retain their mana effects");
                GrowthGraphView view = new GrowthGraphView(settings, save, tables, growth, null, run, new GameNotifications(), new UiGraphics());
                Check(view.GrowthAmountText(tables.GrowthRow("time"), 2) == "44 MP", "Capacity preview is MP, not seconds");
                Check(view.GrowthAmountText(tables.GrowthRow("extension"), 3).Contains("+1 MP"), "Recovery preview is MP, not seconds");
                Check(tables.GrowthRow("time").name == "최대 마나" && tables.GrowthRow("extension").name == "처치 마나 흡수", "Growth titles match wizard concept");
                foreach (string field in new[] { "initialMana", "basicAttackManaCost", "manaAbsorbChance", "manaAbsorbAmount" })
                {
                    PrototypeBalance bad = JsonUtility.FromJson<PrototypeBalance>(Resources.Load<TextAsset>("ProjectDM/Data/PrototypeBalance").text);
                    typeof(PrototypeBalance).GetField(field).SetValue(bad, field == "manaAbsorbChance" ? 1.1f : -2f);
                    Check(PrototypeRules.Validate(bad, tables.skillTable, tables.growthTable) != null, "Reject malformed mana table: " + field);
                }
            }
            finally { Time.timeScale = previousScale; }
        }
    }
}
