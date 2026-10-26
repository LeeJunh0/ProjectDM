using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    [Serializable] internal sealed class CharacterTable { public CharacterDefinition[] characters; }
    [Serializable] internal sealed class CharacterDefinition
    {
        public string id, name, description;
        public bool enabled;
        public float damage, interval, manaCost, range, angle;
        public int extraTargets;
        public float duration, orbitSpeed, shardRadius, hitInterval, freezeSeconds, chillDuration, slowFraction;
        public int shardCount, chillThreshold;
        public CharacterUpgrade[] upgrades;
        public CharacterSkillDefinition[] skills;
    }
    [Serializable] internal sealed class CharacterSkillDefinition
    {
        public string id, name, description, icon;
        public float damage, interval, range, radius, duration, tickInterval, speed;
        public int count, charge;
    }
    [Serializable] internal sealed class CharacterUpgrade
    {
        public string id, name, description, icon, rarity;
        public string skill, unit;
        public bool unlock;
        public float step, permanentStep;
        public float effectiveCap;
        public int maxRank, cost;
        public string Key(string character) => "magic." + character + "." + id;
    }

    /// <summary>Temporary, replaceable character definitions. Archived shared-skill ranks are never removed.</summary>
    internal sealed class CharacterCatalog
    {
        internal readonly CharacterDefinition[] Definitions;
        internal readonly Dictionary<string, GameTables> Graphs = new();
        internal GameTables CommonGraph { get; private set; }
        internal void SetGraph(string id, GameTables graph)
        { if (id == "common") CommonGraph = graph; else Graphs[id] = graph; }

        internal CharacterCatalog(GameTables tables, CharacterTable source)
        {
            if (source?.characters == null || source.characters.Length != 4) throw new InvalidOperationException("캐릭터 테이블: 4개 정의가 필요합니다.");
            Definitions = source.characters;
            HashSet<string> ids = new();
            foreach (CharacterDefinition character in Definitions)
            {
                if (character == null || string.IsNullOrEmpty(character.id) || !ids.Add(character.id)) throw new InvalidOperationException("캐릭터 ID 중복/누락");
                if (!character.enabled) continue;
                if (character.id != "projectile" && character.id != "lightning" && character.id != "flame" && character.id != "frost") throw new InvalidOperationException("지원하지 않는 기본 마법");
                if (!FinitePositive(character.damage) || !FinitePositive(character.interval) || !FinitePositive(character.manaCost) ||
                    !FinitePositive(character.range) || character.extraTargets < 0 ||
                    (character.id == "flame" && (!FinitePositive(character.angle) || character.angle > 180)) || character.upgrades == null)
                    throw new InvalidOperationException("캐릭터 기본 수치/강화 정의 오류: " + character.id);
                if (character.id == "frost" && (!FinitePositive(character.duration) || !FinitePositive(character.orbitSpeed) ||
                    !FinitePositive(character.shardRadius) || !FinitePositive(character.hitInterval) || !FinitePositive(character.freezeSeconds) ||
                    !FinitePositive(character.chillDuration) || !FinitePositive(character.slowFraction) || character.slowFraction >= 1 ||
                    character.shardCount < 1 || character.shardCount > 16 || character.chillThreshold < 1)) throw new InvalidOperationException("빙결 공전 설정 오류");
                List<PrototypeGrowth> rows = new(); List<PrototypeGrowthNode> nodes = new(); HashSet<string> upgrades = new();
                string[] expected = character.id == "projectile" ? new[] { "damage", "speed", "count", "pierce", "homing" } :
                    character.id == "lightning" ? new[] { "damage", "speed", "jumps", "range", "echo" } :
                    character.id == "flame" ? new[] { "damage", "speed", "range", "width", "burn" } :
                    new[] { "damage", "speed", "count", "orbitSpeed", "range", "size", "chill", "freezeDuration" };
                if (character.skills == null || character.skills.Length != 3) throw new InvalidOperationException("캐릭터 스킬은 3개여야 합니다: " + character.id);
                var skillIds = new HashSet<string>();
                foreach (var skill in character.skills)
                    if (skill == null || string.IsNullOrEmpty(skill.id) || !skillIds.Add(skill.id) || string.IsNullOrEmpty(skill.name) ||
                        !FinitePositive(skill.damage) || !FinitePositive(skill.interval) || !FinitePositive(skill.range) ||
                        !FinitePositive(skill.radius) || !FinitePositive(skill.duration) || !FinitePositive(skill.tickInterval) ||
                        !FinitePositive(skill.speed) || skill.count < 1 || skill.count > 16 || skill.charge < 1)
                        throw new InvalidOperationException("전용 스킬 정의 오류: " + character.id);
                foreach (string required in expected)
                    if (Array.Find(character.upgrades, u => u.id == required) == null) throw new InvalidOperationException("기존 강화 누락: " + required);
                for (int i = 0; i < character.upgrades.Length; i++)
                {
                    CharacterUpgrade upgrade = character.upgrades[i];
                    if (upgrade == null || string.IsNullOrEmpty(upgrade.id) || !skillIds.Contains(upgrade.skill ?? "") || !upgrades.Add(upgrade.id) || upgrade.maxRank < 1 || upgrade.cost < 1 ||
                        !FinitePositive(upgrade.step) || !FinitePositive(upgrade.permanentStep) || float.IsNaN(upgrade.effectiveCap) || float.IsInfinity(upgrade.effectiveCap) || upgrade.effectiveCap < 0) throw new InvalidOperationException("전용 강화 오류: " + character.id);
                    if (upgrade.unlock && (upgrade.maxRank != 1 || upgrade.step != 1 || upgrade.permanentStep != 1)) throw new InvalidOperationException("해금 노드는 1단계여야 합니다.");
                    string key = upgrade.Key(character.id);
                    string prerequisite = upgrade.skill == character.skills[0].id || upgrade.unlock ? null : "magic." + character.id + "." + upgrade.skill + "Unlock";
                    rows.Add(new PrototypeGrowth { id = key, name = upgrade.name, description = upgrade.description + " (영구 성장 · 다음 런 적용)",
                        requires = prerequisite, icon = upgrade.icon, unit = upgrade.unit ?? (upgrade.id == "damage" || upgrade.id == "speed" || upgrade.id == "burn" ? "%" :
                            upgrade.id == "echo" ? "%p" : upgrade.id == "homing" || upgrade.id == "orbitSpeed" ? "°/초" : upgrade.id == "width" ? "°" :
                            upgrade.id == "range" || upgrade.id == "size" ? "유닛" : upgrade.id == "freezeDuration" ? "초" : "개"), baseCost = upgrade.cost, costGrowth = 1.8f, maxRank = Mathf.Min(3, upgrade.maxRank), step = upgrade.permanentStep });
                    // Two short outward branches, rather than a long chain or remote cross-links.
                    nodes.Add(new PrototypeGrowthNode { id = key, growthId = key, name = upgrade.name, ranks = Mathf.Min(3, upgrade.maxRank), parentRanks = i < 2 ? 0 : 1,
                        parent = i < 2 ? "ROOT" : character.upgrades[i - 2].Key(character.id),
                        x = (i % 2 == 0 ? -1 : 1) * (180 + i / 2 * 150), y = (i / 2 - 1) * 170 });
                }
                for (int i = 0; i < character.skills.Length; i++)
                {
                    var skill = character.skills[i];
                    if (i > 0 && Array.Find(character.upgrades, u => u.id == skill.id + "Unlock" && u.unlock) == null)
                        throw new InvalidOperationException("스킬 해금 강화 누락: " + skill.id);
                    foreach (var node in nodes)
                    {
                        var upgrade = Array.Find(character.upgrades, u => u.Key(character.id) == node.id);
                        if (upgrade.skill != skill.id) continue;
                        node.parent = i == 0 ? (upgrade.id == "damage" || upgrade.id == "speed" ? "ROOT" : character.upgrades[0].Key(character.id)) :
                            upgrade.unlock ? (i == 1 ? character.upgrades[0].Key(character.id) : "magic." + character.id + "." + character.skills[i - 1].id + "Unlock") :
                            "magic." + character.id + "." + skill.id + "Unlock";
                        node.parentRanks = node.parent == "ROOT" ? 0 : 1;
                    }
                }
                GameTables graph = new GameTables(tables.balance, tables.skillTable, new PrototypeGrowthTable { rows = rows.ToArray(), nodes = nodes.ToArray() });
                Graphs.Add(character.id, graph);
                foreach (PrototypeGrowth row in rows) tables.growthLookup.Add(row.id, row);
                foreach (PrototypeGrowthNode node in nodes) tables.growthNodeLookup.Add(node.id, node);
            }
            foreach (string required in new[] { "projectile", "lightning", "flame", "frost" })
                if (!ids.Contains(required)) throw new InvalidOperationException("캐릭터 누락: " + required);
            if (!Find("projectile").enabled) throw new InvalidOperationException("기본 발사 캐릭터가 활성화되어 있어야 합니다.");
            CommonGraph = CreateCommonGraph(tables);
        }

        private static bool FinitePositive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
        internal CharacterDefinition Find(string id) => Array.Find(Definitions, c => c.id == id);

        private static GameTables CreateCommonGraph(GameTables source)
        {
            HashSet<string> archived = new() { "multishot", "chain", "splash", "meteor", "chainChance", "splashChance", "meteorChance" };
            List<PrototypeGrowth> rows = new(); List<PrototypeGrowthNode> nodes = new();
            foreach (PrototypeGrowth row in source.growthTable.rows) if (!archived.Contains(row.id)) rows.Add(row);
            foreach (PrototypeGrowthNode node in source.growthTable.nodes)
            {
                if (archived.Contains(node.growthId)) continue;
                string parent = node.parent;
                while (source.growthNodeLookup.TryGetValue(parent ?? "", out PrototypeGrowthNode previous) && archived.Contains(previous.growthId)) parent = previous.parent;
                int parentRanks = parent == "ROOT" ? 0 : source.growthNodeLookup.TryGetValue(parent ?? "", out PrototypeGrowthNode retained) ? Mathf.Min(node.parentRanks, retained.ranks) : 0;
                nodes.Add(new PrototypeGrowthNode { id = node.id, growthId = node.growthId, name = node.name, parent = parent,
                    parentRanks = parentRanks, startRank = node.startRank, ranks = node.ranks, x = node.x, y = node.y });
            }
            return new GameTables(source.balance, source.skillTable, new PrototypeGrowthTable { rows = rows.ToArray(), nodes = nodes.ToArray(), goalLine = source.growthTable.goalLine });
        }
    }

    internal sealed class CharacterSession
    {
        internal readonly CharacterCatalog Catalog;
        private readonly PrototypeSave save;
        internal string SelectedId { get; private set; }
        internal CharacterDefinition Current { get; private set; }
        internal bool SelectionChanged => Current?.id != SelectedId;
        internal CharacterSession(CharacterCatalog catalog, PrototypeSave save)
        {
            Catalog = catalog; this.save = save;
            SelectedId = catalog.Find(save.selectedCharacter)?.enabled == true ? save.selectedCharacter : "projectile";
            Current = catalog.Find(SelectedId);
        }
        internal bool Select(string id)
        {
            if (Catalog.Find(id)?.enabled != true) return false;
            SelectedId = save.selectedCharacter = id; return true;
        }
        internal void ApplySelected() => Current = Catalog.Find(SelectedId);
    }
}
