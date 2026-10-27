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
