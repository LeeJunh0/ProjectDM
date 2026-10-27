using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Transient purchase-only reveal state, independent of saved progression and timeScale.</summary>
    internal sealed class GrowthNodeReveal
    {
        internal const float LineDuration = .42f, NodeDelay = .34f, NodeDuration = .4f;
        private readonly Dictionary<string, float> starts = new();

        internal void Begin(string id, float now) => starts[id] = now;
        internal void Clear() => starts.Clear();
        internal bool IsAnimating(string id, float now) => starts.TryGetValue(id, out float start) && now - start < NodeDelay + NodeDuration;
        internal float Line(string id, float now) => starts.TryGetValue(id, out float start)
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - start) / LineDuration)) : 1f;
        internal float Opacity(string id, float now) => starts.TryGetValue(id, out float start)
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - start - NodeDelay) / .2f)) : 1f;
        internal float Scale(string id, float now)
        {
            if (!starts.TryGetValue(id, out float start)) return 1f;
            float age = now - start - NodeDelay;
            if (age < 0f || age >= NodeDuration) return 1f;
            return CombatDamageScaleAnimation.Evaluate(age, NodeDuration, .55f, 1.2f, 1f, .13f, .025f, .245f, 1f);
        }
    }
}
