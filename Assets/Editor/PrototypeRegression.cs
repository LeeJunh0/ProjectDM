using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectDM.Editor
{
    /// <summary>Runs the real combat methods in isolated fixtures; never opens/saves a scene or writes progression.</summary>
    public static class PrototypeRegression
    {
        private static int checks;

        [MenuItem("Project DM/검증/프로토타입 전투·성장 회귀 테스트")]
        public static void RunMenu()
        {
            RunChecks();
            Debug.Log($"Project DM prototype: {checks} Unity assertions PASS.");
        }

        public static void RunBatch()
        {
            try
            {
                RunChecks();
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/PrototypeRegression-result.txt", $"PASS: {checks} Unity assertions\n");
                Debug.Log($"Project DM prototype: {checks} Unity assertions PASS.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/PrototypeRegression-result.txt", "FAIL: " + exception);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }

        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;

        private static void RunChecks()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드를 종료하고 테스트하세요.");
            checks = 0;
            float previousTimeScale = Time.timeScale;
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(7413);
                using (Fixture f = new Fixture())
                {
                    GameTables tables = new GameTables(f.balance, f.skills, f.growth);
                    MemoryProgressStore store = new MemoryProgressStore();
                    store.integers["PROJECT_DM_META_GOLD"] = 321;
                    PrototypeSave oldSave = new PrototypeSave { version = 2, completedRuns = 9 };
                    oldSave.SetRank("proc", 7, .4);
                    string original = JsonUtility.ToJson(oldSave);
                    store.strings[SaveService.PrototypeSaveKey] = original;
                    SaveService persistence = new SaveService(tables, store, false);
                    persistence.Load();
                    Check(persistence.metaCurrency == 321 && persistence.progress.completedRuns == 9, "Injected persistence keeps currency and runs");
                    Check(persistence.progress.version == 3 && persistence.progress.Rank("chainChance") > 0, "Injected persistence executes existing migration");
                    Check(store.strings[SaveService.PrototypePreRadialBackupKey] == original && store.saves == 1, "Migration backup preserves the original snapshot before one flush");
                    persistence.Load();
                    Check(store.strings[SaveService.PrototypePreRadialBackupKey] == original, "Reload never replaces migration backup");
                    MemoryProgressStore sandboxStore = new MemoryProgressStore();
                    sandboxStore.strings[SaveService.PrototypeSaveKey] = original;
                    SaveService sandbox = new SaveService(tables, sandboxStore, true);
                    sandbox.Load(); sandbox.metaCurrency += 10; sandbox.MarkPrototypeProgressDirty(); sandbox.FlushPrototypeProgress();
                    Check(sandboxStore.writes == 0 && sandboxStore.saves == 0, "Injected sandbox persistence performs zero writes, including backups");
                    MemoryProgressStore corruptStore = new MemoryProgressStore();
                    corruptStore.strings[SaveService.PrototypeSaveKey] = "{\"version\":99}";
                    SaveService corrupt = new SaveService(tables, corruptStore, false);
                    bool rejected = false;
                    try { corrupt.Load(); } catch (InvalidOperationException) { rejected = true; }
                    corrupt.MarkPrototypeProgressDirty(); corrupt.FlushPrototypeProgress();
                    Check(rejected && corrupt.progress == null && corruptStore.writes == 0, "Corrupt persistence fails closed and cannot overwrite data later");
                }
                using (Fixture f = new Fixture())
                {
                    Check(PrototypeRules.Validate(f.balance, f.skills, f.growth) == null, "Unity JsonUtility table validation");
                    Check(f.balance.initialSeconds == 40, "Table duration overrides the old 5-second scene debug value");
                    Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Prefabs/Enemy.prefab") != null, "Existing default enemy resource");
                    Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Prefabs/Player.prefab") != null, "Existing player resource");
                    f.Call("SpawnPrototypeInitialEnemies");
                    Check(f.Enemies.Count == 12, "Initial field population");
                    foreach (object enemy in f.Enemies)
                        Check((int)Get(enemy, "kind") == 0 && Near((float)Get(enemy, "hitPoints"), 3), "Only base-HP slimes initially");
                    f.Call("SpawnPrototypeRefill", 100f);
                    Check(f.Enemies.Count == 14, "Refill stops at target population");
                    f.Call("SpawnPrototypeRefill", 101f);
                    Check(f.Enemies.Count == 14, "No uncapped time-driven spawning");
                }
                using (Fixture f = new Fixture())
                {
                    f.ActiveRanks["population"] = 1;
                    f.Call("SpawnPrototypeInitialEnemies");
                    Check(f.Enemies.Count == 17, "Initial population scales proportionally with target growth");
                    f.Call("SpawnPrototypeRefill", 100f);
                    Check(f.Enemies.Count == 19, "Population node increases target");
                }
                using (Fixture f = new Fixture())
                {
                    PrototypeGrowth damage = Array.Find(f.growth.rows, row => row.id == "damage");
                    int gold = 100;
                    Check(PrototypeRules.TryPurchase(f.balance, damage, f.save, 0, ref gold), "Purchase executes");
                    Check(Near(f.MainDamage, 1), "Purchases do not retroactively modify the current run");
                    f.Call("ResetPrototypeRun");
                    Check(Near(f.MainDamage, 1.66f), "Next-run snapshot includes compressed purchase and first bonus");
                    f.Ranks["chain"] = 1;
                    f.Call("ResetPrototypeRun");
                    Check(f.Ranks.Count == 0 && f.save.Rank("damage") == 1, "Run acquisition resets, permanent growth remains");
                }
                using (Fixture f = new Fixture())
                {
                    f.save.version = 1;
                    f.save.SetRank("damage", 4);
                    PrototypeRules.MigrateGrowthRanks(f.save, f.growth);
                    f.Call("ResetPrototypeRun");
                    Check(f.save.version == 3 && Near(f.MainDamage, 1.62f), "Fractional migrated growth survives the next-run snapshot");
                    PrototypeSave roundtrip = JsonUtility.FromJson<PrototypeSave>(JsonUtility.ToJson(f.save));
                    Check(Math.Abs(roundtrip.PartialRank("damage") - .8) < .00001, "Unity save round-trip preserves fractional migrated growth");
                    f.Call("BuildPrototypeGraph");
                    Array graph = (Array)Get(f.manager, "metaTreeNodes");
                    int speed = Array.FindIndex(f.growth.nodes, n => n.id == "chainChance_1") + 1;
                    Check(Get(graph.GetValue(speed), "state").ToString() == "Locked", "Graph reflects the actual cross-growth purchase gate");
                    PrototypeGrowthNode first = Array.Find(f.growth.nodes, n => n.id == "damage_1");
                    PrototypeGrowth row = Array.Find(f.growth.rows, r => r.id == "damage");
                    int gold = 100;
                    Check(PrototypeRules.TryPurchaseNode(f.balance, row, first, f.save, 0, ref gold, f.growth), "Migrated partial tier can buy a new full step");
                    Check(Near(f.MainDamage, 1.62f), "Graph purchase still waits for the next run");
                    f.Call("ResetPrototypeRun");
                    Check(Near(f.MainDamage, 1.82f), "New compressed step stacks with migrated residual growth");
                    f.save.SetRank("chain", 1);
                    f.Call("BuildPrototypeGraph");
                    graph = (Array)Get(f.manager, "metaTreeNodes");
                    Check(Get(graph.GetValue(speed), "state").ToString() == "Reachable", "Skill unlock opens its individual probability growth");
                }
                using (Fixture f = new Fixture())
                {
                    int preview = Array.FindIndex(f.growth.nodes, n => n.id == "chainChance_1") + 1;
                    int distant = Array.FindIndex(f.growth.nodes, n => n.id == "damage_4") + 1;
                    Set(f.manager, "selectedMetaTreeNode", distant);
                    f.Call("BuildPrototypeGraph");
                    Array graph = (Array)Get(f.manager, "metaTreeNodes");
                    Check(Get(graph.GetValue(preview), "state").ToString() == "Locked", "Immediate next node is visible even while its purchase is locked");
                    Check(Get(graph.GetValue(distant), "state").ToString() == "Hidden", "Distant nodes use concealed presentation state");
                    Check((string)Get(graph.GetValue(distant), "title") == "" && (string)Get(graph.GetValue(distant), "subtitle") == "", "Hidden presentation exposes neither title nor description");
                    Check((int)Get(f.manager, "selectedMetaTreeNode") == -1, "Hidden selections are cleared before drawing details");
                    f.save.SetRank("chain", 1);
                    f.Call("BuildPrototypeGraph");
                    graph = (Array)Get(f.manager, "metaTreeNodes");
                    foreach (PrototypeGrowthNode node in f.growth.nodes)
                        if (node.parent == "chainChance_1")
                            Check(Get(graph.GetValue(Array.FindIndex(f.growth.nodes, n => n.id == node.id) + 1), "state").ToString() != "Hidden", "Newly open chance node reveals only its direct next nodes");
                }
                using (Fixture f = new Fixture())
                {
                    PrototypeSkill chain = Array.Find(f.skills.rows, s => s.id == "chain");
                    PrototypeSkill splash = Array.Find(f.skills.rows, s => s.id == "splash");
                    PrototypeSkill meteor = Array.Find(f.skills.rows, s => s.id == "meteor");
                    f.ActiveRanks["chainChance"] = 3;
                    Check(Math.Abs((double)f.Call("AttackEffectChance", chain) - .14) < .00001, "Chain probability receives its own permanent growth");
                    Check(Math.Abs((double)f.Call("AttackEffectChance", splash) - .12) < .00001 &&
                        Math.Abs((double)f.Call("AttackEffectChance", meteor) - .04) < .00001, "Chain growth never changes splash or meteor probability");
                    f.ActiveRanks["meteorChance"] = 3;
                    Check(Math.Abs((double)f.Call("AttackEffectChance", meteor) - .06) < .00001 &&
                        Math.Abs((double)f.Call("AttackEffectChance", chain) - .14) < .00001, "Meteor probability grows independently");
                    f.save.version = 2; f.save.SetRank("proc", 7, .4);
                    PrototypeRules.MigrateGrowthRanks(f.save, f.growth);
                    PrototypeSave roundtrip = JsonUtility.FromJson<PrototypeSave>(JsonUtility.ToJson(f.save));
                    Check(roundtrip.version == 3 && roundtrip.Rank("proc") == 7, "Common proc archive survives Unity serialization");
                    foreach (string id in new[] { "chainChance", "splashChance", "meteorChance" })
                    {
                        PrototypeGrowth row = Array.Find(f.growth.rows, r => r.id == id);
                        Check(Math.Abs(PrototypeRules.Amount(f.balance, row, roundtrip.Rank(id), roundtrip.PartialRank(id)) - .0296) < .00001,
                            "Each probability retains the old common bonus after Unity serialization");
                    }
                }
                using (Fixture f = new Fixture())
                {
                    f.balance.timeRewardChance = 1;
                    object original = f.Enemy(new Vector3(0, 0), 1);
                    object a = f.Enemy(new Vector3(1, 0), 1);
                    object b = f.Enemy(new Vector3(1.5f, 0), 1);
                    foreach (string id in new[] { "chain", "splash", "meteor" }) f.Ranks[id] = 1;
                    Set(f.manager, "forceAttackEffects", true);
                    f.Call("ApplyPrototypeBasicHit", original, 1f);
                    Check((bool)Get(original, "dead"), "Lethal direct hit");
                    Check((bool)Get(a, "dead") && (bool)Get(b, "dead"), "Chain plus splash damage can both execute on a lethal hit");
                    Check(f.Meteors.Count == 1, "One hit independently rolls all effects, and effect damage never recursively rolls");
                    Check(((int[])Get(f.manager, "monsterKillCounts"))[0] == 3, "Death counted exactly once per target");
                    Check(Near((float)Get(f.manager, "activeRunDuration"), 43), "Direct and effect kills each roll time replenishment");
                    Check(Near((float)Get(f.manager, "elapsed"), 0), "Replenishment cannot advance elapsed boss clock");
                    f.Call("DamagePrototypeEnemy", original, 100f);
                    Check(f.save.kills[0] == 3 && Near((float)Get(f.manager, "activeRunDuration"), 43), "Already-dead enemy cannot duplicate drops/stacks/time");
                }
                using (Fixture f = new Fixture())
                {
                    object enemy = f.Enemy(new Vector3(1, 0), 100);
                    f.ActiveRanks["multishot"] = 2;
                    f.Ranks["meteor"] = 1;
                    Set(f.manager, "forceAttackEffects", true);
                    f.Call("FirePrototypeVolley", 0f);
                    Check(f.Projectiles.Count == 3, "Two multishot ranks add two projectiles");
                    f.Call("UpdatePrototypeProjectiles", .13f);
                    Check(f.Projectiles.Count == 0 && f.Meteors.Count == 3, "Every actual multishot hit independently rolls meteor");
                    Check(Near((float)Get(enemy, "hitPoints"), 97.6f), "Additional projectile direct damage is 70%, not 100%");
                    Set(f.manager, "elapsed", .64f);
                    f.Call("UpdatePrototypeEffects", .64f);
                    Check(Near((float)Get(enemy, "hitPoints"), 97.6f), "Meteor damage waits for configured delay");
                    Set(f.manager, "elapsed", .65f);
                    f.Call("UpdatePrototypeEffects", .01f);
                    Check(Near((float)Get(enemy, "hitPoints"), 87.1f), "All meteor effects use full main damage, including extra-projectile procs");
                    Check(f.Meteors.Count == 0, "Delayed meteors resolve once");
                }
                using (Fixture f = new Fixture())
                {
                    object enemy = f.Enemy(Vector3.zero, 1);
                    f.save.kills[0] = 119;
                    f.Call("DamagePrototypeEnemy", enemy, 1f);
                    Check(f.save.kills[0] == 120 && PrototypeRules.Stage(f.balance, f.save) == 1, "Kill stack immediately unlocks next mixed stage without consuming kills");
                }
                using (Fixture f = new Fixture())
                {
                    f.save.kills[0] = 119;
                    // This check exercises BeginRun, so provide its real player dependency;
                    // combat-only fixtures deliberately use a bare transform instead.
                    GameObject avatar = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Prefabs/Player.prefab"));
                    avatar.transform.SetParent(((Transform)Get(f.manager, "player")).parent);
                    Player avatarController = avatar.GetComponent<Player>();
                    RuntimeTestAccess.Call(avatar.GetComponent<PlayerAnimation>(), "Awake");
                    RuntimeTestAccess.Call(avatarController, "Awake");
                    Set(f.manager, "player", avatar.transform); Set(f.manager, "playerController", avatarController);
                    Set(f.manager, "playerSprite", avatar.GetComponentInChildren<SpriteRenderer>().sprite);
                    Set(f.manager, "playerAnimatorController", avatar.GetComponentInChildren<Animator>().runtimeAnimatorController);
                    f.Call("BeginRun");
                    RunController run = (RunController)Get(f.manager, "run");
                    RunResultView results = (RunResultView)Get(f.manager, "results");
                    Check(run.permanentKillsAtRunStart[0] == 119 && run.monsterKillCounts[0] == 0, "Start copies lifetime kills separately from empty run kills");
                    object enemy = f.Enemy(Vector3.zero, 1);
                    f.Call("DamagePrototypeEnemy", enemy, 1f);
                    f.Call("EndRun");
                    var row = results.GetPermanentKillProgress(0);
                    Check(row.before == 119 && row.total == 120 && row.runKills == 1 && row.target == 120, "Result snapshots actual previous/final lifetime and this-run kills");
                    Check(row.NewlyUnlocked && results.PermanentKillStatus(0, row, 120).Contains("스켈레톤 해금!"), "Threshold crossing identifies this-run next-monster unlock");
                    Check(results.AnimatedPermanentKillCount(row, 2, 0) == 119 && results.AnimatedPermanentKillCount(row, 2, 2) == 120, "Result lifetime animation starts from prior progress and finishes exact");
                    Check(results.PermanentKillStatus(0, row, 119) == "다음: 스켈레톤", "Animation retains next target before crossing threshold");
                    Check(results.GetPermanentKillProgress(1).available && results.PermanentKillStatus(2, results.GetPermanentKillProgress(2), 0) == "미해금", "Current/newly unlocked and still-locked species distinguished");
                    int completed = f.save.completedRuns;
                    f.save.kills[0] = 121;
                    f.Call("EndRun");
                    Check(run.resultPermanentKillCounts[0] == 120 && f.save.completedRuns == completed, "Repeated EndRun neither re-snapshots results nor double counts a run");
                    f.Call("BeginRun");
                    Check(run.permanentKillsAtRunStart[0] == 121 && run.monsterKillCounts[0] == 0 && f.save.kills[0] == 121, "Restart retains lifetime kills, refreshes baseline and clears only run kills");
                    Check(!results.GetPermanentKillProgress(4).HasTarget, "Final species has no invented goal");
                    f.balance.monsters[0].nextUnlockKills = 200;
                    Check(results.GetPermanentKillProgress(0).target == 200, "Unlock goal comes from the configured monster table");
                }
                using (Fixture f = new Fixture())
                {
                    object pickup = Activator.CreateInstance(f.EntityType("Pickup"));
                    Set(pickup, "kind", Enum.ToObject(f.EntityType("PickupKind"), 0));
                    Set(pickup, "amount", .6f);
                    for (int i = 0; i < 12; i++) f.Call("Collect", pickup);
                    Check((int)Get(f.manager, "level") == 2 && Near((float)Get(f.manager, "experience"), .2f), "Fractional XP survives per-drop collection");
                    Check((int)Get(f.manager, "pendingRunUpgrades") == 1, "XP opens one selection per earned level");
                    Array choices = (Array)Get(f.manager, "runUpgradeChoices");
                    Check(choices.Length == 2, "Fresh run offers only the two available base upgrades");
                    Set(pickup, "amount", 100f);
                    f.Call("Collect", pickup);
                    int pending = (int)Get(f.manager, "pendingRunUpgrades");
                    Check(pending > 1, "Large XP drop queues every earned upgrade, not just one");
                    string selected = (string)Get(choices.GetValue(0), "upgrade");
                    f.Call("ChooseRunUpgrade", selected);
                    Check((int)Get(f.manager, "pendingRunUpgrades") == pending - 1 && f.Ranks[selected] == 1, "One choice consumes one pending level");
                    Check((bool)Get(f.manager, "choosingUpgrade"), "Remaining queued choices stay paused");
                }
                using (Fixture f = new Fixture())
                {
                    f.ActiveRanks["chain"] = 1;
                    f.Call("BuildPrototypeRunChoices");
                    Array choices = (Array)Get(f.manager, "runUpgradeChoices");
                    Check(choices.Length == 3, "Meta unlock enables third run card");
                    bool containsChain = false;
                    foreach (object card in choices) containsChain |= (string)Get(card, "upgrade") == "chain";
                    Check(containsChain && !f.Ranks.ContainsKey("chain"), "Meta unlock offers skill but does not auto-equip it");
                    int oldGold = PlayerPrefs.GetInt("PROJECT_DM_META_GOLD", 0);
                    f.save.SetRank("meteor", 1);
                    Set(f.manager, "metaCurrency", 123456);
                    f.Call("MarkPrototypeProgressDirty");
                    f.Call("FlushPrototypeProgress");
                    Check(PlayerPrefs.GetInt("PROJECT_DM_META_GOLD", 0) == oldGold, "Sandbox never writes real currency");
                    PrototypeSave roundtrip = JsonUtility.FromJson<PrototypeSave>(JsonUtility.ToJson(f.save));
                    Check(roundtrip.Rank("meteor") == 1 && roundtrip.kills.Length == 5, "Unity save round-trip preserves ranks and counters");
                }
                using (Fixture f = new Fixture())
                {
                    f.balance.criticalChance = 1;
                    object enemy = f.Enemy(Vector3.zero, 100);
                    f.Ranks["meteor"] = 1;
                    Set(f.manager, "forceAttackEffects", true);
                    f.Call("ApplyPrototypeBasicHit", enemy, .7f);
                    Check(Near((float)Get(enemy, "hitPoints"), 98.6f), "Critical applies to the actual extra-projectile direct damage");
                    IList popups = (IList)Get(Get(f.manager, "combatDamageDisplay"), "active");
                    Check((string)Get(popups[0], "text") == "1.4!", "Actual critical damage uses critical presentation");
                    Check(f.Meteors.Count == 1 && Near((float)Get(f.Meteors[0], "damage"), 3.5f), "Critical does not multiply or recursively trigger effect damage");
                    Set(f.manager, "enableCriticalHits", false);
                    f.Call("ApplyPrototypeBasicHit", enemy, .7f);
                    Check(Near((float)Get(enemy, "hitPoints"), 97.9f), "Disabling critical preserves normal damage");
                    Set(f.manager, "forceCriticalHits", true);
                    f.Call("ResetPrototypeRun");
                    Check(!(bool)Get(f.manager, "forceCriticalHits"), "Restart clears forced critical debug state");
                }
                using (Fixture f = new Fixture())
                {
                    object small = f.Enemy(Vector3.zero, 100);
                    object big = f.Enemy(Vector3.zero, 100);
                    Set(big, "kind", Enum.ToObject(f.EntityType("MonsterKind"), 4));
                    f.Call("UpdatePlayerContactDamage", .1f);
                    Check(Near((float)Get(f.manager, "playerHealth"), 94), "Simultaneous contact uses strongest damage, not all monsters stacked");
                    Check(Near((float)Get(f.manager, "playerInvulnerabilityRemaining"), .6f), "Player receives brief immunity");
                    f.Call("ApplyPlayerDamage", 10f);
                    f.Call("UpdatePlayerContactDamage", 0f);
                    Check(Near((float)Get(f.manager, "playerHealth"), 94), "Invulnerability rejects repeated damage and pause does not tick");
                    f.Call("UpdatePlayerContactDamage", .6f);
                    Check(Near((float)Get(f.manager, "playerHealth"), 88), "Contact damage repeats after immunity expires");
                    ((Transform)Get(small, "transform")).position = Vector3.right * 10;
                    ((Transform)Get(big, "transform")).position = Vector3.right * 10;
                    f.Call("UpdatePlayerContactDamage", .6f);
                    f.Call("ApplyPlayerDamage", 100f);
                    Check((bool)Get(f.manager, "runEnded") && (bool)Get(f.manager, "playerDefeated") && Near((float)Get(f.manager, "playerHealth"), 0), "Lethal damage ends run once and clamps HP");
                    Check(f.save.completedRuns == 1 && Near((float)Get(f.manager, "remainingTimeFraction"), 1), "Player death records one run without falsely showing timer depleted");
                    f.Call("ApplyPlayerDamage", 100f);
                    Check(f.save.completedRuns == 1, "Damage after defeat cannot duplicate end-run statistics");
                    f.Call("ResetPrototypeRun");
                    Check(Near((float)Get(f.manager, "playerHealth"), 100) && !(bool)Get(f.manager, "playerDefeated"), "Restart restores player health and defeat state");
                }
                using (Fixture f = new Fixture())
                {
                    object display = Get(f.manager, "combatDamageDisplay");
                    Type kind = f.EntityType("CombatDamageKind");
                    UnityEngine.Random.State before = UnityEngine.Random.state;
                    for (int i = 0; i < 3; i++) f.Call("ShowCombatDamage", Vector3.zero, 1.5f, Enum.ToObject(kind, i));
                    IList popups = (IList)Get(display, "active");
                    Check(popups.Count == 3, "Normal, received and critical presentation styles");
                    Check((string)Get(popups[0], "text") == "1.5" && (string)Get(popups[1], "text") == "−1.5" &&
                        (string)Get(popups[2], "text") == "1.5!", "Fractional damage text and style markers");
                    Check(before.Equals(UnityEngine.Random.state), "Visual jitter must not change combat RNG");
                    MethodInfo tick = display.GetType().GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
                    tick.Invoke(display, new object[] { 0f, .7f });
                    Check(popups.Count == 3, "Paused damage numbers remain frozen");
                    tick.Invoke(display, new object[] { .71f, .7f });
                    Check(popups.Count == 0, "Expired damage numbers return to pool");
                    Set(f.manager, "combatDamageMaximum", 2);
                    for (int i = 0; i < 4; i++) f.Call("ShowCombatDamage", Vector3.zero, 1f, Enum.ToObject(kind, 0));
                    Check(popups.Count == 2, "Only visual density is capped");
                    f.Call("ResetPrototypeRun");
                    Check(popups.Count == 0, "Restart clears damage presentation");
                }
            }
            finally { Time.timeScale = previousTimeScale; UnityEngine.Random.state = randomState; }
        }

        private static object Get(object target, string field) => RuntimeTestAccess.Get(target, field);
        private static void Set(object target, string field, object value) => RuntimeTestAccess.Set(target, field, value);

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root, prefab;
            private readonly GameObjectPool pool;
            internal readonly GameManager manager;
            internal readonly PrototypeBalance balance;
            internal readonly PrototypeSkillTable skills;
            internal readonly PrototypeGrowthTable growth;
            internal readonly PrototypeSave save = new PrototypeSave();
            internal Dictionary<string, int> Ranks => (Dictionary<string, int>)Get(manager, "runSkillRanks");
            internal Dictionary<string, int> ActiveRanks => (Dictionary<string, int>)Get(manager, "activeGrowthRanks");
            internal IList Enemies => (IList)Get(manager, "enemies");
            internal IList Projectiles => (IList)Get(manager, "projectiles");
            internal IList Meteors => (IList)Get(manager, "meteors");
            internal float MainDamage => (float)Get(manager, "MainDamage");

            internal Fixture()
            {
                root = new GameObject("Prototype regression fixture");
                root.SetActive(false); // No GameManager Awake, Addressables bootstrap or real save loading.
                manager = root.AddComponent<GameManager>();
                pool = root.AddComponent<GameObjectPool>();
                prefab = new GameObject("Default mock sprite prefab");
                prefab.transform.SetParent(root.transform);
                prefab.AddComponent<SpriteRenderer>();
                GameObject player = new GameObject("Test player");
                player.transform.SetParent(root.transform);
                balance = JsonUtility.FromJson<PrototypeBalance>(Resources.Load<TextAsset>("ProjectDM/Data/PrototypeBalance").text);
                skills = JsonUtility.FromJson<PrototypeSkillTable>(Resources.Load<TextAsset>("ProjectDM/Data/PrototypeSkills").text);
                growth = JsonUtility.FromJson<PrototypeGrowthTable>(Resources.Load<TextAsset>("ProjectDM/Data/PrototypeGrowth").text);
                // Default tests suppress randomized time/chest rewards; individual tests explicitly enable them.
                balance.timeRewardChance = 0;
                balance.criticalChance = 0;
                balance.chestChance = 0;
                GameTables tables = new GameTables(balance, skills, growth);
                SaveService persistence = new SaveService(tables, new MemoryProgressStore(), true) { progress = save };
                manager.ComposeServices(tables, persistence, pool, root.AddComponent<PrototypeSkillVisuals>());
                Set(manager, "player", player.transform);
                ProjectDMRuntimeAssets assets = new ProjectDMRuntimeAssets();
                foreach (string property in new[] { "EnemyPrefab", "ProjectilePrefab", "ChestPickupPrefab" })
                    typeof(ProjectDMRuntimeAssets).GetProperty(property).SetValue(assets, prefab);
                foreach (string property in new[] { "ExperiencePickupPrefabs", "CurrencyPickupPrefabs" })
                    typeof(ProjectDMRuntimeAssets).GetProperty(property).SetValue(assets, new[] { prefab, prefab, prefab, prefab, prefab });
                Set(manager, "runtimeAssets", assets);
                Call("ResetPrototypeRun");
                Set(manager, "initialRunSeconds", 40f); Set(manager, "activeRunDuration", 40f);
            }

            internal Type EntityType(string name) => typeof(GameManager).Assembly.GetType("ProjectDM." + name);
            internal object Enemy(Vector3 position, float hp)
            {
                Call("SpawnPrototypeEnemy", true);
                object enemy = Enemies[Enemies.Count - 1];
                ((Transform)Get(enemy, "transform")).position = position;
                Set(enemy, "hitPoints", hp);
                return enemy;
            }
            internal object Call(string method, params object[] args) => RuntimeTestAccess.Call(manager, method, args);

            public void Dispose()
            {
                // Pooled instances currently active are unparented; destroy every tracked test instance as well.
                IDictionary instances = (IDictionary)Get(pool, "sourceByInstance");
                foreach (GameObject instance in instances.Keys) if (instance != null) Object.DestroyImmediate(instance);
                Object.DestroyImmediate(root);
            }
        }
    }
}
