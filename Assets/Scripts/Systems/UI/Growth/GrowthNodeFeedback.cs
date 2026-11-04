using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    internal sealed class GrowthNodeFeedback
    {
        private sealed class State
        {
            internal float scale = 1, updatedAt, purchasedAt = float.NegativeInfinity;
        }
        private readonly Dictionary<string, State> nodes = new();

        private State Get(string id, float now)
        {
            if (!nodes.TryGetValue(id, out var state)) nodes.Add(id, state = new State { updatedAt = now });
            return state;
        }

        internal float Scale(string id, bool hovered, bool holding, float progress, float now)
        {
            var state = Get(id, now);
            float target = holding ? Mathf.Lerp(1.09f, 1.14f, Mathf.Clamp01(progress)) : hovered ? 1.06f : 1;
            state.scale = Mathf.Lerp(state.scale, target, 1 - Mathf.Exp(-18 * Mathf.Max(0, now - state.updatedAt)));
            state.updatedAt = now;
            return state.scale;
        }

        internal void Purchased(string id, float now) => Get(id, now).purchasedAt = now;
        internal float PurchaseAge(string id, float now) => nodes.TryGetValue(id, out var state) ? now - state.purchasedAt : float.PositiveInfinity;
        internal void Clear() => nodes.Clear();
    }
}
