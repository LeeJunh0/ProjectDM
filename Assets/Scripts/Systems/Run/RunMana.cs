using System;

namespace ProjectDM
{
    /// <summary>Run-scoped resource. Mana capacity is independent of the boss's elapsed-time limit.</summary>
    internal sealed class RunMana
    {
        internal RunMana() { }
        internal float Maximum { get; private set; } = 40;
        internal float Current { get; private set; } = 40;
        internal float BasicAttackCost { get; private set; } = 1;
        internal float AbsorbedThisRun { get; private set; }
        internal float SpentThisRun { get; private set; }
        internal float Fraction => Current / Maximum;
        internal bool IsDepleted => Current <= 0;

        internal void Reset(float maximum, float basicAttackCost)
        {
            if (float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= 0 ||
                float.IsNaN(basicAttackCost) || float.IsInfinity(basicAttackCost) || basicAttackCost <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximum), "마나 최대값과 기본 공격 소모량은 양수여야 합니다.");
            Maximum = Current = maximum;
            BasicAttackCost = basicAttackCost;
            AbsorbedThisRun = SpentThisRun = 0;
        }

        internal void Consume(float amount)
        {
            if (amount <= 0) return;
            float previous = Current;
            Current = Math.Max(0, Current - amount);
            SpentThisRun += previous - Current;
        }

        internal bool SpendForBasicAttack()
        {
            if (IsDepleted) return false;
            if (Current < BasicAttackCost) { Consume(Current); return false; }
            Consume(BasicAttackCost);
            return true;
        }

        internal float Absorb(float amount)
        {
            if (amount <= 0 || IsDepleted) return 0; // A depleted run cannot be revived by a late hit.
            float previous = Current;
            Current = Math.Min(Maximum, Current + amount);
            float added = Current - previous;
            AbsorbedThisRun += added;
            return added;
        }
    }
}
