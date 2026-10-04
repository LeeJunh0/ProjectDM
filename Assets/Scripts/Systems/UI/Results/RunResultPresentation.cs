using System;

namespace ProjectDM
{
    /// <summary>Display-only count-up math. Never changes earned rewards or kill counters.</summary>
    internal static class RunResultPresentation
    {
        internal readonly struct PermanentKillProgress
        {
            internal readonly int before, total, runKills, target;
            internal readonly bool available;
            internal bool HasTarget => target > 0;
            internal bool NewlyUnlocked => available && HasTarget && before < target && total >= target;
            internal PermanentKillProgress(int before, int total, int runKills, int target, bool available)
            {
                this.total = Math.Max(0, total);
                this.before = Math.Min(this.total, Math.Max(0, before));
                this.runKills = Math.Max(0, runKills);
                this.target = Math.Max(0, target);
                this.available = available;
            }
        }

        internal static PermanentKillProgress PermanentKills(int before, int total, int runKills, int target, bool available)
            => new PermanentKillProgress(before, total, runKills, target, available);

        internal static int AnimatedPermanentKills(PermanentKillProgress progress, double elapsed, double duration, double delay)
            => duration <= 0 ? progress.total : progress.before + CountUp(progress.total - progress.before, elapsed, duration, delay);

        internal static double PermanentKillFraction(PermanentKillProgress progress, int count)
            => progress.available && progress.HasTarget ? Math.Min(1, Math.Max(0, (double)count / progress.target)) : 0;

        internal static int CountUp(int target, double elapsed, double duration, double delay)
        {
            target = Math.Max(0, target);
            double localTime = elapsed - Math.Max(0d, delay);
            if (target == 0 || localTime <= 0d) return 0;
            if (duration <= 0d || localTime >= duration) return target;

            double progress = localTime / duration;
            double remaining = 1d - progress;
            double eased = 1d - remaining * remaining * remaining;
            // Floor preserves the zero start, and the explicit completion branch guarantees the exact target.
            return (int)Math.Min(target, Math.Floor(target * eased));
        }
    }
}
