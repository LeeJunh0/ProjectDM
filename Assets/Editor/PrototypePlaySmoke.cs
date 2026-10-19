using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectDM.Editor
{
    /// <summary>Batch-only smoke check of the real Main scene/Addressables boot. Does not save scenes or player data.</summary>
    [InitializeOnLoad]
    public static class PrototypePlaySmoke
    {
        private const string SessionKey = "ProjectDM.PrototypePlaySmoke";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static PrototypePlaySmoke()
        {
            if (SessionState.GetBool(SessionKey, false)) EditorApplication.update += Poll;
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("This entry point is for isolated batch verification only.");
            SessionState.SetBool(SessionKey, true);
            SessionState.SetBool(SessionKey + ".done", false);
            SessionState.SetBool(SessionKey + ".failed", false);
            SessionState.SetFloat(SessionKey + ".started", (float)EditorApplication.timeSinceStartup);
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            GameManager manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (manager == null) throw new InvalidOperationException("Main scene has no GameManager.");
            SerializedObject settings = new SerializedObject(manager);
            settings.FindProperty("prototypeSandbox").boolValue = true;
            settings.FindProperty("enablePrototypeDebug").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo(); // In-memory only; the scene file is never saved.
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            EditorApplication.EnterPlaymode();
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(SessionKey, false)) { EditorApplication.update -= Poll; return; }
            if (SessionState.GetBool(SessionKey + ".done", false))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                bool failed = SessionState.GetBool(SessionKey + ".failed", false);
                SessionState.SetBool(SessionKey, false);
                EditorApplication.update -= Poll;
                EditorApplication.Exit(failed ? 1 : 0);
                return;
            }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(SessionKey + ".started", 0) > 90)
                    throw new InvalidOperationException("Main scene initialization timed out.");
                if (!EditorApplication.isPlaying) return;
                GameManager manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
                if (manager == null) return;
                string error = Get(manager, "prototypeError") as string;
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                ProjectDMAssetLoader loader = Get(manager, "assetLoader") as ProjectDMAssetLoader;
                if (loader != null && !string.IsNullOrEmpty(loader.Error)) throw new InvalidOperationException(loader.Error);
                if (!(bool)Get(manager, "isInitialized")) return;
                Verify(manager);
                Finish("PASS: Main Play Mode, real Addressables/player/prefabs, both stages, four personal-magic characters/casts/cards, higher-cost frost orbit, no retired shared procs, MP capacity/next-run snapshot, mana game-over/restart, independent boss clock, sandbox.", false);
            }
            catch (Exception exception) { Finish("FAIL: " + exception, true); }
        }

        private static void Verify(GameManager manager)
        {
            Check((bool)Get(manager, "sandboxThisSession"), "Smoke check must never save player data.");
            Check(Get(manager, "player") != null, "Real player boot");
            Check(Get(manager, "runtimeAssets") != null, "Real Addressables boot");
            RunController characterRun = (RunController)Get(manager, "run");
            characterRun.characters.Select("projectile");
            ProjectDMRuntimeAssets assets = (ProjectDMRuntimeAssets)Get(manager, "runtimeAssets");
            Check(assets.CurrencyPickupPrefabs.Length == 5 && assets.ExperiencePickupPrefabs.Length == 5, "Addressables preserves all five grade slots");
            for (int i = 0; i < 5; i++)
            {
                GameObject currencyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GameContent/Prefabs/CurrencyPickup_{i + 1:00}.prefab");
                GameObject xpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GameContent/Prefabs/ExperiencePickup_{i + 1:00}.prefab");
                Check(assets.CurrencyPickupPrefabs[i].GetComponentInChildren<SpriteRenderer>().sprite == currencyPrefab.GetComponentInChildren<SpriteRenderer>().sprite, "Loaded currency grade order");
                Check(assets.ExperiencePickupPrefabs[i].GetComponentInChildren<SpriteRenderer>().sprite == xpPrefab.GetComponentInChildren<SpriteRenderer>().sprite, "Loaded XP grade order");
            }
            Check(Get(manager, "boltSprite") != null && Get(manager, "gemSprite") != null, "Existing default effect sprites loaded");
            SkillVisualAssets skillArt = ((WorldArtAssets)Get(manager, "art")).skillVisualAssets;
            Check(skillArt != null && skillArt.chain != null && skillArt.splash != null && skillArt.meteor != null && skillArt.impactMaterial != null,
                "Real Main boot loads the approved generated skill artwork and impact material");
            Call(manager, "BeginRun");
            PrototypeBalance balance = (PrototypeBalance)Get(manager, "balance");
            RunMana mana = (RunMana)Get(manager, "mana");
            Check(mana.Maximum >= balance.ResolvedInitialMana, "Table 40 MP, not scene debug value");
            Check(mana.Fraction == 1, "Mana begins full");
            Check(((IList)Get(manager, "enemies")).Count >= 12, "Real initial monsters");
            StageSession stages = (StageSession)Get(manager, "stages");
            Check(stages.Current.stageId == 1, "Default real Main run uses meadow stage");
            PrototypeSave beforeStageChange = (PrototypeSave)Get(manager, "progress");
            WorldArtAssets stageArt = (WorldArtAssets)Get(manager, "art");
            foreach (int id in new[] { 2, 1, 2, 1 })
            {
                string beforeStageSave = JsonUtility.ToJson((PrototypeSave)Get(manager, "progress"));
                stages.Select(id);
                Call(manager, "BeginRun");
                Check(stages.Current.stageId == id && stageArt.stage == stages.Current, "Real restart swaps ground and roster together");
                Check(JsonUtility.ToJson((PrototypeSave)Get(manager, "progress")) == beforeStageSave, "Testing stage selection does not change progression or rewards");
                IList population = (IList)Get(manager, "enemies");
                foreach (object enemy in population)
                {
                    int slot = (int)(MonsterKind)RuntimeTestAccess.Get(enemy, "kind");
                    Check((UnityEngine.Sprite)RuntimeTestAccess.Get(enemy, "baseSprite") == stages.Current.monsters[slot].frames[0], "Pooled monster uses current stage sprite");
                    Check((UnityEngine.Sprite)RuntimeTestAccess.Get(enemy, "alternateSprite") == stageArt.MonsterAlternate((MonsterKind)slot), "Pooled monster uses current animation owner");
                }
                VerifyMonsterPresentation(manager, stageArt);
                Call(manager, "ArriveAtPrototypeBoss", true);
                Transform preview = (Transform)Get(manager, "prototypeBoss");
                Check(preview.GetComponent<SpriteRenderer>().sprite == stages.Current.bossFrames[0], "Boss preview uses stage-specific artwork, including reused preview objects");
                Check(Mathf.Approximately(preview.localScale.x, stages.Current.bossScale), "Boss preview uses its independent authored scale after pooling/stage swaps");
                Call(manager, "BeginRun");
            }
            Call(manager, "TriggerDebugLevelUp");
            Check((bool)Get(manager, "choosingUpgrade") && Time.timeScale == 0, "Card selection pauses run");
            Array cards = (Array)Get(manager, "runUpgradeChoices");
            Check(cards.Length >= 2, "Available cards are built from tables");
            string selected = (string)cards.GetValue(0).GetType().GetField("upgrade").GetValue(cards.GetValue(0));
            Call(manager, "ChooseRunUpgrade", selected);
            Check(!(bool)Get(manager, "choosingUpgrade") && Time.timeScale == 1, "Selection applies and resumes");
            Dictionary<string, int> ranks = (Dictionary<string, int>)Get(manager, "runSkillRanks");
            foreach (string id in new[] { "chain", "splash", "meteor" }) ranks[id] = 1;
            Set(manager, "forceAttackEffects", true);
            IList enemies = (IList)Get(manager, "enemies");
            object victim = enemies[0];
            victim.GetType().GetField("hitPoints").SetValue(victim, 1f);
            Call(manager, "ApplyPrototypeBasicHit", victim, 1f);
            Check(((IList)Get(manager, "meteors")).Count == 0, "Personal magic does not trigger retired shared effects, even with archived ranks");
            PrototypeSave progress = (PrototypeSave)Get(manager, "progress");
            bool wasReached = progress.reachedBoss;
            Call(manager, "ArriveAtPrototypeBoss", true);
            Check((bool)Get(manager, "runEnded") && (bool)Get(manager, "bossArrived") && Time.timeScale == 0, "Boss arrival freezes at prototype milestone");
            Check(Get(manager, "prototypeBoss") != null, "Default sprite boss preview exists");
            Check(progress.reachedBoss == wasReached, "Debug preview does not claim real boss arrival");
            Call(manager, "BeginRun");
            Check(ranks.Count == 0 && !(bool)Get(manager, "runEnded") && Time.timeScale == 1, "Restart clears temporary skills/effects and resumes");
            mana.Reset(1000, 1);
            Set(manager, "elapsed", balance.ResolvedInitialMana);
            Call(manager, "Update");
            Check(!(bool)Get(manager, "bossArrived"), "A deadline filled to the cap is not actual elapsed boss arrival");
            Set(manager, "elapsed", balance.bossElapsedSeconds);
            Call(manager, "Update");
            Check((bool)Get(manager, "bossArrived") && progress.reachedBoss && (bool)Get(manager, "runEnded"), "Actual Update clock triggers boss at elapsed cap before timeout results");
            Call(manager, "BuildPrototypeGraph");
            PrototypeGrowthTable growth = ((GameTables)Get(manager, "tables")).characters.CommonGraph.growthTable;
            Check(((Array)Get(manager, "metaTreeNodes")).Length == growth.nodes.Length + 1 + growth.goalLine.Length, "Real graph contains root, all purchase nodes and objective previews");
            int savedManaRank = progress.Rank("time");
            double savedManaPartial = progress.PartialRank("time");
            progress.SetRank("time", 0);
            Call(manager, "BeginRun");
            Check(mana.Current == mana.Maximum && Mathf.Approximately(mana.Maximum, balance.ResolvedInitialMana), "Real restart fills base MP");
            progress.SetRank("time", 1);
            Check(Mathf.Approximately(mana.Maximum, balance.ResolvedInitialMana), "Buying mana capacity does not modify the active run");
            Call(manager, "BeginRun");
            Check(Mathf.Approximately(mana.Current, balance.ResolvedInitialMana + 2), "Old time-growth saved rank now increases next-run mana capacity");
            float beforeIdle = mana.Current;
            Call(manager, "TickClock", .1f);
            Check(mana.Current == beforeIdle, "Real run clock never drains mana while idle");
            mana.Reset(balance.basicAttackManaCost, balance.basicAttackManaCost);
            Call(manager, "FirePrototypeVolley", (float)Get(manager, "elapsed"));
            Check(mana.IsDepleted, "Real affordable last basic volley spends the last MP");
            int completedBefore = progress.completedRuns;
            Call(manager, "Update");
            Check((bool)Get(manager, "runEnded") && mana.IsDepleted && Time.timeScale == 0, "Actual GameManager Update ends immediately at zero MP");
            Call(manager, "Update");
            Check(progress.completedRuns == completedBefore + 1, "Mana game over records a single run");
            progress.SetRank("time", savedManaRank, savedManaPartial);
            Call(manager, "BeginRun");
            Check(mana.Current == mana.Maximum && mana.AbsorbedThisRun == 0 && !(bool)Get(manager, "runEnded"), "Real restart clears depletion and absorption statistics");
            ManaBarRenderer liquid = (ManaBarRenderer)Get(manager, "manaBar");
            manager.SetManaVisualState(ManaVisualState.Awakened);
            liquid.Tick(1);
            Check(liquid.State == ManaVisualState.Awakened, "Public future awakening API switches only MP presentation");
            manager.SetManaVisualState(ManaVisualState.Normal);
            foreach (string character in new[] { "lightning", "flame", "frost", "projectile" })
            {
                characterRun.characters.Select(character); Call(manager, "BeginRun");
                Check(characterRun.characters.Current.id == character, "Real restart applies selected personal magic");
                EnemySystem enemySystem = (EnemySystem)Get(manager, "enemiesSystem");
                Enemy target = enemySystem.enemies[0]; target.transform.position = ((Transform)Get(manager, "player")).position + Vector3.right;
                target.hitPoints = 1000;
                Call(manager, "FireAtNearestEnemy", 0f);
                Check(mana.SpentThisRun == (character == "frost" ? 3 : 1), "Real personal magic pays its table-defined cast cost");
                if (character == "frost")
                {
                    SkillSystem skills = (SkillSystem)Get(manager, "skills");
                    skills.UpdatePrototypeEffects(.05f);
                    IList shards = (IList)RuntimeTestAccess.Get(manager.GetComponent<PrototypeSkillVisuals>(), "frostShards");
                    Check(skills.Frost.Active && shards.Count == 2 && ((SpriteRenderer)shards[0]).sprite != null, "Real frost orbit loads and positions existing sprite resources");
                }
                Call(manager, "TriggerDebugLevelUp");
                foreach (RunUpgradePresentation card in (RunUpgradePresentation[])Get(manager, "runUpgradeChoices"))
                    Check(card.upgrade.StartsWith("magic." + character + "."), "Real level-up pool contains only active personal magic");
            }
            Call(manager, "BeginRun");
        }

        private static void VerifyMonsterPresentation(GameManager manager, WorldArtAssets art)
        {
            EnemySystem system = (EnemySystem)Get(manager, "enemiesSystem");
            PrototypeBalance balance = (PrototypeBalance)Get(manager, "balance");
            var stage = balance.stages[4];
            var weights = stage.weights;
            int overrideBefore = system.debugStageOverride;
            try
            {
                system.Reset(); system.debugStageOverride = 4;
                // Force every monster slot without modifying progression or relying on random sampling.
                for (int slot = 0; slot < 5; slot++)
                {
                    stage.weights = new int[5]; stage.weights[slot] = 1;
                    system.SpawnPrototypeEnemy(true);
                    Enemy enemy = system.enemies[slot];
                    Check((int)enemy.kind == slot, "Forced visual slot spawned");
                    Check(enemy.renderer.bounds.size.y >= 1f, "Live monster has readable world size");
                    Animator animator = enemy.transform.GetComponentInChildren<Animator>();
                    RuntimeAnimatorController controller = art.MonsterController(enemy.kind);
                    Check(animator.runtimeAnimatorController == controller && animator.enabled == (controller != null), "Pooled Animator switches correctly between biomes");
                    if (controller != null)
                    {
                        Check(enemy.alternateSprite == null, "Manual playback cannot overwrite Animator output");
                        animator.Update(.18f);
                        Check(enemy.renderer.sprite == art.stage.monsters[slot].frames[1], "Original Animator advances to the second actual frame");
                        animator.Update(.17f);
                        Check(enemy.renderer.sprite == enemy.baseSprite, "Original looping Animator returns to the first frame");
                    }
                }
                system.UpdateEnemies(.17f);
                foreach (Enemy enemy in system.enemies)
                    if (enemy.alternateSprite != null) Check(enemy.renderer.sprite == enemy.alternateSprite, "Live two-frame sprite animation advances");
                system.UpdateEnemies(.17f);
                foreach (Enemy enemy in system.enemies)
                    if (enemy.alternateSprite != null) Check(enemy.renderer.sprite == enemy.baseSprite, "Live two-frame sprite animation loops");
            }
            finally { stage.weights = weights; system.Reset(); system.debugStageOverride = overrideBefore; }
        }

        private static void Finish(string message, bool failed)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/PrototypePlaySmoke-result.txt", message + "\n");
            SessionState.SetBool(SessionKey + ".failed", failed);
            SessionState.SetBool(SessionKey + ".done", true);
            if (failed) Debug.LogError(message); else Debug.Log(message);
            EditorApplication.ExitPlaymode();
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static object Get(GameManager target, string field) => RuntimeTestAccess.Get(target, field);
        private static void Set(GameManager target, string field, object value) => RuntimeTestAccess.Set(target, field, value);
        private static object Call(GameManager target, string method, params object[] args) => RuntimeTestAccess.Call(target, method, args);
    }
}
