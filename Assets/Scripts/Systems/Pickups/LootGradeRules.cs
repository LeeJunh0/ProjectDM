using System;

namespace ProjectDM
{
    [System.Serializable]
    public sealed class PrototypeLootGrade
    {
        public int grade, unlockStage, prefabIndex, currencyAmount;
        public float experienceAmount;
        public string currencyName, experienceName;
    }

    /// <summary>Currency: one categorical draw. XP: the existing one-step promotion.</summary>
    public static class LootGradeRules
    {
        public static string CurrencyGrowthId(int grade) => grade == 2 ? "silverDropChance" : grade == 3 ? "goldDropChance" :
            grade == 4 ? "emeraldDropChance" : grade == 5 ? "amethystDropChance" : null;

        public static int CurrencyGrowthGrade(string id) => id == "silverDropChance" ? 2 : id == "goldDropChance" ? 3 :
            id == "emeraldDropChance" ? 4 : id == "amethystDropChance" ? 5 : 0;

        public static double[] CurrencyProbabilities(PrototypeBalance balance, PrototypeMonster monster, int stage, double[] bonuses)
        {
            int count = balance.lootGrades == null ? 1 : balance.lootGrades.Length;
            double[] chances = new double[count];
            int ceiling = HighestUnlocked(balance, stage);
            if (monster.currencyChances == null)
                chances[Math.Max(1, Math.Min(monster.currencyGrade, ceiling)) - 1] = 1;
            else
            {
                double total = 0;
                for (int i = 0; i < count && i < ceiling; i++) total += monster.currencyChances[i];
                if (total > 0) for (int i = 0; i < count && i < ceiling; i++) chances[i] = monster.currencyChances[i] / total;
                else chances[0] = 1; // Validation requires an available base outcome; defensive fallback.
            }
            // Higher-tier bonuses consume only lower-tier probability, never higher-tier probability.
            // Apply high -> low deterministically, capped by the available lower-tier pool.
            for (int i = Math.Min(count, ceiling) - 1; i >= 1; i--)
            {
                double requested = bonuses != null && i < bonuses.Length ? PrototypeRules.Chance(0, bonuses[i], 0) : 0;
                double available = 0;
                for (int lower = 0; lower < i; lower++) available += chances[lower];
                double transfer = Math.Min(requested, available);
                if (transfer <= 0) continue;
                for (int lower = 0; lower < i; lower++) chances[lower] *= (available - transfer) / available;
                chances[i] += transfer;
            }
            return chances;
        }

        public static int ResolveCurrencyGrade(PrototypeBalance balance, PrototypeMonster monster, int stage, double[] bonuses, double sample)
        {
            double[] chances = CurrencyProbabilities(balance, monster, stage, bonuses);
            double cumulative = 0;
            int last = 1;
            for (int i = 0; i < chances.Length; i++)
            {
                if (chances[i] <= 0) continue;
                last = i + 1; cumulative += chances[i];
                if (sample < cumulative) return last;
            }
            return last; // Random.value may equal 1; never choose an impossible/locked outcome.
        }
        public static int HighestUnlocked(PrototypeBalance balance, int stage)
        {
            int highest = 1;
            if (balance.lootGrades != null) foreach (PrototypeLootGrade row in balance.lootGrades)
                if (row.unlockStage <= stage) highest = System.Math.Max(highest, row.grade);
            return highest;
        }

        public static int ResolveGrade(PrototypeBalance balance, int baseGrade, int stage, double chance, double sample)
        {
            if (balance.lootGrades == null) return 1; // Legacy tables retain their own reward values.
            int ceiling = HighestUnlocked(balance, stage);
            int grade = System.Math.Max(1, System.Math.Min(baseGrade, ceiling));
            return grade < ceiling && sample < PrototypeRules.Chance(0, chance, 0) ? grade + 1 : grade;
        }

        public static double BaseAmount(PrototypeBalance balance, PrototypeMonster monster, int grade, bool currency)
        {
            if (balance.lootGrades == null) return currency ? monster.coins : monster.xp;
            PrototypeLootGrade row = balance.lootGrades[grade - 1];
            return currency ? row.currencyAmount : row.experienceAmount;
        }

        public static double Reward(PrototypeBalance balance, PrototypeMonster monster, int grade, bool currency,
            double quantityBonus, bool bonusCoin, double roundingSample)
        {
            double amount = BaseAmount(balance, monster, grade, currency);
            if (currency && bonusCoin) amount += monster.bonusCoins;
            amount *= 1 + System.Math.Max(0, quantityBonus);
            return currency ? PrototypeRules.StochasticCoins(amount, roundingSample) : System.Math.Min(float.MaxValue, amount);
        }

        public static int PrefabIndex(PrototypeBalance balance, int grade) => balance.lootGrades == null ? 0 : balance.lootGrades[grade - 1].prefabIndex;

        public static string Name(PrototypeBalance balance, int grade, bool currency) => balance.lootGrades == null
            ? (currency ? "재화" : "경험치") : currency ? balance.lootGrades[grade - 1].currencyName : balance.lootGrades[grade - 1].experienceName;

        public static string Validate(PrototypeBalance balance)
        {
            if (balance.lootGrades == null) return null;
            if (balance.lootGrades.Length < 1 || balance.lootGrades.Length > 5) return "드롭 등급은 기존 리소스 범위인 1~5개가 필요합니다.";
            int lastCurrency = 0, lastStage = -1;
            System.Collections.Generic.HashSet<int> slots = new System.Collections.Generic.HashSet<int>();
            float lastExperience = 0;
            for (int i = 0; i < balance.lootGrades.Length; i++)
            {
                PrototypeLootGrade row = balance.lootGrades[i];
                if (row == null || row.grade != i + 1 || row.unlockStage < 0 || row.unlockStage > 4 || row.unlockStage < lastStage ||
                    (i == 0 && row.unlockStage != 0) || row.prefabIndex < 0 || row.prefabIndex >= 5 || !slots.Add(row.prefabIndex) ||
                    row.currencyAmount <= lastCurrency || row.experienceAmount <= lastExperience ||
                    float.IsNaN(row.experienceAmount) || float.IsInfinity(row.experienceAmount) ||
                    string.IsNullOrEmpty(row.currencyName) || string.IsNullOrEmpty(row.experienceName)) return "드롭 등급 순서/금액/해금/리소스 설정 오류: " + (i + 1);
                lastCurrency = row.currencyAmount; lastExperience = row.experienceAmount; lastStage = row.unlockStage;
            }
            for (int i = 0; i < balance.monsters.Length; i++)
            {
                PrototypeMonster monster = balance.monsters[i];
                if (monster.currencyChances != null)
                {
                    if (monster.currencyChances.Length != balance.lootGrades.Length) return "몬스터 재화 확률은 등급 수와 같아야 합니다: " + monster.id;
                    double total = 0, available = 0;
                    for (int slot = 0; slot < monster.currencyChances.Length; slot++)
                    {
                        float chance = monster.currencyChances[slot];
                        if (float.IsNaN(chance) || float.IsInfinity(chance) || chance < 0 || chance > 1) return "몬스터 재화 확률 범위 오류: " + monster.id;
                        total += chance;
                        if (balance.lootGrades[slot].unlockStage <= i) available += chance;
                    }
                    if (Math.Abs(total - 1) > .00001 || available <= 0) return "몬스터 재화 확률 합은 1이며 등장 단계에서 가능한 재화가 있어야 합니다: " + monster.id;
                }
                foreach (int grade in new[] { monster.currencyGrade, monster.experienceGrade })
                    if (grade < 1 || grade > balance.lootGrades.Length || balance.lootGrades[grade - 1].unlockStage > i)
                        return "몬스터 기본 드롭 등급이 해당 몬스터 해금 범위를 벗어납니다: " + monster.id;
            }
            return null;
        }
    }
}
