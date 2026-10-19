using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ProjectDM.Editor
{
    public static class CharacterRegression
    {
        private static int checks;
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;
        [MenuItem("Project DM/검증/캐릭터 고유 마법 검증")]
        public static void RunMenu()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드를 종료한 후 검증하세요.");
            Verify(); Debug.Log($"Character regression: {checks} assertions PASS.");
        }
        public static void RunBatch()
        {
            try { Verify(); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/CharacterRegression-result.txt", $"PASS: {checks} assertions; personal cards/attacks, growth isolation, snapshots, MP, save compatibility.\n"); EditorApplication.Exit(0); }
            catch (Exception e) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/CharacterRegression-result.txt", "FAIL: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
        }
        private static void Verify()
        {
            checks = 0; float previousScale = Time.timeScale; var random = Random.state;
            try
            {
                using Fixture f = new();
                Check(f.run.characters.Current.id == "projectile", "Default character is projectile");
                Check(f.run.characters.Select("frost") && !f.run.characters.Select("missing"), "Frost is available, invalid characters cannot be selected");
                f.run.characters.Select("projectile");
                CheckAllPurchases(f.tables.characters.CommonGraph);
                foreach (GameTables graph in f.tables.characters.Graphs.Values) CheckAllPurchases(graph);
                f.save.progress.SetRank("meteor", 1); f.save.progress.SetRank("chainChance", 2);
                string old = JsonUtility.ToJson(f.save.progress);
                foreach (string id in new[] { "projectile", "lightning", "flame", "frost" })
                {
                    string active = f.run.characters.Current.id;
                    Check(f.run.characters.Select(id) && f.run.characters.Current.id == active, "Selection never hot swaps a running character");
                    f.Begin();
                    float cost = id == "frost" ? 3 : 1;
                    Check(f.run.characters.Current.id == id && Near(f.mana.BasicAttackCost, cost), "New run applies selected character and MP cost");
                    for (int i = 0; i < 20; i++)
                    {
                        f.selection.BuildPrototypeRunChoices(); Check(f.selection.runUpgradeChoices.Length == 3, "Three available personal choices");
                        System.Collections.Generic.HashSet<string> offered = new();
                        foreach (RunUpgradePresentation card in f.selection.runUpgradeChoices)
                            Check(card.upgrade.StartsWith("magic." + id + ".") && offered.Add(card.upgrade), "Cards contain only distinct current personal magic upgrades");
                    }
                    f.selection.pendingRunUpgrades = 1; f.selection.OpenRunUpgradeSelection();
                    f.selection.ChooseRunUpgrade("meteor"); Check(f.selection.choosingUpgrade && f.skills.RunRank("meteor") == 0, "Forged unrelated selection is ignored");
                    string selected = f.selection.runUpgradeChoices[0].upgrade;
                    f.selection.ChooseRunUpgrade(selected); Check(f.skills.RunRank(selected) == 1 && !f.selection.choosingUpgrade, "Offered personal selection applies");
                    f.Begin(); Check(f.skills.runSkillRanks.Count == 0, "Run upgrades clear on restart");
                    GameTables graph = f.tables.characters.Graphs[id];
                    foreach (PrototypeGrowthNode node in graph.growthTable.nodes)
                    {
                        PrototypeGrowth row = graph.GrowthRow(node.growthId);
                        Check(node.ranks >= 1 && node.ranks <= 3 && row.maxRank == node.ranks, "Personal nodes capped at three purchases without redundant ranks");
                        if (node.parent == "ROOT") Check(PrototypeRules.NodeUnlockBlock(graph.growthTable, node, f.save.progress) == null, "Root personal branches purchasable");
                        else Check(PrototypeRules.NodeUnlockBlock(graph.growthTable, node, f.save.progress) != null, "Child requires its own local predecessor");
                    }
                    f.Enemy(new Vector3(1, 0), 100); f.Enemy(new Vector3(2, 0), 100); f.Enemy(new Vector3(3, 0), 100);
                    f.combat.FireAtNearestEnemy(0); Check(Near(f.mana.Current, f.mana.Maximum - cost), "Every character pays exactly once per cast");
                    float paid = f.mana.Current; f.combat.FireAtNearestEnemy(0); Check(Near(f.mana.Current, paid), "Cooldown costs no MP");
                    if (id == "projectile") Check(f.combat.projectiles.Count == 1, "Projectile character creates a projectile");
                    else if (id == "frost") Check(f.skills.Frost.Active && f.skills.Frost.Count == 2, "Frost creates two orbiting shards");
                    else Check(f.combat.projectiles.Count == 0 && f.enemies.enemies[0].hitPoints < 100, "Lightning/flame directly damage without projectiles");
                    if (id == "lightning") foreach (Enemy enemy in f.enemies.enemies) Check(enemy.hitPoints < 100, "Base lightning hits first target and two chained targets");
                    f.enemies.Reset(); f.combat.nextShot = 0; paid = f.mana.Current; f.combat.FireAtNearestEnemy(0);
                    Check(Near(f.mana.Current, paid), "No target costs no MP");
                }
                f.run.characters.Select("projectile"); f.Begin();
                GameTables projectileGraph = f.tables.characters.Graphs["projectile"];
                PrototypeGrowthNode damageNode = projectileGraph.growthTable.nodes[0];
                f.save.metaCurrency = 1000;
                Check(PrototypeRules.TryPurchaseNode(f.tables.balance, projectileGraph.GrowthRow(damageNode.growthId), damageNode, f.save.progress, 0, ref f.save.metaCurrency, projectileGraph.growthTable), "Personal permanent growth is purchasable with real currency");
                Check(Near(f.skills.PersonalAmount("damage"), 0), "Purchased growth does not leak into current run");
                f.Begin(); Check(Near(f.skills.PersonalAmount("damage"), .15f), "Next run applies personal permanent growth");
                f.run.characters.Select("lightning"); f.Begin(); Check(Near(f.skills.PersonalAmount("damage"), 0), "Other character cannot inherit projectile-only ranks");
                f.run.characters.Select("projectile"); f.Begin();
                f.skills.runSkillRanks["magic.projectile.count"] = 2;
                f.skills.runSkillRanks["magic.projectile.pierce"] = 1;
                Enemy a = f.Enemy(new Vector3(1, 0), 100), b = f.Enemy(new Vector3(2, 0), 100);
                f.combat.FireAtNearestEnemy(0); Check(f.combat.projectiles.Count == 3 && Near(f.mana.SpentThisRun, 1), "Multishot still costs one cast");
                f.combat.UpdateProjectiles(.3f); Check(a.hitPoints < 100 && b.hitPoints < 100, "Piercing swept segment hits multiple targets in one frame");
                Check(f.skills.meteors.Count == 0, "Archived shared spells never proc in character mode");
                f.Begin(); f.skills.runSkillRanks["magic.projectile.homing"] = 1;
                Enemy moving = f.Enemy(new Vector3(2, 0), 100); f.combat.FireAtNearestEnemy(0); moving.transform.position = new Vector3(2, 2);
                f.combat.UpdateProjectiles(.05f); Check(f.combat.projectiles.Count == 1 && f.combat.projectiles[0].direction.y > 0, "Homing upgrade turns toward a moving target");
                f.run.characters.Select("lightning"); f.Begin();
                a = f.Enemy(new Vector3(1, 0), 100); b = f.Enemy(new Vector3(5, 0), 100);
                f.combat.FireAtNearestEnemy(0); Check(Near(b.hitPoints, 100), "Lightning base hop cannot jump beyond three units");
                f.skills.runSkillRanks["magic.lightning.range"] = 2; f.combat.nextShot = 0; f.combat.FireAtNearestEnemy(1);
                Check(b.hitPoints < 100, "Hop-range upgrade reaches the farther target");
                f.Begin(); a = f.Enemy(new Vector3(1, 0), 100);
                f.skills.runSkillRanks["magic.lightning.echo"] = 10; // Force 100% for this isolated test only.
                f.combat.FireAtNearestEnemy(0); Check(Near(100 - a.hitPoints, f.tables.balance.baseDamage * .75f * 1.5f), "Echo adds one half-damage hit, never a recursive chain");
                f.run.characters.Select("flame"); f.Begin();
                Enemy front = f.Enemy(new Vector3(1, 0), 100), back = f.Enemy(new Vector3(-1, 0), 100), side = f.Enemy(new Vector3(1, 2), 100);
                f.skills.runSkillRanks["magic.flame.burn"] = 1;
                f.combat.FireAtNearestEnemy(0); Check(front.hitPoints < 100 && Near(back.hitPoints, 100) && Near(side.hitPoints, 100), "Flame affects only real forward cone");
                float hp = front.hitPoints; f.skills.UpdatePrototypeEffects(0); Check(Near(front.hitPoints, hp), "Paused DOT does not tick");
                f.skills.UpdatePrototypeEffects(1); Check(front.hitPoints < hp && Near(f.mana.SpentThisRun, 1), "Burn ticks without another MP charge");
                Check(!CharacterMagicSystem.InCone(new Vector2(4, 0), Vector2.right, 3, 60), "Cone excludes out-of-range targets");
                f.Begin(); front = f.Enemy(new Vector3(5, 0), 100); f.combat.FireAtNearestEnemy(0); Check(Near(f.mana.SpentThisRun, 0), "Flame never spends MP on out-of-range targets");
                Check(f.save.progress.Rank("meteor") == 1 && f.save.progress.Rank("chainChance") == 2, "Archived investments survive every selection/restart");
                PrototypeSave restored = JsonUtility.FromJson<PrototypeSave>(JsonUtility.ToJson(f.save.progress));
                Check(restored.selectedCharacter == "flame" && restored.Rank("magic.projectile.damage") == 1 && restored.Rank("meteor") == 1, "Save roundtrip preserves character and old/new investments");
                VerifyFrost(f);
                GrowthGraphView view = (GrowthGraphView)RuntimeTestAccess.Get(f.manager, "growthView");
                foreach (string id in new[] { "projectile", "lightning", "flame", "frost" })
                {
                    f.run.characters.Select(id);
                    foreach (bool personal in new[] { false, true })
                    {
                        RuntimeTestAccess.Set(view, "personalGraph", personal); view.BuildPrototypeGraph();
                        Check(view.metaTreeNodes.Length == (personal ? f.tables.characters.Graphs[id].graphLayoutNodes.Length + 1 : f.tables.characters.CommonGraph.graphLayoutNodes.Length + 1), "Common/personal graph uses correct isolated node catalog");
                        foreach (Vector2 size in new[] { new Vector2(1920, 1080), new Vector2(1280, 720), new Vector2(960, 540) })
                        {
                            view.FitPrototypeGraph(size);
                            foreach (MetaTreeNodePresentation node in view.metaTreeNodes)
                            {
                                if (node.state == MetaTreeNodeState.Hidden) continue;
                                Rect card = view.GetMetaTreeNodeRect(node, size);
                                Check(card.xMin >= 0 && card.xMax <= size.x && card.yMin >= 0 && card.yMax <= size.y, "Visible graph card stays on screen");
                                foreach (Rect overlay in GrowthGraphView.GetGrowthOverlayRects(size, false)) Check(!card.Overlaps(overlay), "Card avoids selection/wallet/details/navigation overlay");
                            }
                        }
                    }
                }
            }
            finally { Time.timeScale = previousScale; Random.state = random; }
        }

        private static void CheckAllPurchases(GameTables graph)
        {
            PrototypeSave progress = new(); int wallet = int.MaxValue, bought = 0, expected = 0;
            foreach (PrototypeGrowth row in graph.growthTable.rows) expected += row.maxRank;
            // Ignore monster-stage gates only, not node dependencies, costs or purchase limits.
            for (int pass = 0; pass < 300; pass++)
            {
                bool changed = false;
                foreach (PrototypeGrowthNode node in graph.growthTable.nodes)
                    if (PrototypeRules.TryPurchaseNode(graph.balance, graph.GrowthRow(node.growthId), node, progress, 4, ref wallet, graph.growthTable))
                    { bought++; changed = true; }
                if (!changed) break;
            }
            Check(bought == expected, "Every growth path is reachable, capped and purchasable without deadlock");
        }

        private static void VerifyFrost(Fixture f)
        {
            f.run.characters.Select("frost"); f.Begin();
            Transform player = (Transform)RuntimeTestAccess.Get(f.manager, "player");
            PrototypeSkillVisuals visuals = f.manager.GetComponent<PrototypeSkillVisuals>();
            Enemy enemy = f.Enemy(Vector3.right * 1.8f, 100); enemy.speed = 1;
            f.combat.FireAtNearestEnemy(0);
            Check(Near(f.mana.SpentThisRun, 3) && f.skills.Frost.Active, "Frost has a higher 3 MP cast cost");
            float hp = enemy.hitPoints; f.skills.UpdatePrototypeEffects(0);
            Check(Near(enemy.hitPoints, hp) && Near(f.skills.Frost.Age, 0), "Paused orbit neither rotates nor damages");
            f.skills.UpdatePrototypeEffects(.01f);
            Check(enemy.hitPoints < hp && enemy.chillStacks == 1, "Orbit contact deals damage and adds cold");
            hp = enemy.hitPoints; f.skills.UpdatePrototypeEffects(.01f);
            Check(Near(enemy.hitPoints, hp), "Same shard cannot hit the same enemy every frame");
            f.combat.nextShot = 0; f.combat.FireAtNearestEnemy(1);
            Check(Near(f.mana.SpentThisRun, 3), "Active orbit cannot be recast or charged a second time");
            player.position = new Vector3(3, 2);
            f.skills.UpdatePrototypeEffects(.05f);
            IList sprites = (IList)RuntimeTestAccess.Get(visuals, "frostShards");
            Check(sprites.Count == 2 && Vector3.Distance(((SpriteRenderer)sprites[0]).transform.position,
                f.skills.Frost.Position(0, player.position, f.skills.Frost.Age)) < .001f, "Visible shards follow the player at the actual hit positions");
            f.skills.UpdatePrototypeEffects(5);
            Check(!f.skills.Frost.Active && sprites.Count == 0 && Near(f.mana.SpentThisRun, 3), "Duration expiry returns visuals without extra MP cost");
            f.Begin(); enemy = f.Enemy(Vector3.right * 10, 100); f.combat.FireAtNearestEnemy(0);
            Check(!f.skills.Frost.Active && Near(f.mana.SpentThisRun, 0), "No nearby enemy means no idle orbit or MP waste");
            f.Begin(); enemy = f.Enemy(Vector3.right * 1.8f, 100); enemy.speed = 1;
            f.skills.runSkillRanks["magic.frost.chill"] = 2;
            for (int i = 0; i < 10; i++)
            {
                f.selection.BuildPrototypeRunChoices();
                foreach (RunUpgradePresentation card in f.selection.runUpgradeChoices)
                    Check(card.upgrade != "magic.frost.chill", "Already guaranteed freeze cannot offer a redundant cold-stack upgrade");
            }
            f.combat.FireAtNearestEnemy(0); f.skills.UpdatePrototypeEffects(.01f);
            Check(enemy.freezeRemaining > 0 && enemy.chillStacks == 0, "Threshold freezes once and resets stacks");
            Vector3 frozenPosition = enemy.transform.position; f.enemies.UpdateEnemies(.1f);
            Check(enemy.transform.position == frozenPosition, "Frozen monster does not move");
            f.skills.UpdatePrototypeEffects(.01f);
            Check(Near(enemy.chillStacks, 0), "Frozen target cannot pre-stack another freeze");
            f.enemies.UpdateEnemies(1);
            Check(enemy.freezeRemaining == 0 && enemy.transform.position != frozenPosition, "Freeze expires and movement resumes");
            f.Begin(); enemy = f.Enemy(Vector3.right * 1.8f, 100); enemy.speed = 1;
            f.combat.FireAtNearestEnemy(0); f.skills.UpdatePrototypeEffects(.01f);
            Vector3 chilledPosition = enemy.transform.position; f.enemies.UpdateEnemies(.1f);
            Check(Near(Vector3.Distance(enemy.transform.position, chilledPosition), .08f), "Cold slows movement by table-defined twenty percent");
            f.enemies.UpdateEnemies(3); Check(enemy.chillStacks == 0 && enemy.chillRemaining == 0, "Cold stacks expire rather than accumulating forever");
            f.Begin(); enemy = f.Enemy(Vector3.right * 1.8f, 100);
            f.skills.runSkillRanks["magic.frost.count"] = 3; f.skills.runSkillRanks["magic.frost.size"] = 1;
            f.combat.FireAtNearestEnemy(0);
            Check(f.skills.Frost.Count == 5 && Near(f.mana.SpentThisRun, 3), "Extra shards retain one larger payment per cast");
            f.Begin(); Check(!f.skills.Frost.Active && sprites.Count == 0, "Restart clears the orbit and old cooldowns/visuals");
            enemy = f.Enemy(Vector3.right * 1.8f, 100); f.mana.Reset(2, 3); f.combat.FireAtNearestEnemy(0);
            Check(f.mana.IsDepleted && !f.skills.Frost.Active, "Insufficient MP cannot create a free orbit or fractional softlock");
            f.run.characters.Select("projectile"); f.Begin();
            Check(Near(f.mana.BasicAttackCost, 1) && !f.skills.Frost.Active, "Other characters restore their own cost and clear frost state");
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root, prefab;
            private readonly GameObjectPool pool;
            internal readonly GameManager manager;
            internal readonly GameTables tables;
            internal readonly SaveService save;
            internal readonly RunController run;
            internal readonly SkillSystem skills;
            internal readonly EnemySystem enemies;
            internal readonly CombatSystem combat;
            internal readonly LevelUpController selection;
            internal readonly RunMana mana;
            internal Fixture()
            {
                root = new GameObject("Character regression"); root.SetActive(false);
                manager = root.AddComponent<GameManager>(); pool = root.AddComponent<GameObjectPool>();
                tables = GameTables.LoadResources(new GameSettings()); tables.balance.manaAbsorbChance = 0; tables.balance.chestChance = 0; tables.balance.criticalChance = 0;
                save = new SaveService(tables, new MemoryProgressStore(), true); save.Load();
                manager.ComposeServices(tables, save, pool, root.AddComponent<PrototypeSkillVisuals>());
                GameObject player = new GameObject("Test player"); player.transform.SetParent(root.transform); player.AddComponent<SpriteRenderer>(); player.AddComponent<Animator>();
                RuntimeTestAccess.Set(manager, "player", player.transform); RuntimeTestAccess.Set(manager, "playerController", player.AddComponent<Player>());
                RuntimeTestAccess.Call(player.GetComponent<PlayerAnimation>(), "Awake"); RuntimeTestAccess.Call(player.GetComponent<Player>(), "Awake");
                prefab = new GameObject("Default test sprite"); prefab.transform.SetParent(root.transform); prefab.AddComponent<SpriteRenderer>();
                ProjectDMRuntimeAssets assets = new();
                foreach (string property in new[] { "EnemyPrefab", "ProjectilePrefab", "ChestPickupPrefab" }) typeof(ProjectDMRuntimeAssets).GetProperty(property).SetValue(assets, prefab);
                foreach (string property in new[] { "ExperiencePickupPrefabs", "CurrencyPickupPrefabs" }) typeof(ProjectDMRuntimeAssets).GetProperty(property).SetValue(assets, new[] { prefab, prefab, prefab, prefab, prefab });
                RuntimeTestAccess.Set(manager, "runtimeAssets", assets);
                run = (RunController)RuntimeTestAccess.Get(manager, "run"); skills = (SkillSystem)RuntimeTestAccess.Get(manager, "skills");
                combat = (CombatSystem)RuntimeTestAccess.Get(manager, "combat"); enemies = (EnemySystem)RuntimeTestAccess.Get(manager, "enemiesSystem");
                selection = (LevelUpController)RuntimeTestAccess.Get(manager, "selection"); mana = (RunMana)RuntimeTestAccess.Get(manager, "mana");
                Begin();
            }
            internal void Begin() { run.BeginRun(); enemies.Reset(); }
            internal Enemy Enemy(Vector3 position, float hp)
            { enemies.SpawnPrototypeEnemy(true); Enemy enemy = enemies.enemies[enemies.enemies.Count - 1]; enemy.transform.position = position; enemy.hitPoints = hp; return enemy; }
            public void Dispose()
            {
                IDictionary instances = (IDictionary)RuntimeTestAccess.Get(pool, "sourceByInstance");
                foreach (GameObject instance in instances.Keys) if (instance != null) Object.DestroyImmediate(instance);
                Object.DestroyImmediate(root);
            }
        }
    }
}
