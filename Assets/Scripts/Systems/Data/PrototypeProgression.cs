using System;
using System.Collections.Generic;

namespace ProjectDM
{
    // Plain C# table/state types: no Unity dependencies, so progression can be regression tested.
    [Serializable]
    public sealed class PrototypeBalance
    {
        public string version;
        public float baseDamage, attackInterval, projectileSpeed, projectileLifetime, moveSpeed;
        public float initialSeconds, bossElapsedSeconds, timeRewardChance, timeRewardSeconds;
        public float firstDamageBonus, latePriceScale, xpFirst, xpGrowth;
        public float initialSpawnMin, initialSpawnMax, refillSpawnMin, refillSpawnMax;
        public float chestChance;
        public int chestCoins;
        public float playerHealth = 100;
        public float playerInvulnerability = .6f;
        public float playerContactRadius = .55f;
        public float criticalChance = .05f;
        public float criticalMultiplier = 2;
        public PrototypeMonster[] monsters;
        public PrototypeStage[] stages;
        public PrototypeLootGrade[] lootGrades;
    }

    [Serializable]
    public sealed class PrototypeMonster
    {
        public string id, name;
        public float hp, speed, xp, bonusChance;
        public int coins, bonusCoins, nextUnlockKills;
        public float contactDamage = 1;
        public int currencyGrade = 1, experienceGrade = 1;
        public float[] currencyChances;
    }

    [Serializable]
    public sealed class PrototypeStage
    {
        public int initial, target, cap, batch;
        public float refillSeconds;
        public int[] weights;
    }

    [Serializable]
    public sealed class PrototypeSkillTable { public PrototypeSkill[] rows; }

    [Serializable]
    public sealed class PrototypeSkill
    {
        public string id, name, description, unlock, icon, rarity;
        public int maxRank, targets;
        public float step, chance, damageRatio, range, radius, delay;
        // Optional per-run skill ranks beyond acquisition. Kept at rank 1 in the V2 prototype.
        public float rankChanceStep, rankDamageStep;
    }

    [Serializable]
    public sealed class PrototypeGrowthTable
    {
        public PrototypeGrowth[] rows;
        public PrototypeGrowthNode[] nodes;
        public PrototypeGoalNode[] goalLine;
    }

    [Serializable]
    public sealed class PrototypeGoalNode
    {
        public string id, name, description, kind, parent, icon;
        public int stage;
        public float x, y, limitSeconds;
        public bool final;
    }

    [Serializable]
    public sealed class PrototypeGrowthNode
    {
        public string id, growthId, name, parent;
        public int startRank, ranks, parentRanks;
        public float x, y;
    }

    [Serializable]
    public sealed class PrototypeGrowth
    {
        public string id, name, description, requires, parent, icon, unit;
        public int baseCost, maxRank, minStage, legacyMaxRank;
        public float costGrowth, step, damageRatio, x, y, costRampRanks;
    }

    [Serializable]
    public sealed class PrototypeRank
    {
        public string id;
        public int rank;
        public double partialRank;
        public PrototypeRank() { }
        public PrototypeRank(string id, int rank) { this.id = id; this.rank = rank; }
    }

    [Serializable]
    public sealed class PrototypeSave
    {
        public int version = 3;
        public int currencyDropsVersion;
        public int[] kills = new int[5];
        public List<PrototypeRank> ranks = new List<PrototypeRank>();
        public int completedRuns;
        public double playedSeconds;
        public bool reachedBoss;

        public int Rank(string id)
        {
            foreach (PrototypeRank item in ranks)
                if (item.id == id) return Math.Max(0, item.rank);
            return 0;
        }

        public double PartialRank(string id)
        {
            foreach (PrototypeRank item in ranks)
                if (item.id == id) return Math.Max(0, Math.Min(.999999999, item.partialRank));
            return 0;
        }

        public void SetRank(string id, int value, double partial = 0)
        {
            foreach (PrototypeRank item in ranks)
                if (item.id == id) { item.rank = Math.Max(0, value); item.partialRank = partial; return; }
            ranks.Add(new PrototypeRank(id, Math.Max(0, value)) { partialRank = partial });
        }
    }

    public static class PrototypeRules
    {
        public static int Stage(PrototypeBalance balance, PrototypeSave save)
        {
            int stage = 0;
            while (stage < balance.monsters.Length - 1 &&
                   save.kills[stage] >= balance.monsters[stage].nextUnlockKills) stage++;
            return stage;
        }

        public static int Cost(PrototypeGrowth row, int rank, double latePriceScale)
        {
            double scale = LegacyRankScale(row);
            // Keep the affordable first purchase; later purchases bundle the old curve's costs.
            if (scale == 1 || rank <= 0) return LegacyCost(row, rank, latePriceScale);
            double start = rank * scale, end = (rank + 1) * scale, price = 0;
            for (int legacy = (int)Math.Floor(start); legacy < Math.Ceiling(end); legacy++)
                price += LegacyCost(row, legacy, latePriceScale) * (Math.Min(end, legacy + 1) - Math.Max(start, legacy));
            if (row.costRampRanks > 0) price *= 1 - Math.Exp(-rank / row.costRampRanks);
            return (int)Math.Min(int.MaxValue, Math.Max(1, Math.Ceiling(price - 1e-8)));
        }

        private static int LegacyCost(PrototypeGrowth row, int rank, double latePriceScale)
        {
            double price = row.baseCost * Math.Pow(row.costGrowth, Math.Max(0, rank)) *
                (1 + (latePriceScale - 1) * (1 - Math.Exp(-Math.Max(0, rank) / 8.0)));
            return (int)Math.Min(int.MaxValue, Math.Max(1, Math.Ceiling(price)));
        }

        public static double LegacyRankScale(PrototypeGrowth row) => row.legacyMaxRank > 0 ? (double)row.legacyMaxRank / row.maxRank : 1;

        public static void MigrateGrowthRanks(PrototypeSave save, PrototypeGrowthTable table)
        {
            if (save.version >= 3) { MigrateCurrencyGrowthRanks(save, table); return; }
            int sourceVersion = save.version;
            double oldProcAmount = (save.Rank("proc") + save.PartialRank("proc")) * (sourceVersion == 1 ? .0008 : .004);
            if (sourceVersion == 1) foreach (PrototypeRank entry in save.ranks)
            {
                PrototypeGrowth row = Array.Find(table.rows, r => r.id == entry.id);
                if (row == null) continue;
                double converted = Math.Min(row.maxRank, Math.Max(0, entry.rank) / LegacyRankScale(row));
                entry.rank = (int)Math.Floor(converted + 1e-9);
                entry.partialRank = Math.Max(0, converted - entry.rank);
            }
            foreach (string id in new[] { "chainChance", "splashChance", "meteorChance" })
            {
                PrototypeGrowth row = Array.Find(table.rows, r => r.id == id);
                if (row == null || row.step <= 0) continue;
                double converted = Math.Min(row.maxRank, oldProcAmount / row.step);
                converted = Math.Max(converted, save.Rank(id) + save.PartialRank(id));
                double rounded = Math.Round(converted);
                if (Math.Abs(converted - rounded) < 1e-5) converted = rounded;
                int rank = (int)Math.Floor(converted + 1e-7);
                save.SetRank(id, Math.Min(row.maxRank, rank), rank < row.maxRank ? Math.Max(0, converted - rank) : 0);
            }
            // Retain the archived proc entry for recovery; it is no longer used by combat or the graph.
            save.version = 3;
            MigrateCurrencyGrowthRanks(save, table);
        }

        private static void MigrateCurrencyGrowthRanks(PrototypeSave save, PrototypeGrowthTable table)
        {
            if (save.currencyDropsVersion >= 1) return;
            PrototypeGrowth silver = Array.Find(table.rows, row => row.id == "silverDropChance");
            if (silver == null) return; // Legacy/custom growth tables retain their old interpretation.
            double inherited = save.Rank("currencyGradeChance") + save.PartialRank("currencyGradeChance");
            inherited = Math.Min(silver.maxRank, Math.Max(inherited, save.Rank(silver.id) + save.PartialRank(silver.id)));
            if (inherited > 0) save.SetRank(silver.id, (int)Math.Floor(inherited), inherited - Math.Floor(inherited));
            save.currencyDropsVersion = 1; // Keep archived ranks, but never reapply them after later purchases.
        }

        public static double Amount(PrototypeBalance balance, PrototypeGrowth row, int rank, double partialRank = 0)
        {
            double effective = Math.Min(row.maxRank, Math.Max(0, rank + partialRank));
            return effective * row.step +
                (row.id == "damage" && effective > 0 ? balance.firstDamageBonus : 0);
        }

        public static string PurchaseBlock(PrototypeGrowth row, PrototypeSave save, int stage, int gold, double priceScale)
        {
            if (save.Rank(row.id) >= row.maxRank) return "최대 성장 완료";
            if (stage < row.minStage) return "몬스터 " + (row.minStage + 1) + "단계 해금 필요";
            if (!string.IsNullOrEmpty(row.requires) && save.Rank(row.requires) < 1)
                return "선행 성장 해금 필요: " + row.requires;
            if (gold < Cost(row, save.Rank(row.id), priceScale)) return "재화 부족";
            return null;
        }

        public static bool TryPurchase(PrototypeBalance balance, PrototypeGrowth row, PrototypeSave save, int stage, ref int gold)
        {
            if (PurchaseBlock(row, save, stage, gold, balance.latePriceScale) != null) return false;
            int rank = save.Rank(row.id);
            gold -= Cost(row, rank, balance.latePriceScale);
            save.SetRank(row.id, rank + 1, rank + 1 < row.maxRank ? save.PartialRank(row.id) : 0);
            return true;
        }

        public static int NodeRank(PrototypeGrowthNode node, PrototypeSave save)
        { return Math.Min(node.ranks, Math.Max(0, save.Rank(node.growthId) - node.startRank)); }

        public static float GraphFitZoom(PrototypeGrowthNode[] nodes, float width, float height)
        {
            double horizontal = 1, vertical = 1;
            foreach (PrototypeGrowthNode node in nodes)
            {
                horizontal = Math.Max(horizontal, Math.Abs(node.x));
                vertical = Math.Max(vertical, Math.Abs(node.y));
            }
            return (float)Math.Max(.12, Math.Min(1.05,
                Math.Min(Math.Max(1, width - 160) / (horizontal * 2), Math.Max(1, height - 160) / (vertical * 2))));
        }

        // Display coordinates only: keep purchase dependencies, prices, ranks and original radial data intact.
        public static PrototypeGrowthNode[] CreateGraphLayout(PrototypeGrowthTable table)
        {
            int goals = table.goalLine == null ? 0 : table.goalLine.Length;
            PrototypeGrowthNode[] layout = new PrototypeGrowthNode[table.nodes.Length + goals];
            for (int i = 0; i < table.nodes.Length; i++)
            {
                PrototypeGrowthNode node = table.nodes[i];
                float side = node.x < 0 ? -1 : 1;
                if (Math.Abs(node.x) < 2)
                {
                    PrototypeGrowthNode parent = Array.Find(table.nodes, n => n.id == node.parent);
                    double branchDirection = 0;
                    foreach (PrototypeGrowthNode child in table.nodes) if (child.parent == node.id) branchDirection += child.x;
                    if (Math.Abs(branchDirection) < .001) branchDirection = parent == null ? node.x : parent.x;
                    side = branchDirection < 0 ? -1 : 1;
                }
                layout[i] = new PrototypeGrowthNode { id = node.id, growthId = node.growthId, name = node.name,
                    parent = node.parent, parentRanks = node.parentRanks, startRank = node.startRank, ranks = node.ranks,
                    x = node.x + (goals > 0 ? side * 180 : 0), y = node.y };
            }
            for (int i = 0; i < goals; i++)
            {
                PrototypeGoalNode goal = table.goalLine[i];
                layout[table.nodes.Length + i] = new PrototypeGrowthNode { id = goal.id, name = goal.name,
                    parent = goal.parent, x = goal.x, y = goal.y };
            }
            return layout;
        }

        private static string ValidateGoalLine(PrototypeBalance balance, PrototypeGrowthTable table)
        {
            if (table.goalLine == null || table.goalLine.Length == 0) return null;
            if (table.goalLine.Length > 32 || table.goalLine.Length % 2 != 0) return "목표 라인은 스테이지/보스 쌍으로 설정해야 합니다.";
            HashSet<string> ids = new HashSet<string> { "ROOT" };
            if (table.nodes != null) foreach (PrototypeGrowthNode node in table.nodes) ids.Add(node.id);
            string parent = "ROOT";
            float previousY = 0;
            for (int i = 0; i < table.goalLine.Length; i++)
            {
                PrototypeGoalNode goal = table.goalLine[i];
                bool boss = i % 2 == 1;
                if (goal == null || string.IsNullOrEmpty(goal.id) || !ids.Add(goal.id) || string.IsNullOrEmpty(goal.name) ||
                    string.IsNullOrEmpty(goal.description) || goal.parent != parent || goal.kind != (boss ? "boss" : "stage") ||
                    goal.stage != i / 2 + 1 || goal.x != 0 || float.IsNaN(goal.y) || float.IsInfinity(goal.y) ||
                    goal.y > previousY - 120 || goal.y < previousY - 380 ||
                    (boss && (!Positive(goal.limitSeconds) || goal.limitSeconds < balance.initialSeconds)) ||
                    (!boss && goal.limitSeconds != 0) || (goal.final && (!boss || i != table.goalLine.Length - 1)))
                    return "목표 노드의 순서/12시 연결/스테이지/보스 리미트 설정 오류: " + (i + 1);
                parent = goal.id; previousY = goal.y;
            }
            return null;
        }

        public static PrototypeGrowthNode[] CreateFallbackNodes(PrototypeGrowth[] rows)
        {
            List<PrototypeGrowthNode> nodes = new List<PrototypeGrowthNode>();
            bool more = true;
            for (int tier = 0; more; tier++)
            {
                more = false;
                foreach (PrototypeGrowth row in rows)
                {
                    int start = tier * 3;
                    if (start >= row.maxRank) continue;
                    more = true;
                    int index = nodes.Count;
                    double angle = (index * 137.507764 - 90) * Math.PI / 180;
                    double radius = 230 + (index / 12) * 190;
                    PrototypeGrowthNode parent = nodes.FindLast(n => n.growthId != row.id && n.startRank < start);
                    nodes.Add(new PrototypeGrowthNode { id = row.id + "_" + (tier + 1), growthId = row.id,
                        name = row.name + " " + (tier + 1), startRank = start, ranks = Math.Min(3, row.maxRank - start),
                        parent = parent?.id ?? "ROOT", parentRanks = 0,
                        x = (float)(Math.Cos(angle) * radius), y = (float)(Math.Sin(angle) * radius) });
                }
            }
            return nodes.ToArray();
        }

        public static string NodePurchaseBlock(PrototypeBalance balance, PrototypeGrowth row, PrototypeGrowthNode node,
            PrototypeSave save, int stage, int gold, PrototypeGrowthTable table = null)
        {
            if (NodeRank(node, save) >= node.ranks) return "이 노드의 성장 완료";
            if (save.Rank(node.growthId) < node.startRank) return "앞 단계 성장 노드를 먼저 완료하세요";
            string dependency = NodeUnlockBlock(table, node, save);
            if (dependency != null) return dependency;
            return PurchaseBlock(row, save, stage, gold, balance.latePriceScale);
        }

        public static string NodeUnlockBlock(PrototypeGrowthTable table, PrototypeGrowthNode node, PrototypeSave save)
        {
            if (save.Rank(node.growthId) < node.startRank) return "앞 단계 성장 노드를 먼저 완료하세요";
            if (node.parentRanks <= 0) return null;
            if (table?.nodes == null) return "선행 노드 테이블 설정 오류";
            PrototypeGrowthNode parent = Array.Find(table.nodes, n => n.id == node.parent);
            if (parent == null) return "선행 노드 설정 오류";
            int current = NodeRank(parent, save);
            return current >= node.parentRanks ? null : $"{parent.name} {current}/{node.parentRanks}단계 필요";
        }

        public static bool IsNodeOpen(PrototypeGrowthTable table, PrototypeGrowthNode node, PrototypeSave save, int stage)
        {
            if (table?.rows == null || node == null || save == null) return false;
            // Keep already-invested nodes visible, including fractional progress from old saves.
            if (save.Rank(node.growthId) + save.PartialRank(node.growthId) > node.startRank) return true;
            PrototypeGrowth row = Array.Find(table.rows, r => r.id == node.growthId);
            return row != null && stage >= row.minStage && NodeUnlockBlock(table, node, save) == null &&
                (string.IsNullOrEmpty(row.requires) || save.Rank(row.requires) >= 1);
        }

        public static bool IsNodeRevealed(PrototypeGrowthTable table, PrototypeGrowthNode node, PrototypeSave save, int stage)
        {
            if (table?.nodes == null || node == null || save == null) return false;
            if (IsNodeOpen(table, node, save, stage) || node.parent == "ROOT") return true;
            PrototypeGrowthNode parent = Array.Find(table.nodes, n => n.id == node.parent);
            // Preview exactly one link ahead. Revealed-but-locked nodes do not extend visibility.
            return parent != null && IsNodeOpen(table, parent, save, stage);
        }

        public static bool TryPurchaseNode(PrototypeBalance balance, PrototypeGrowth row, PrototypeGrowthNode node,
            PrototypeSave save, int stage, ref int gold, PrototypeGrowthTable table = null)
        {
            if (NodePurchaseBlock(balance, row, node, save, stage, gold, table) != null) return false;
            return TryPurchase(balance, row, save, stage, ref gold);
        }

        public static int XpThreshold(PrototypeBalance balance, int level)
        {
            return (int)Math.Min(int.MaxValue, Math.Max(1, Math.Ceiling(
                balance.xpFirst * Math.Pow(balance.xpGrowth, Math.Max(0, level - 1)))));
        }

        public static double Chance(double baseChance, double permanent, double runBonus)
        { return Math.Min(1, Math.Max(0, baseChance + permanent + runBonus)); }

        // Elapsed time never changes when remaining time is replenished. This is the boss trigger.
        public static bool BossDue(double elapsedSeconds, double capSeconds)
        { return elapsedSeconds >= capSeconds; }

        public static double ExtendDeadline(double deadline, double seconds, double bossCap)
        { return Math.Min(bossCap, deadline + Math.Max(0, seconds)); }

        public static int StochasticCoins(double amount, double sample)
        {
            double clamped = Math.Min(int.MaxValue, Math.Max(0, amount));
            int whole = (int)Math.Floor(clamped);
            return whole < int.MaxValue && sample < clamped - whole ? whole + 1 : whole;
        }

        public static string Validate(PrototypeBalance b, PrototypeSkillTable skills, PrototypeGrowthTable growth)
        {
            if (b == null || skills == null || growth == null || skills.rows == null || growth.rows == null)
                return "테이블이 비어 있습니다.";
            if (!Positive(b.baseDamage) || !Positive(b.attackInterval) || !Positive(b.projectileSpeed) ||
                !Positive(b.projectileLifetime) || !Positive(b.moveSpeed) || !Positive(b.initialSeconds) ||
                !Positive(b.bossElapsedSeconds) || b.bossElapsedSeconds < b.initialSeconds ||
                !Positive(b.xpFirst) || !Positive(b.xpGrowth) || b.xpGrowth < 1 ||
                !Positive(b.latePriceScale) || b.latePriceScale < 1 || !FiniteNonnegative(b.firstDamageBonus) ||
                !Probability(b.timeRewardChance) || !FiniteNonnegative(b.timeRewardSeconds) ||
                !Probability(b.chestChance) || b.chestCoins < 0 || !Positive(b.playerHealth) ||
                !Positive(b.playerInvulnerability) || !Positive(b.playerContactRadius) ||
                !Probability(b.criticalChance) || !Positive(b.criticalMultiplier) || b.criticalMultiplier < 1 ||
                !FiniteNonnegative(b.initialSpawnMin) || b.initialSpawnMin < .6f ||
                !Positive(b.initialSpawnMax) || b.initialSpawnMax < b.initialSpawnMin ||
                !FiniteNonnegative(b.refillSpawnMin) || b.refillSpawnMin < .6f ||
                !Positive(b.refillSpawnMax) || b.refillSpawnMax < b.refillSpawnMin)
                return "기본 수치/시간/확률/스폰 범위를 확인하세요.";
            string[] monsterIds = { "slime", "skeleton", "goblin", "mushroom", "boar" };
            if (b.monsters == null || b.stages == null || b.monsters.Length != 5 || b.stages.Length != 5)
                return "몬스터/스폰 단계는 5개가 필요합니다.";
            for (int i = 0; i < 5; i++)
            {
                PrototypeMonster m = b.monsters[i];
                PrototypeStage s = b.stages[i];
                if (m == null || m.id != monsterIds[i] || !Positive(m.hp) || !Positive(m.speed) ||
                    !Positive(m.xp) || m.coins < 0 || m.bonusCoins < 0 || !Probability(m.bonusChance) || !FiniteNonnegative(m.contactDamage) ||
                    (i < 4 && m.nextUnlockKills < 1)) return "몬스터 순서/수치 오류: " + i;
                if (s == null || s.initial < 1 || s.initial > s.target || s.target > s.cap || s.cap > 1000 ||
                    s.batch < 1 || !Positive(s.refillSeconds) || s.weights == null || s.weights.Length != 5)
                    return "스폰 단계 수치 오류: " + i;
                long sum = 0;
                for (int j = 0; j < 5; j++)
                {
                    if (s.weights[j] < 0 || (j > i && s.weights[j] != 0)) return "미해금 몬스터/음수 스폰 가중치";
                    sum += s.weights[j];
                }
                if (sum < 1 || sum > int.MaxValue) return "스폰 가중치 합 오류";
            }
            string lootError = LootGradeRules.Validate(b);
            if (lootError != null) return lootError;
            string[] growthIds = { "damage", "speed", "time", "yield", "xp", "population", "respawn", "extension", "multishot", "chain", "splash", "meteor", "chainChance", "splashChance", "meteorChance", "currencyGradeChance", "experienceGradeChance", "silverDropChance", "goldDropChance", "emeraldDropChance", "amethystDropChance" };
            HashSet<string> ids = new HashSet<string>();
            foreach (PrototypeGrowth r in growth.rows)
            {
                if (r == null || Array.IndexOf(growthIds, r.id) < 0 || !ids.Add(r.id) || r.baseCost < 1 ||
                    !Positive(r.costGrowth) || r.costGrowth < 1 || r.maxRank < 1 || r.maxRank > 10000 ||
                    r.minStage < 0 || r.minStage > 4 || r.legacyMaxRank < 0 || r.legacyMaxRank > 10000 ||
                    (r.legacyMaxRank > 0 && r.legacyMaxRank < r.maxRank) || !FiniteNonnegative(r.costRampRanks) || !FiniteNonnegative(r.step) ||
                    !FiniteNonnegative(r.damageRatio) || float.IsNaN(r.x) || float.IsInfinity(r.x) ||
                    float.IsNaN(r.y) || float.IsInfinity(r.y)) return "성장 행 ID/수치 오류";
            }
            // Older custom tables may omit the two newly added optional grade growths.
            foreach (string id in growthIds)
                if (id != "currencyGradeChance" && id != "experienceGradeChance" && LootGradeRules.CurrencyGrowthGrade(id) == 0 && !ids.Contains(id)) return "필수 성장 행이 누락되었습니다.";
            foreach (PrototypeGrowth r in growth.rows)
            {
                if ((!string.IsNullOrEmpty(r.requires) && !ids.Contains(r.requires)) ||
                    (!string.IsNullOrEmpty(r.parent) && r.parent != "ROOT" && !ids.Contains(r.parent)))
                    return "존재하지 않는 성장 연결: " + r.id;
            }
            foreach (PrototypeGrowth r in growth.rows)
            {
                HashSet<string> visited = new HashSet<string> { r.id };
                string parent = r.parent;
                while (!string.IsNullOrEmpty(parent) && parent != "ROOT")
                {
                    if (!visited.Add(parent)) return "성장 그래프 연결 순환: " + r.id;
                    parent = Array.Find(growth.rows, p => p.id == parent).parent;
                }
                visited.Clear(); visited.Add(r.id);
                string prerequisite = r.requires;
                while (!string.IsNullOrEmpty(prerequisite))
                {
                    if (!visited.Add(prerequisite)) return "선행 성장 조건 순환: " + r.id;
                    prerequisite = Array.Find(growth.rows, p => p.id == prerequisite).requires;
                }
            }
            if (growth.nodes != null)
            {
                HashSet<string> nodeIds = new HashSet<string>();
                foreach (PrototypeGrowthNode node in growth.nodes)
                {
                    if (node == null || string.IsNullOrEmpty(node.id) || node.id == "ROOT" || !nodeIds.Add(node.id) ||
                        !ids.Contains(node.growthId) || node.startRank < 0 || node.ranks < 1 || node.ranks > 3 || node.parentRanks < 0 ||
                        float.IsNaN(node.x) || float.IsInfinity(node.x) || float.IsNaN(node.y) || float.IsInfinity(node.y))
                        return "성장 분할 노드 ID/범위/좌표 오류";
                }
                foreach (PrototypeGrowthNode node in growth.nodes)
                    if (!string.IsNullOrEmpty(node.parent) && node.parent != "ROOT" && !nodeIds.Contains(node.parent))
                        return "존재하지 않는 분할 노드 연결: " + node.id;
                foreach (PrototypeGrowthNode node in growth.nodes)
                    if (node.parentRanks > 0 && (string.IsNullOrEmpty(node.parent) || node.parent == "ROOT" ||
                        node.parentRanks > Array.Find(growth.nodes, n => n.id == node.parent).ranks))
                        return "선행 노드 구매 단계 범위 오류: " + node.id;
                foreach (PrototypeGrowthNode node in growth.nodes)
                {
                    HashSet<string> visited = new HashSet<string> { node.id };
                    string parent = node.parent;
                    while (!string.IsNullOrEmpty(parent) && parent != "ROOT")
                    {
                        if (!visited.Add(parent)) return "분할 노드 연결 순환: " + node.id;
                        parent = Array.Find(growth.nodes, n => n.id == parent).parent;
                    }
                }
                foreach (PrototypeGrowth row in growth.rows)
                {
                    PrototypeGrowthNode[] group = Array.FindAll(growth.nodes, n => n.growthId == row.id);
                    Array.Sort(group, (a, z) => a.startRank.CompareTo(z.startRank));
                    long end = 0;
                    foreach (PrototypeGrowthNode node in group)
                    {
                        if (node.startRank != end) return "분할 노드 구매 단계의 누락/중복: " + row.id;
                        end += node.ranks;
                    }
                    if (end != row.maxRank) return "분할 단계 합계와 최대 성장 단계 불일치: " + row.id;
                }
                HashSet<string> visiting = new HashSet<string>(), completed = new HashSet<string>();
                foreach (PrototypeGrowthNode node in growth.nodes)
                    if (HasPurchaseCycle(node, growth.nodes, visiting, completed))
                        return "분할 노드 구매 조건 순환: " + node.id;
            }
            string goalError = ValidateGoalLine(b, growth);
            if (goalError != null) return goalError;
            string[] skillIds = { "damage", "speed", "proc", "chain", "splash", "meteor" };
            ids.Clear();
            foreach (PrototypeSkill r in skills.rows)
            {
                if (r == null || Array.IndexOf(skillIds, r.id) < 0 || !ids.Add(r.id) || r.maxRank < 1 ||
                    !Probability(r.chance) || !FiniteNonnegative(r.step) || !FiniteNonnegative(r.damageRatio) ||
                    !FiniteNonnegative(r.radius) || !FiniteNonnegative(r.range) || !FiniteNonnegative(r.delay) ||
                    !FiniteNonnegative(r.rankChanceStep) || !FiniteNonnegative(r.rankDamageStep) ||
                    (!string.IsNullOrEmpty(r.unlock) && Array.IndexOf(growthIds, r.unlock) < 0))
                    return "스킬 행 ID/확률/수치 오류";
                if ((r.id == "chain" && (r.targets < 1 || !Positive(r.range) || !Positive(r.damageRatio))) ||
                    ((r.id == "splash" || r.id == "meteor") && (!Positive(r.radius) || !Positive(r.damageRatio))))
                    return "공격 효과 범위/대상 수 오류: " + r.id;
            }
            return ids.Count == skillIds.Length ? null : "필수 스킬 행이 누락되었습니다.";
        }

        private static bool HasPurchaseCycle(PrototypeGrowthNode node, PrototypeGrowthNode[] nodes,
            HashSet<string> visiting, HashSet<string> completed)
        {
            if (completed.Contains(node.id)) return false;
            if (!visiting.Add(node.id)) return true;
            PrototypeGrowthNode parent = node.parentRanks > 0 ? Array.Find(nodes, n => n.id == node.parent) : null;
            PrototypeGrowthNode previous = node.startRank > 0
                ? Array.Find(nodes, n => n.growthId == node.growthId && n.startRank + n.ranks == node.startRank) : null;
            if ((parent != null && HasPurchaseCycle(parent, nodes, visiting, completed)) ||
                (previous != null && HasPurchaseCycle(previous, nodes, visiting, completed))) return true;
            visiting.Remove(node.id); completed.Add(node.id);
            return false;
        }

        private static bool Positive(float n) { return n > 0 && !float.IsNaN(n) && !float.IsInfinity(n); }
        private static bool FiniteNonnegative(float n) { return n >= 0 && !float.IsNaN(n) && !float.IsInfinity(n); }
        private static bool Probability(float n) { return FiniteNonnegative(n) && n <= 1; }
    }
}
