using System;
using System.Globalization;

namespace ProjectDM
{
    /// <summary>Full grouped numbers below one million, then M/B/T. Display only, without upward rounding.</summary>
    internal static class CompactNumber
    {
        private static readonly string[] Suffixes = { "", "M", "B", "T" };

        internal static string Format(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "—";
            double amount = Math.Abs(value);
            int unit = 0;
            if (amount >= 1000000)
            {
                amount /= 1000000; unit = 1;
                while (amount >= 1000 && unit < Suffixes.Length - 1) { amount /= 1000; unit++; }
            }
            // Runtime currency/requirements are int and XP is float; also handle larger diagnostic inputs.
            if (amount > (double)(decimal.MaxValue / 100)) return value.ToString("0.##E+0", CultureInfo.InvariantCulture);
            decimal visible = decimal.Truncate((decimal)amount * 100) / 100;
            return (value < 0 && visible != 0 ? "-" : "") + visible.ToString(unit == 0 ? "#,0.##" : "0.##", CultureInfo.InvariantCulture) + Suffixes[unit];
        }
    }
}
