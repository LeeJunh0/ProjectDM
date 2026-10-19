using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Owns the permanent-growth snapshot applied for the current run.</summary>
    internal sealed class ProgressionService
    {
        private readonly SaveService save;
        private readonly GameTables tables;

        internal ProgressionService(SaveService save, GameTables tables)
        {
            this.save = save;
            this.tables = tables;
        }

        internal readonly Dictionary<string, int> activeGrowthRanks = new();

        internal readonly Dictionary<string, double> activeGrowthPartialRanks = new();

        internal int ActiveRank(string id) => activeGrowthRanks.TryGetValue(id, out int value) ? value : 0;

        internal string GrowthNodeRankLabel(string nodeId)
        {
            PrototypeGrowthNode node = tables.GrowthNode(nodeId);
            double value = System.Math.Min(node.ranks, System.Math.Max(0,
                save.progress.Rank(node.growthId) + save.progress.PartialRank(node.growthId) - node.startRank));
            return $"{value:0.##}/{node.ranks}";
        }

        internal float PermanentAmount(string id) => !tables.growthLookup.ContainsKey(id) ? 0 : (float)PrototypeRules.Amount(tables.balance, tables.GrowthRow(id), ActiveRank(id),
            activeGrowthPartialRanks.TryGetValue(id, out double partial) ? partial : 0);

        internal void ResetSnapshot()
        {
            save.FlushPrototypeProgress();
            activeGrowthRanks.Clear();
            activeGrowthPartialRanks.Clear();
            foreach (PrototypeGrowth row in tables.growthLookup.Values)
            {
                activeGrowthRanks[row.id] = Mathf.Min(save.progress.Rank(row.id), row.maxRank);
                activeGrowthPartialRanks[row.id] = save.progress.Rank(row.id) < row.maxRank ? save.progress.PartialRank(row.id) : 0;
            }
        }
    }
}
