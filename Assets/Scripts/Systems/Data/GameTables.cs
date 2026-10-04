using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Validates and indexes the supplied balance, skill and growth tables.</summary>
    internal sealed class GameTables
    {
        internal GameTables(PrototypeBalance suppliedBalance, PrototypeSkillTable suppliedSkills, PrototypeGrowthTable suppliedGrowth)
        {

            balance = suppliedBalance; skillTable = suppliedSkills; growthTable = suppliedGrowth;
            if (growthTable.nodes == null) growthTable.nodes = PrototypeRules.CreateFallbackNodes(growthTable.rows);
            foreach (PrototypeGrowth row in growthTable.rows) growthLookup.Add(row.id, row);
            foreach (PrototypeSkill row in skillTable.rows) skillLookup.Add(row.id, row);
            foreach (PrototypeGrowthNode node in growthTable.nodes) growthNodeLookup.Add(node.id, node);
            if (growthTable.goalLine != null) foreach (PrototypeGoalNode node in growthTable.goalLine) goalNodeLookup.Add(node.id, node);
            graphLayoutNodes = PrototypeRules.CreateGraphLayout(growthTable);
        }

        internal PrototypeBalance balance;

        internal PrototypeSkillTable skillTable;

        internal PrototypeGrowthTable growthTable;

        internal readonly Dictionary<string, PrototypeGrowth> growthLookup = new();

        internal readonly Dictionary<string, PrototypeSkill> skillLookup = new();

        internal readonly Dictionary<string, PrototypeGrowthNode> growthNodeLookup = new();
        internal readonly Dictionary<string, PrototypeGoalNode> goalNodeLookup = new();
        internal readonly PrototypeGrowthNode[] graphLayoutNodes;

        internal PrototypeGrowth GrowthRow(string id) => growthLookup[id];

        internal PrototypeGrowthNode GrowthNode(string id) => growthNodeLookup[id];

        internal PrototypeGrowth NodeGrowthRow(string nodeId) => GrowthRow(GrowthNode(nodeId).growthId);

        internal PrototypeSkill SkillRow(string id) => skillLookup[id];

        internal static GameTables LoadResources(GameSettings settings)
        {
            if (settings.prototypeBalanceTable == null) settings.prototypeBalanceTable = Resources.Load<TextAsset>("ProjectDM/Data/PrototypeBalance");
            if (settings.prototypeSkillTable == null) settings.prototypeSkillTable = Resources.Load<TextAsset>("ProjectDM/Data/PrototypeSkills");
            if (settings.prototypeGrowthTable == null) settings.prototypeGrowthTable = Resources.Load<TextAsset>("ProjectDM/Data/PrototypeGrowth");
            if (settings.prototypeBalanceTable == null || settings.prototypeSkillTable == null || settings.prototypeGrowthTable == null)
                throw new InvalidOperationException("프로토타입 JSON 테이블을 찾을 수 없습니다.");
            PrototypeBalance balance = JsonUtility.FromJson<PrototypeBalance>(settings.prototypeBalanceTable.text);
            PrototypeSkillTable skills = JsonUtility.FromJson<PrototypeSkillTable>(settings.prototypeSkillTable.text);
            PrototypeGrowthTable growth = JsonUtility.FromJson<PrototypeGrowthTable>(settings.prototypeGrowthTable.text);
            string error = PrototypeRules.Validate(balance, skills, growth);
            if (error != null) throw new InvalidOperationException(error);
            return new GameTables(balance, skills, growth);
        }
    }
}
