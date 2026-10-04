using System;

namespace ProjectDM
{
    // Pure timing math shared by all damage styles; independent of rendering and combat RNG.
    internal static class CombatDamageScaleAnimation
    {
        internal static float Evaluate(float age, float lifetime, float startScale, float peakScale, float endScale,
            float growSeconds, float holdSeconds, float shrinkSeconds, float speed)
        {
            startScale = Math.Max(.05f, startScale);
            peakScale = Math.Max(.05f, peakScale);
            endScale = Math.Max(.05f, endScale);
            if (age <= 0) return startScale;

            double rate = Math.Max(.01f, speed);
            double grow = Math.Max(0, growSeconds) / rate;
            double hold = Math.Max(0, holdSeconds) / rate;
            double shrink = Math.Max(0, shrinkSeconds) / rate;
            double total = grow + hold + shrink;
            double available = Math.Max(.05f, lifetime);
            // Never cut the shrink phase off when the label's lifetime is shortened.
            if (total > available)
            {
                double fit = available / total;
                grow *= fit; hold *= fit; shrink *= fit;
            }

            double time = age;
            if (grow > 0 && time < grow)
            {
                double t = time / grow;
                double eased = 1 - Math.Pow(1 - t, 3);
                return (float)(startScale + (peakScale - startScale) * eased);
            }
            time -= grow;
            if (time < hold) return peakScale;
            time -= hold;
            if (shrink <= 0 || time >= shrink) return endScale;
            double shrinkProgress = Math.Max(0, time / shrink);
            return (float)(peakScale + (endScale - peakScale) * shrinkProgress * shrinkProgress);
        }
    }
}
