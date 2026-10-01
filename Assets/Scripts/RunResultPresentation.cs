using System;

namespace ProjectDM
{
    /// <summary>Display-only count-up math. Never changes earned rewards or kill counters.</summary>
    internal static class RunResultPresentation
    {
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
