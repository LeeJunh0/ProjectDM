// Copy to an isolated project's Assets/Editor; execute ProjectDM.Editor.ManaEfficiencyRegression.Run.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    public static class ManaEfficiencyRegression
    {
        private static int checks;
        private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        public static void Run()
        {
            try
            {
                var tables = GameTables.LoadResources(new GameSettings());
                var save = new SaveService(tables, new MemoryProgressStore(), true) { progress = new PrototypeSave() };
                var progression = new ProgressionService(save, tables);
                var row = tables.GrowthRow("manaEfficiency");
                Check(row.maxRank == 5 && Mathf.Abs(row.step - .1f) < .00001f, "Efficiency is five ranks at ten percentage points");
                progression.ResetSnapshot();
                Check(progression.PermanentAmount(row.id) == 0 && RunMana.EfficientCost(1, 0) == 1, "Old saves without efficiency preserve normal cost");
                save.progress.SetRank(row.id, 5);
                Check(progression.PermanentAmount(row.id) == 0, "Buying efficiency cannot change the running snapshot");
                progression.ResetSnapshot();
                Check(Mathf.Abs(progression.PermanentAmount(row.id) - .5f) < .0001f, "Next run applies 50 percent reduction");
                foreach (var character in tables.characters.Definitions)
                {
                    float cost = RunMana.EfficientCost(character.manaCost, progression.PermanentAmount(row.id));
                    Check(Mathf.Abs(cost - character.manaCost * .5f) < .0001f, "Every character shares efficiency");
                    var mana = new RunMana(); mana.Reset(40, cost);
                    Check(mana.SpendForBasicAttack() && Mathf.Abs(mana.Current - (40 - cost)) < .0001f, "Attack consumes the reduced cost");
                    float before = mana.Current; mana.Consume(3);
                    Check(Mathf.Abs(before - mana.Current - 3) < .0001f, "Mana shield hit remains full damage");
                }
                Check(RunMana.EfficientCost(1, 10) == .5f && RunMana.EfficientCost(1, -1) == 1, "Invalid growth cannot create free attacks or extra cost");
                var graph = tables.characters.CommonGraph;
                var nodes = graph.growthTable.nodes.Where(n => n.growthId == row.id).ToArray();
                Check(nodes.Length == 5 && nodes.All(n => n.ranks == 1), "Five separate nodes each allow exactly one purchase");
                Check(graph.growthTable.nodes.GroupBy(n => new Vector2(n.x,n.y)).All(g => g.Count() == 1), "No nodes overlap");
                var fresh = new PrototypeSave();
                Check(!PrototypeRules.IsNodeRevealed(graph.growthTable, nodes[0], fresh, 0), "Efficiency stays concealed before the mana branch");
                foreach (var timeNode in graph.growthTable.nodes.Where(n => n.growthId == "time")) fresh.SetRank("time", 10);
                Check(PrototypeRules.IsNodeRevealed(graph.growthTable, nodes[0], fresh, 0), "Finishing the mana branch reveals efficiency");
                int wallet = 1000000;
                foreach (var node in nodes)
                for (int i = 0; i < node.ranks; i++) Check(PrototypeRules.TryPurchaseNode(graph.balance,row,node,fresh,0,ref wallet,graph.growthTable), "Efficiency purchase chain must be traversable");
                Check(fresh.Rank(row.id) == 5, "Efficiency reaches full rank");
                Check(JsonUtility.FromJson<PrototypeSave>(JsonUtility.ToJson(fresh)).Rank(row.id) == 5, "Efficiency survives save serialization");
                var authored = Resources.Load<GrowthTreeSettings>(GrowthTreeSettings.ResourcePath);
                string workbook = Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Design/GrowthTree.xlsx");
                GrowthTreeSpreadsheet.Export(workbook, authored, GrowthTreeSettings.Graphs(tables));
                var imported = GrowthTreeSpreadsheet.Import(workbook);
                Check(imported.Validate(GrowthTreeSettings.Graphs(tables)) == null, "Updated workbook round-trips validly");
                UnityEngine.Object.DestroyImmediate(imported);
                Debug.Log("MANA EFFICIENCY PASSED: " + checks + " assertions; workbook exported"); EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
