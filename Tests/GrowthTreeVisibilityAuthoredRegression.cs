// Copy into an isolated Unity project's Assets/Editor and run
// -executeMethod ProjectDM.Editor.GrowthTreeVisibilityAuthoredRegression.Run.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public static class GrowthTreeVisibilityAuthoredRegression
    {
        private static int checks;
        private static void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }

        public static void Run()
        {
            try
            {
                var settings = new GameSettings();
                var tables = GameTables.LoadResources(settings);
                var authored = Resources.Load<GrowthTreeSettings>(GrowthTreeSettings.ResourcePath);
                var workbook = GrowthTreeSpreadsheet.Import(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Design/GrowthTree.xlsx"));
                Check(workbook.Validate(GrowthTreeSettings.Graphs(tables)) == null, "Workbook must import as a valid growth table.");
                int unlockExceptions = 0;
                foreach (string id in GrowthTreeSettings.TreeIds)
                {
                    var source = authored.trees.Single(tree => tree.id == id);
                    var imported = workbook.trees.Single(tree => tree.id == id);
                    Check(source.nodes.Length == imported.nodes.Length, id + ": workbook node count differs.");
                    foreach (var node in source.nodes)
                    {
                        var row = imported.nodes.Single(other => other.id == node.id);
                        Check(node.enabled == row.enabled && node.growthId == row.growthId && node.ranks == row.ranks &&
                            node.parent == row.parent && node.parentRanks == row.parentRanks && node.VisualParent == row.VisualParent &&
                            node.displayOrthogonal == row.displayOrthogonal && node.x == row.x && node.y == row.y &&
                            (node.displayLinks ?? Array.Empty<string>()).SequenceEqual(row.displayLinks ?? Array.Empty<string>()),
                            node.id + ": workbook differs from the actual authored node.");
                        bool skillOrder = id != "common" && node.id.EndsWith("Unlock", StringComparison.Ordinal) &&
                            node.VisualParent == "ROOT" && node.parent != "ROOT";
                        if (skillOrder) unlockExceptions++;
                        else Check(node.parent == node.VisualParent, node.id + ": purchase parent differs from the visible path.");
                    }

                    var graph = id == "common" ? tables.characters.CommonGraph : tables.characters.Graphs[id];
                    Check(graph.growthTable.nodes.GroupBy(node => new Vector2(node.x, node.y)).All(group => group.Count() == 1), id + ": overlapping node centers.");
                    var fresh = new PrototypeSave();
                    foreach (var node in graph.growthTable.nodes)
                    {
                        Check(PrototypeRules.IsNodeRevealed(graph.growthTable, node, fresh, 0) == (node.VisualParent == "ROOT"),
                            node.id + ": fresh-save visibility is not one visual link from ROOT.");
                        var legacy = new PrototypeSave();
                        legacy.SetRank(node.growthId, node.startRank, .25);
                        Check(PrototypeRules.IsNodeRevealed(graph.growthTable, node, legacy, 0), node.id + ": old fractional investment disappeared.");
                    }

                    bool progressed;
                    int purchases = 0;
                    do
                    {
                        progressed = false;
                        foreach (var node in graph.growthTable.nodes)
                        {
                            if (!PrototypeRules.IsNodeRevealed(graph.growthTable, node, fresh, 4)) continue;
                            int gold = int.MaxValue;
                            if (!PrototypeRules.TryPurchaseNode(graph.balance, graph.GrowthRow(node.growthId), node, fresh, 4, ref gold, graph.growthTable)) continue;
                            purchases++;
                            progressed = true;
                        }
                    } while (progressed && purchases < 10000);
                    foreach (var node in graph.growthTable.nodes)
                        Check(PrototypeRules.NodeRank(node, fresh) == node.ranks, id + ": undiscoverable or blocked node " + node.id);
                    Debug.Log("GROWTH VISIBILITY " + id + ": " + graph.growthTable.nodes.Length + " nodes, " + purchases + " purchases, no deadlocks.");
                }
                Check(unlockExceptions == 8, "The second and third skill unlocks must preserve their existing order.");

                var huntingSave = new SaveService(tables, new MemoryProgressStore(), true) { progress = new PrototypeSave() };
                var progression = new ProgressionService(huntingSave, tables);
                var huntingRun = new RunController(settings, huntingSave, tables, progression, null, null, null, null, () => { }, () => { }, () => false, new RunMana());
                var playerStats = new PlayerStats(tables, progression);
                var skillSystem = new SkillSystem(tables, progression, null, huntingRun, null, null, () => false, () => 1, playerStats);
                var choices = new LevelUpController(tables, progression, huntingRun, skillSystem);
                var field = new EnemySystem(settings, huntingSave, tables, progression, null, null, huntingRun, null, null, null, null, (point, amount, kind) => { });
                UnityEngine.Random.InitState(314);
                foreach (var character in tables.characters.Definitions)
                {
                    huntingRun.characters.Select(character.id); huntingRun.characters.ApplySelected();
                    progression.ResetSnapshot();
                    Check(field.FieldTarget == tables.balance.stages[0].target, character.id + ": hunting leaked across characters.");
                    huntingSave.progress.SetRank("magic." + character.id + ".hunt", 12);
                    foreach (var upgrade in character.upgrades.Where(upgrade => upgrade.unlock)) huntingSave.progress.SetRank(upgrade.Key(character.id), 1);
                    Check(field.FieldTarget == tables.balance.stages[0].target, "A purchase changed the active run before ResetSnapshot.");
                    progression.ResetSnapshot();
                    Check(field.FieldTarget == tables.balance.stages[0].target + 36 && field.FieldCap == tables.balance.stages[0].cap + 36 && field.RefillBatch == tables.balance.stages[0].batch + 6,
                        character.id + ": hunting target/cap/refill did not match the purchased effect.");
                    for (int sample = 0; sample < 500; sample++)
                    {
                        choices.BuildPrototypeRunChoices();
                        Check(choices.runUpgradeChoices.All(choice => !choice.upgrade.EndsWith(".hunt", StringComparison.Ordinal)), "Permanent-only hunting appeared in a run card.");
                    }
                    Check(progression.CombatScale == 1, "Other characters' mastery changed this character's combat scale.");
                    huntingSave.progress.SetRank("damage", 40); huntingSave.progress.SetRank("criticalDamage", 3);
                    huntingSave.progress.SetRank("magic." + character.id + ".damage", 24);
                    foreach (var skill in character.skills.Skip(1)) huntingSave.progress.SetRank("magic." + character.id + "." + skill.id + "Power", 24);
                    Check(progression.CombatScale == 1, "Combat scale changed before the next-run snapshot.");
                    progression.ResetSnapshot();
                    Check(Math.Abs(progression.CombatScale - character.finalCombatScale) < .01, "Full mastery scale mismatch.");
                    skillSystem.runSkillRanks["magic." + character.id + ".damage"] = 10;
                    var finisher = character.skills[character.id == "projectile" ? 1 : 2];
                    skillSystem.runSkillRanks["magic." + character.id + "." + finisher.id + "Power"] = 3;
                    var combatStats = new CombatStats(tables, progression, skillSystem);
                    float hit = combatStats.MainDamage * finisher.damage * (1 + skillSystem.PersonalAmount(finisher.id + "Power")) * playerStats.CriticalDamage;
                    if (character.id == "frost") hit *= 2; // Frozen-target shatter.
                    var targetObject = new GameObject("QA damage target");
                    var target = new Enemy { transform = targetObject.transform, hitPoints = 999999 };
                    field.DamageElementalEnemy(target, hit, CombatDamageKind.Critical, CombatDamageElement.None);
                    Check(field.MaxDamageThisRun >= 974900 && field.MaxDamageThisRun <= 975100, "Practical hit did not reach 975k through the real damage path.");
                    Check(target.hitPoints > 24890 && target.hitPoints < 25110, "Only the popup changed; actual HP subtraction is wrong.");
                    UnityEngine.Object.DestroyImmediate(targetObject);
                    if (character.id == "frost")
                    {
                        var storm = character.skills.Single(skill => skill.id == "blizzard");
                        Check(storm.initialDamage == 3, "Blizzard needs an attainable heavy first pulse.");
                        var caster = new GameObject("QA blizzard caster");
                        var victimObject = new GameObject("QA blizzard victim");
                        var victim = new Enemy { transform = victimObject.transform, hitPoints = 999999 };
                        field.enemies.Add(victim);
                        var pulses = new System.Collections.Generic.List<float>();
                        var arsenal = new CharacterArsenalSystem(huntingRun.characters, field, null, () => combatStats.MainDamage,
                            skillSystem.PersonalAmount, skillSystem.IsSkillUnlocked, (enemy, power) => pulses.Add(power));
                        arsenal.Tick(.01f, caster.transform);
                        typeof(CharacterArsenalSystem).GetMethod("Cast", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(arsenal, new object[] { storm, victim });
                        arsenal.Tick(storm.tickInterval, caster.transform);
                        arsenal.Tick(storm.tickInterval, caster.transform);
                        Check(pulses.Count == 2 && Math.Abs(pulses[0] / pulses[1] - storm.initialDamage / storm.damage) < .001,
                            "Blizzard must use the heavy pulse once, then regular tick damage.");
                        field.enemies.Remove(victim);
                        UnityEngine.Object.DestroyImmediate(caster); UnityEngine.Object.DestroyImmediate(victimObject);
                    }
                }

                var save = new SaveService(tables, new MemoryProgressStore(), true) { progress = new PrototypeSave() };
                var run = new RunController(settings, save, tables, null, null, null, null, null, () => { }, () => { }, () => false, new RunMana());
                using (var view = new GrowthGraphView(settings, save, tables, null, null, run, null, null))
                {
                    var personal = typeof(GrowthGraphView).GetField("personalGraph", BindingFlags.Instance | BindingFlags.NonPublic);
                    foreach (string id in GrowthTreeSettings.TreeIds)
                    {
                        personal.SetValue(view, id != "common");
                        if (id != "common") run.characters.Select(id);
                        view.BuildPrototypeGraph();
                        var graph = id == "common" ? tables.characters.CommonGraph : tables.characters.Graphs[id];
                        for (int i = 0; i < graph.growthTable.nodes.Length; i++)
                        {
                            var node = graph.growthTable.nodes[i];
                            var drawn = view.metaTreeNodes[i + 1];
                            bool expected = PrototypeRules.IsNodeRevealed(graph.growthTable, node, save.progress, 0);
                            Check((drawn.state != MetaTreeNodeState.Hidden) == expected, node.id + ": runtime presentation leaked a node.");
                            if (!expected) Check(drawn.title == "" && drawn.subtitle == "", node.id + ": hidden text leaked.");
                        }
                    }
                }
                UnityEngine.Object.DestroyImmediate(workbook);
                Debug.Log("GROWTH VISIBILITY AUTHORED CHECK PASSED: " + checks + " assertions.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
    }
}
