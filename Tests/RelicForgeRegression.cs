// Execute ProjectDM.Editor.RelicForgeRegression.Run in an isolated Unity project.
using System;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public static class RelicForgeRegression
    {
        private static int checks;
        private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        public static void Run()
        {
            try
            {
                var tables = GameTables.LoadResources(new GameSettings());
                var memory = new MemoryProgressStore();
                var save = new SaveService(tables, memory, true) { progress = new PrototypeSave(), metaCurrency = 5000 };
                bool result = false;
                double draw = 0;
                var forge = new RelicForge(save, () => result, () => draw);
                forge.RecordResult(1000);
                Check(!forge.Open() && forge.Best == 0 && !forge.Begin(), "Play cannot offer or enter the forge");
                result = true; forge.RecordResult(1000);
                Check(forge.Stake == 1000 && forge.Best == 1000 && forge.Minimum == 800, "Stake is the latest run income; minimum is 80% of best");
                Check(forge.Open() && forge.Begin() && forge.Pending == 1 && save.metaCurrency == 4000, "First win spends only this run's income and creates one unclaimed fragment");
                Check(!forge.Begin(), "The same run cannot pay a second entry stake");
                Check(forge.Continue() && forge.Pending == 2 && save.metaCurrency == 4000, "Second win gives two total fragments with no extra gold cost");
                Check(forge.Continue() && forge.Pending == 0 && forge.Fragments == 5 && !forge.CanContinue, "Third win automatically banks five total fragments");
                Check(!forge.Continue() && !forge.Claim(), "Max tier cannot repeat or double-claim");
                forge.Close(); forge.Open(); Check(!forge.Begin(), "Reopening cannot create another chance in the same run");
                forge.RecordResult(799); forge.Open();
                Check(forge.Begin() == false && forge.Minimum == 800 && forge.Best == 1000, "Below 80% is ineligible and preserves the best record");
                forge.RecordResult(800); forge.Open(); draw = .499999;
                Check(forge.Begin() && forge.Claim() && forge.Fragments == 6, "Exactly 80% can enter; values below 0.5 win");
                forge.RecordResult(1000); forge.Open(); draw = .5;
                int gold = save.metaCurrency;
                Check(forge.Begin() && forge.Pending == 0 && forge.Fragments == 6 && save.metaCurrency == gold - 1000, "0.5 loses the stake, preserving banked fragments");
                forge.RecordResult(800); forge.Open(); draw = 0; forge.Begin(); draw = .9;
                Check(forge.Continue() && forge.Pending == 0 && forge.Fragments == 6 && !forge.CanContinue, "A failed continuation loses the entire current unclaimed reward");
                forge.RecordResult(1001);
                Check(forge.Minimum == 801, "80% minimum must round upward, never below the threshold");
                save.metaCurrency = 800; forge.Open();
                Check(!forge.Begin() && save.metaCurrency == 800, "Insufficient wallet cannot spend gold");
                forge.RecordResult(0); forge.Open(); Check(!forge.Begin(), "Zero-income runs cannot forge for free");
                forge.RecordResult(int.MaxValue);
                Check(forge.Minimum == 1717986918, "Minimum calculation cannot overflow");
                Check(memory.writes == 0 && memory.saves == 0, "Sandbox must never alter persistent storage");

                var store = new MemoryProgressStore(); var persistent = new SaveService(tables, store, false); persistent.Load(); persistent.metaCurrency = 5000;
                var durable = new RelicForge(persistent, () => true, () => 0);
                durable.RecordResult(1000); durable.Open(); durable.Begin();
                var reloaded = new SaveService(tables, store, false); reloaded.Load();
                Check(reloaded.metaCurrency == 4000 && reloaded.progress.bestRunCurrency == 1000 && reloaded.progress.pendingRelicFragments == 1, "Gold, record and pending reward must survive reload");
                var recovered = new RelicForge(reloaded, () => false);
                Check(recovered.Fragments == 1 && recovered.Pending == 0 && !recovered.Open(), "Interrupted successful reward is banked once without reopening play-time entry");
                new RelicForge(reloaded, () => false);
                Check(reloaded.progress.relicFragments == 1, "Recovery cannot duplicate fragments");
                var state = UnityEngine.Random.state;
                int wins = 0;
                for (int i = 0; i < 1000; i++)
                {
                    var sample = new SaveService(tables, new MemoryProgressStore(), true) { progress = new PrototypeSave(), metaCurrency = 1 };
                    var f = new RelicForge(sample, () => true, () => i / 1000.0);
                    f.RecordResult(1); f.Open(); f.Begin(); if (f.Pending > 0) wins++;
                }
                Check(wins == 500 && JsonUtility.ToJson(state) == JsonUtility.ToJson(UnityEngine.Random.state), "Exact 50% split must not consume combat or loot RNG");
                var integrationSave = new SaveService(tables, new MemoryProgressStore(), true) { progress = new PrototypeSave(), metaCurrency = 500 };
                var run = new RunController(new GameSettings(), integrationSave, tables, null, null, null, null, null, () => { }, () => { }, () => false, new RunMana());
                run.runCurrency = 100; run.EndRun();
                Check(integrationSave.progress.bestRunCurrency == 100 && run.relicForge.Open() && run.relicForge.Begin(), "Actual EndRun must create the entry opportunity and record income");
                run.relicForge.Close(); run.EndRun();
                Check(run.relicForge.Open() && !run.relicForge.Begin(), "Repeated EndRun cannot reset the wager");
                run.showMetaTree = true; Check(!run.relicForge.Open() && !run.relicForge.Continue(), "Growth map cannot enter or continue forging");
                var motion = new RelicForgeMotion();
                motion.Begin(true, true, 0, 0);
                Check(motion.Busy(.7f) && !motion.Revealed(.7f) && motion.VisibleFragments(5, .7f) == 0, "A saved third win cannot reveal its reward before the animation");
                Check(motion.Revealed(.72f) && motion.VisibleFragments(5, .72f) == 5, "Reward appears when the crystal emerges from the kettle");
                Check(motion.Busy(1.39f) && !motion.Busy(1.4f), "Actions unlock only once the 1.4-second kettle motion settles");
                Check(motion.PhaseText(.1f).Contains("금화"), "First attempt describes melting expedition gold");
                motion.Begin(false, false, 5, 0);
                Check(!motion.PhaseText(.1f).Contains("금화"), "A continuation cannot imply paying more gold");
                motion.Clear(); Check(!motion.Busy(0) && motion.VisibleFragments(5, 0) == 5 && motion.PhaseText(0) == null, "Clearing motion restores the actual stored inventory");
                Check(RelicForgeMotion.CoinCount(0) == 0 && RelicForgeMotion.CoinCount(-10) == 0, "No money cannot create deposit coins");
                int previousCoins = 0;
                foreach (int amount in new[] { 1, 10, 100, 1000, 10000, 100000, 999999 })
                {
                    int coins = RelicForgeMotion.CoinCount(amount);
                    Check(coins > previousCoins && coins <= amount && coins <= 128, "Larger stakes must produce visibly larger bounded deposits");
                    previousCoins = coins;
                    for (int coin = 0; coin < coins; coin++)
                        Check(RelicForgeMotion.CoinPhase(.66f, coin, coins) >= 2 && .66f < RelicForgeMotion.RevealAt,
                            "Every coin must finish melting before the outcome appears");
                }
                Check(RelicForgeMotion.CoinCount(1) == 1 && RelicForgeMotion.CoinCount(int.MaxValue) == 128, "Tiny and extreme stakes remain safe");
                Check(RelicForgeMotion.CoinPhase(.3f, 0, 1) > 1 && RelicForgeMotion.CoinPhase(.3f, 0, 1) < 2, "Coins have a visible melting phase after landing");
                motion.Begin(true, true, 0, 0, 100);
                Check(motion.DepositCoins == RelicForgeMotion.CoinCount(100), "Animation snapshots the actual stake's coin count");
                motion.Begin(true, false, 0, 0, 100000);
                Check(motion.DepositCoins == 0, "Condensing the same material cannot deposit any additional money");
                var coinTexture = Resources.Load<Texture2D>("ProjectDM/UI/RelicForge/forge-coin-v1");
                Check(coinTexture != null && coinTexture.width == 32 && coinTexture.height == 32 && coinTexture.filterMode == FilterMode.Point,
                    "Forge deposits must load their dedicated transparent 32-pixel coin with point filtering");
                var kettleTexture = Resources.Load<Texture2D>("ProjectDM/UI/RelicForge/forge-effects");
                Check(kettleTexture != null && kettleTexture.width == 1920 && kettleTexture.height == 2016 && kettleTexture.filterMode == FilterMode.Point,
                    "Five kettle clips must import at native pixel size without filtering blur: " + (kettleTexture == null ? "missing" : kettleTexture.width + "x" + kettleTexture.height + " / " + kettleTexture.filterMode));
                Debug.Log("RELIC FORGE PASSED: " + checks + " assertions, deterministic 50% split, persistence, recovery and real EndRun");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
