using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Computes attack damage and interval from tables, permanent growth and temporary skills.</summary>
    internal sealed class CombatStats
    {
        private readonly GameTables tables;
        private readonly ProgressionService progression;
        private readonly SkillSystem skills;

        internal CombatStats(GameTables tables, ProgressionService progression, SkillSystem skills)
        {
            this.tables = tables;
            this.progression = progression;
            this.skills = skills;
        }

        internal float MainDamage => tables.balance.baseDamage * (1f + progression.PermanentAmount("damage")) *
            (1f + skills.RunRank("damage") * tables.SkillRow("damage").step);

        internal float AttackInterval => tables.balance.attackInterval /
            ((1f + progression.PermanentAmount("speed")) * (1f + skills.RunRank("speed") * tables.SkillRow("speed").step));
    }
}
