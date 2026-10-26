using System;

namespace ProjectDM
{
    /// <summary>Choice-grade probabilities. Luck moves probability from bronze to silver and gold.</summary>
    internal static class UpgradeGradeRules
    {
        internal readonly struct Chances
        {
            internal readonly double gold, silver, bronze;
            internal Chances(double gold, double silver, double bronze)
            { this.gold = gold; this.silver = silver; this.bronze = bronze; }
        }

        // One luck point moves 0.2 percentage points to each higher grade; stop when bronze reaches zero.
        internal static Chances ForLuck(double luck)
        {
            double effective = double.IsNaN(luck) ? 0 : Math.Max(0, Math.Min(150, luck));
            return new Chances(.05 + effective * .002, .35 + effective * .002, Math.Max(0, .60 - effective * .004));
        }

        internal static string Roll(double luck, double sample)
        {
            Chances chances = ForLuck(luck);
            if (sample < chances.gold) return "gold";
            if (sample < chances.gold + chances.silver || chances.bronze <= 0) return "silver";
            return "bronze";
        }

        internal static int RankGain(string grade) => grade == "gold" ? 3 : grade == "silver" ? 2 : 1;
    }
}
