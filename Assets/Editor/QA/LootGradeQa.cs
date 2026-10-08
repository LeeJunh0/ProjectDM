using System;
using System.IO;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ProjectDM.Editor
{
    /// <summary>Actual prefab/loot/collection QA with memory saves; never writes scenes or PlayerPrefs.</summary>
    public static class LootGradeQa
    {
        private static int checks;
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        [MenuItem("Project DM/검증/드롭 등급 QA")]
        public static void RunMenu() { Execute(); Debug.Log($"Loot grade QA: {checks} assertions PASS."); }
        public static void RunBatch()
        {
            try { Execute(); EditorApplication.Exit(0); }
            catch (Exception e) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/LootGradeQa-error.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1); }
        }
        private static void Execute()
        {
            checks = 0;
            UnityEngine.Random.State originalRandomState = UnityEngine.Random.state;
            GameObject root = new GameObject("Loot grade QA memory-only");
            try
            {
                GameTables tables = new GameTables(
                    JsonUtility.FromJson<PrototypeBalance>(Resources.Load<TextAsset>("ProjectDM/Data/PrototypeBalance").text),
                    JsonUtility.FromJson<PrototypeSkillTable>(Resources.Load<TextAsset>("ProjectDM/Data/PrototypeSkills").text),
                    JsonUtility.FromJson<PrototypeGrowthTable>(Resources.Load<TextAsset>("ProjectDM/Data/PrototypeGrowth").text));
                Check(PrototypeRules.Validate(tables.balance, tables.skillTable, tables.growthTable) == null, "Runtime tables invalid");
                foreach (PrototypeMonster monster in tables.balance.monsters) monster.bonusChance = 0;
                tables.balance.chestChance = 0;
                GameSettings settings = new GameSettings();
                WorldArtAssets art = new WorldArtAssets { runtimeAssets = new ProjectDMRuntimeAssets() };
                art.runtimeAssets.CurrencyPickupPrefabs = new GameObject[5]; art.runtimeAssets.ExperiencePickupPrefabs = new GameObject[5];
                for (int i = 0; i < 5; i++)
                {
                    art.runtimeAssets.CurrencyPickupPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GameContent/Prefabs/CurrencyPickup_{i + 1:00}.prefab");
                    art.runtimeAssets.ExperiencePickupPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GameContent/Prefabs/ExperiencePickup_{i + 1:00}.prefab");
                    Check(art.runtimeAssets.CurrencyPickupPrefabs[i] != null && art.runtimeAssets.ExperiencePickupPrefabs[i] != null, "Existing grade prefab missing");
                }
                MemoryProgressStore store = new MemoryProgressStore();
                MemoryProgressStore oldStore = new MemoryProgressStore();
                PrototypeSave oldProgress = new PrototypeSave { version = 3, completedRuns = 7, playedSeconds = 321 };
                oldProgress.SetRank("damage", 4, .3); oldProgress.kills[0] = 120;
                oldStore.strings[SaveService.PrototypeSaveKey] = JsonUtility.ToJson(oldProgress);
                oldStore.integers["PROJECT_DM_META_GOLD"] = 91;
                SaveService retained = new SaveService(tables, oldStore, false); retained.Load();
                Check(retained.progress.version == 3 && retained.progress.Rank("damage") == 4 && Math.Abs(retained.progress.PartialRank("damage") - .3) < .000001, "Existing V3 growth or fractional progress lost");
                Check(retained.metaCurrency == 91 && retained.progress.kills[0] == 120 && retained.progress.completedRuns == 7 && retained.progress.playedSeconds == 321, "Existing V3 currency/kills/run statistics lost");
                Check(retained.progress.Rank("currencyGradeChance") == 0 && retained.progress.Rank("experienceGradeChance") == 0, "New grade growth must start at zero without resetting old progress");
                PrototypeSave previous = new PrototypeSave { version = 3 };
                previous.SetRank("currencyGradeChance", 4, .3);
                oldStore.strings[SaveService.PrototypeSaveKey] = JsonUtility.ToJson(previous);
                retained.Load();
                Check(retained.progress.Rank("silverDropChance") == 4 && Math.Abs(retained.progress.PartialRank("silverDropChance") - .3) < .000001, "Previous paid currency growth must migrate to silver without losing fractional ranks");
                Check(retained.progress.Rank("currencyGradeChance") == 4 && retained.progress.currencyDropsVersion == 1, "Archive previous ranks and mark migration once");
                SaveService save = new SaveService(tables, store, true); save.Load();
                ProgressionService progression = new ProgressionService(save, tables); progression.ResetSnapshot();
                PlayerStats playerStats = new PlayerStats(tables, progression);
                PlayerWorld world = new PlayerWorld(settings, tables, art, playerStats);
                RunController run = new RunController(settings, save, tables, progression, world, () => { }, () => { }, () => { }, () => { }, () => { }, () => false);
                float xp = 0;
                GameObjectPool pool = root.AddComponent<GameObjectPool>();
                PickupSystem pickups = new PickupSystem(settings, save, tables, progression, art, world, run, pool, amount => xp += amount, playerStats);
                GrowthGraphView view = new GrowthGraphView(settings, save, tables, progression, art, run, new GameNotifications(), new UiGraphics());
                Check(view.GrowthAmountText(tables.GrowthRow("experienceGradeChance"), 1).Contains("2% / 한 단계 상승"), "XP promotion tooltip changed");
                for (int grade = 2; grade <= 5; grade++)
                    Check(view.GrowthAmountText(tables.GrowthRow(LootGradeRules.CurrencyGrowthId(grade)), 1).Contains("%p / 낮은 재화 확률에서 전환"), "Individual currency tooltip was mistaken for an attack skill chance");
                void ClearDrops()
                {
                    foreach (Pickup pickup in pickups.pickups) pool.Return(pickup.transform.gameObject);
                    pickups.pickups.Clear();
                }
                void VerifyDrops(int expectedXpGrade, int expectedCoinGrade)
                {
                    Check(pickups.pickups.Count == 2, "One XP and one currency drop expected");
                    foreach (Pickup pickup in pickups.pickups)
                    {
                        bool currency = pickup.kind == PickupKind.Currency;
                        int grade = currency ? expectedCoinGrade : expectedXpGrade;
                        GameObject prefab = (currency ? art.runtimeAssets.CurrencyPickupPrefabs : art.runtimeAssets.ExperiencePickupPrefabs)[grade - 1];
                        Check(pickup.grade == grade, "Wrong runtime grade");
                        Check(pickup.transform.GetComponentInChildren<SpriteRenderer>().sprite == prefab.GetComponentInChildren<SpriteRenderer>().sprite, "Actual sprite does not match its payout grade");
                        float amount = currency ? tables.balance.lootGrades[grade - 1].currencyAmount : tables.balance.lootGrades[grade - 1].experienceAmount;
                        Check(Mathf.Abs(pickup.amount - amount) < .0001f, "Drop payout does not match grade");
                        Check(pickup.isLaunching, "Grade selection lost fountain motion");
                        if (currency)
                        {
                            int before = save.metaCurrency;
                            pickups.Collect(pickup);
                            Check(save.metaCurrency - before == amount && run.runCurrency == save.metaCurrency, "Currency collection mismatched/double awarded");
                        }
                        else
                        {
                            float before = xp; pickups.Collect(pickup);
                            Check(Mathf.Abs(xp - before - amount) < .0001f, "XP collection mismatched");
                        }
                    }
                    ClearDrops();
                }
                for (int stage = 0; stage <= 4; stage++)
                {
                    if (stage > 0) save.progress.kills[stage - 1] = tables.balance.monsters[stage - 1].nextUnlockKills;
                    for (int kind = 0; kind <= stage; kind++)
                    {
                        UnityEngine.Random.State before = UnityEngine.Random.state;
                        float unusedXpSample = UnityEngine.Random.value;
                        double coinSample = UnityEngine.Random.value;
                        UnityEngine.Random.state = before;
                        int coinGrade = LootGradeRules.ResolveCurrencyGrade(tables.balance, tables.balance.monsters[kind], stage, new double[5], coinSample);
                        pickups.SpawnPrototypeLoot(Vector3.zero, kind);
                        VerifyDrops(kind + 1, coinGrade); // Actual monster-specific categorical tables and prefabs.
                    }
                }
                foreach (PrototypeMonster monster in tables.balance.monsters)
                {
                    monster.currencyChances = new float[5]; monster.currencyChances[monster.currencyGrade - 1] = 1;
                }
                tables.GrowthRow("silverDropChance").step = 1; tables.GrowthRow("experienceGradeChance").step = 1;
                save.progress.SetRank("silverDropChance", 1);
                pickups.SpawnPrototypeLoot(Vector3.zero, 0); VerifyDrops(1, 1); // Purchase is not retroactive.
                progression.ResetSnapshot();
                for (int kind = 0; kind < 5; kind++)
                {
                    pickups.SpawnPrototypeLoot(Vector3.zero, kind); VerifyDrops(kind + 1, Mathf.Max(2, kind + 1));
                }
                save.progress.SetRank("silverDropChance", 0);
                tables.GrowthRow("amethystDropChance").step = 1;
                save.progress.SetRank("amethystDropChance", 1); progression.ResetSnapshot();
                for (int kind = 0; kind < 5; kind++)
                {
                    pickups.SpawnPrototypeLoot(Vector3.zero, kind); VerifyDrops(kind + 1, 5); // May jump directly to an unlocked named currency.
                }
                save.progress.SetRank("experienceGradeChance", 1); progression.ResetSnapshot();
                for (int kind = 0; kind < 5; kind++)
                {
                    pickups.SpawnPrototypeLoot(Vector3.zero, kind); VerifyDrops(Mathf.Min(5, kind + 2), 5);
                }
                Array.Clear(save.progress.kills, 0, save.progress.kills.Length); // Even forced 100% cannot promote into a locked grade.
                pickups.SpawnPrototypeLoot(Vector3.zero, 0); VerifyDrops(1, 1);
                Check(store.writes == 0 && store.saves == 0, "QA must never write PlayerPrefs or even the memory store in sandbox");
                // Missing grade slots must retain their index and fail boot, never shift a higher tier downward.
                ProjectDMAssetLoader loader = root.AddComponent<ProjectDMAssetLoader>();
                GameObject[] failedSlots = null;
                MethodInfo loadSlots = typeof(ProjectDMAssetLoader).GetMethod("LoadGradeCollection", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(typeof(GameObject));
                IEnumerator missing = (IEnumerator)loadSlots.Invoke(loader, new object[] { new AssetReference[5], new Action<GameObject[]>(slots => failedSlots = slots) });
                while (missing.MoveNext()) { }
                Check(failedSlots != null && failedSlots.Length == 5 && Array.TrueForAll(failedSlots, slot => slot == null), "Missing grade slots were compacted/reordered");
                Check(!string.IsNullOrEmpty(loader.Error), "Missing grade resources must fail initialization explicitly");
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/LootGradeQa-result.txt", $"PASS: {checks} assertions; actual five currency prefabs, categorical monster tables, one currency per kill, named currency growth, locked ceiling, unchanged XP promotion, legacy migration, next-run snapshot, pool/fountain/collection, zero sandbox save writes.\n");
            }
            finally { UnityEngine.Random.state = originalRandomState; UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
