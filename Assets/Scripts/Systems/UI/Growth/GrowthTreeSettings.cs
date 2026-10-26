using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    [Serializable]
    public sealed class GrowthTreeNodeSettings
    {
        public bool enabled = true;
        public string id, growthId, name, parent = "ROOT", displayParent;
        public string VisualParent => string.IsNullOrEmpty(displayParent) ? parent : displayParent;
        public bool displayOrthogonal;
        public string[] displayLinks = Array.Empty<string>();
        public int ranks = 1, parentRanks;
        public float x, y;
    }

    [Serializable]
    public sealed class GrowthTreeDefinition
    {
        public string id, name;
        public GrowthTreeNodeSettings[] nodes = Array.Empty<GrowthTreeNodeSettings>();
    }

    /// <summary>Authored purchase nodes. Combat effects and saved ranks retain their existing growth IDs.</summary>
    public sealed class GrowthTreeSettings : ScriptableObject
    {
        public const string ResourcePath = "ProjectDM/Data/GrowthTrees";
        public GrowthTreeDefinition[] trees = Array.Empty<GrowthTreeDefinition>();
        public static readonly string[] TreeIds = { "common", "projectile", "flame", "lightning", "frost" };
        public static readonly string[] TreeNames = { "공통 성장", "마법미사일", "화염", "번개", "빙결" };

        internal static Dictionary<string, GameTables> Graphs(GameTables tables)
        {
            var graphs = new Dictionary<string, GameTables>(tables.characters.Graphs);
            graphs.Add("common", tables.characters.CommonGraph); return graphs;
        }

        internal static GrowthTreeSettings Defaults(GameTables tables)
        {
            var settings = CreateInstance<GrowthTreeSettings>(); var graphs = Graphs(tables);
            settings.trees = new GrowthTreeDefinition[TreeIds.Length];
            for (int i = 0; i < TreeIds.Length; i++)
            {
                var graph = graphs[TreeIds[i]];
                var nodes = graph.growthTable.nodes; var layout = graph.graphLayoutNodes;
                var result = new GrowthTreeNodeSettings[nodes.Length];
                for (int n = 0; n < result.Length; n++) result[n] = new GrowthTreeNodeSettings
                {
                    id = nodes[n].id, growthId = nodes[n].growthId, name = nodes[n].name,
                    parent = string.IsNullOrEmpty(nodes[n].parent) ? "ROOT" : nodes[n].parent, displayParent = nodes[n].displayParent, displayOrthogonal = nodes[n].displayOrthogonal,
                    displayLinks = nodes[n].displayLinks == null ? Array.Empty<string>() : (string[])nodes[n].displayLinks.Clone(),
                    ranks = nodes[n].ranks, parentRanks = nodes[n].parentRanks, x = layout[n].x, y = layout[n].y
                };
                settings.trees[i] = new GrowthTreeDefinition { id = TreeIds[i], name = TreeNames[i], nodes = result };
            }
            return settings;
        }

        internal string Validate(Dictionary<string, GameTables> defaults)
        {
            if (trees == null || trees.Length != TreeIds.Length) return "공통 및 캐릭터 4종의 트리 정의가 필요합니다.";
            var treeIds = new HashSet<string>(); var allIds = new HashSet<string> { "ROOT" };
            foreach (var graph in defaults.Values)
                if (graph.growthTable.goalLine != null) foreach (var goal in graph.growthTable.goalLine) allIds.Add(goal.id);
            foreach (var tree in trees)
            {
                if (tree == null || tree.id == null || !defaults.ContainsKey(tree.id) || !treeIds.Add(tree.id) || tree.nodes == null)
                    return "트리 ID 누락/중복 또는 지원하지 않는 트리입니다.";
                var graph = defaults[tree.id]; var ids = new Dictionary<string, GrowthTreeNodeSettings>();
                var totals = new Dictionary<string, int>(); var groups = new Dictionary<string, List<GrowthTreeNodeSettings>>();
                foreach (var node in tree.nodes)
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.id) || node.id != node.id.Trim() || !allIds.Add(node.id))
                        return tree.name + ": 노드 ID 누락/중복입니다. ID는 전체 트리에서 고유해야 합니다.";
                    if (node.growthId == null || !graph.growthLookup.ContainsKey(node.growthId)) return node.id + ": 이 트리에 없는 성장 종류입니다.";
                    if (string.IsNullOrWhiteSpace(node.name) || !Finite(node.x) || !Finite(node.y) || node.ranks < 1 || node.ranks > 3 || node.parentRanks < 0)
                        return node.id + ": 이름·좌표·구매 단계(1~3)를 확인하세요.";
                    if (!node.enabled) continue;
                    ids.Add(node.id, node);
                    if (!groups.TryGetValue(node.growthId, out var group)) groups.Add(node.growthId, group = new List<GrowthTreeNodeSettings>());
                    group.Add(node);
                    totals[node.growthId] = (totals.TryGetValue(node.growthId, out int total) ? total : 0) + node.ranks;
                    if (totals[node.growthId] > graph.GrowthRow(node.growthId).maxRank) return node.growthId + ": 생성하는 구매 단계 합계가 최대 단계를 초과합니다.";
                }
                foreach (var node in ids.Values)
                {
                    if (string.IsNullOrEmpty(node.parent) || node.parent == "ROOT")
                    { if (node.parentRanks != 0) return node.id + ": ROOT 연결의 필요 구매 단계는 0이어야 합니다."; }
                    else if (!ids.TryGetValue(node.parent, out var parent) || node.parent == node.id || node.parentRanks > parent.ranks)
                        return node.id + ": 부모가 없거나 생성 해제되었거나 필요 구매 단계가 잘못되었습니다.";
                    string requires = graph.GrowthRow(node.growthId).requires;
                    if (!string.IsNullOrEmpty(requires) && !groups.ContainsKey(requires)) return node.id + ": 선행 성장 " + requires + "의 노드도 생성해야 합니다.";
                    var seen = new HashSet<string> { node.id }; string at = node.parent;
                    while (!string.IsNullOrEmpty(at) && at != "ROOT")
                    {
                        if (!seen.Add(at)) return node.id + ": 연결이 순환합니다.";
                        if (!ids.TryGetValue(at, out var previous)) break;
                        at = previous.parent;
                    }
                    string display = node.VisualParent;
                    if (!string.IsNullOrEmpty(display) && display != "ROOT" && !ids.ContainsKey(display)) return node.id + ": 표시 연결의 노드가 없거나 생성 해제되었습니다.";
                    var links = new HashSet<string>();
                    foreach (string link in node.displayLinks ?? Array.Empty<string>())
                        if (string.IsNullOrEmpty(link) || link == node.id || !ids.ContainsKey(link) || !links.Add(link))
                            return node.id + ": 추가 표시 연결의 노드가 없거나 자기 자신/중복 연결입니다.";
                    seen.Clear(); seen.Add(node.id); at = display;
                    while (!string.IsNullOrEmpty(at) && at != "ROOT")
                    {
                        if (!seen.Add(at)) return node.id + ": 표시 연결이 순환합니다.";
                        if (!ids.TryGetValue(at, out var previous)) break;
                        at = previous.VisualParent;
                    }
                }
                // Purchase dependencies include the earlier segment of the same stat and its
                // stat-wide unlock condition, in addition to the authored parent connector.
                var visiting = new HashSet<string>(); var complete = new HashSet<string>();
                bool Visit(GrowthTreeNodeSettings node)
                {
                    if (complete.Contains(node.id)) return false;
                    if (!visiting.Add(node.id)) return true;
                    if (node.parentRanks > 0 && Visit(ids[node.parent])) return true;
                    var group = groups[node.growthId]; int index = group.IndexOf(node);
                    if (index > 0 && Visit(group[index - 1])) return true;
                    string requires = graph.GrowthRow(node.growthId).requires;
                    if (!string.IsNullOrEmpty(requires) && Visit(groups[requires][0])) return true;
                    visiting.Remove(node.id); complete.Add(node.id); return false;
                }
                foreach (var node in ids.Values) if (Visit(node)) return node.id + ": 구매 조건이 서로를 기다리는 순환입니다.";
            }
            return null;
        }

        internal void Apply(GameTables tables)
        {
            var graphs = Graphs(tables); string error = Validate(graphs);
            if (error != null) throw new InvalidOperationException("성장트리 설정: " + error);
            tables.growthNodeLookup.Clear();
            foreach (var tree in trees)
            {
                var ranks = new Dictionary<string, int>(); var nodes = new List<PrototypeGrowthNode>();
                foreach (var node in tree.nodes)
                {
                    if (!node.enabled) continue;
                    ranks.TryGetValue(node.growthId, out int start);
                    var output = new PrototypeGrowthNode { id = node.id, growthId = node.growthId, name = node.name,
                        parent = string.IsNullOrEmpty(node.parent) ? "ROOT" : node.parent, displayParent = node.displayParent, displayOrthogonal = node.displayOrthogonal, parentRanks = node.parentRanks,
                        displayLinks = node.displayLinks == null ? Array.Empty<string>() : (string[])node.displayLinks.Clone(),
                        ranks = node.ranks, startRank = start, x = node.x, y = node.y };
                    ranks[node.growthId] = start + node.ranks; nodes.Add(output); tables.growthNodeLookup.Add(output.id, output);
                }
                var source = graphs[tree.id];
                var graph = new GameTables(source.balance, source.skillTable, new PrototypeGrowthTable
                { rows = source.growthTable.rows, nodes = nodes.ToArray(), goalLine = source.growthTable.goalLine, authoredLayout = true });
                tables.characters.SetGraph(tree.id, graph);
            }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
