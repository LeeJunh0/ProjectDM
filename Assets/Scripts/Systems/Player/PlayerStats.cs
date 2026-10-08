using UnityEngine;

namespace ProjectDM
{
    /// <summary>Player attributes from the immutable-for-this-run permanent growth snapshot.</summary>
    internal sealed class PlayerStats
    {
        private readonly GameTables tables;
        private readonly ProgressionService progression;

        internal PlayerStats(GameTables tables, ProgressionService progression)
        {
            this.tables = tables;
            this.progression = progression;
        }

        internal float MaxHealth => Value("health");
        internal float MoveSpeed => Value("movement");
        internal float CriticalChance => Value("criticalChance");
        // 2.75 means 275% of normal damage, not +275% bonus damage.
        internal float CriticalDamage => Value("criticalDamage");
        internal float Invulnerability => Value("invulnerability");
        internal float MagnetRadius => Value("magnet");

        private float Value(string id) => Calculate(tables.balance, id, progression.PermanentAmount(id));

        // Shared with growth previews so current/next labels match actual gameplay formulas.
        internal static float Calculate(PrototypeBalance balance, string id, float amount) => id switch
        {
            "health" => balance.playerHealth + amount,
            "movement" => balance.moveSpeed * (1 + amount),
            "criticalChance" => Mathf.Clamp01(balance.criticalChance + amount),
            "criticalDamage" => balance.criticalMultiplier + amount,
            "invulnerability" => balance.playerInvulnerability + amount,
            "magnet" => balance.pickupMagnetRadius + amount,
            _ => throw new System.ArgumentOutOfRangeException(nameof(id), id, "Unknown player attribute")
        };
    }
}
