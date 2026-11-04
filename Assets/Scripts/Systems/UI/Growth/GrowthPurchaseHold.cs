using System;

namespace ProjectDM
{
    internal sealed class GrowthPurchaseHold
    {
        internal const float Duration = 1f;
        internal string NodeId { get; private set; }
        private float startedAt;
        private bool completed;
        internal bool IsHolding => NodeId != null && !completed;

        internal float Progress(float now) => NodeId == null ? 0 : Math.Min(1, Math.Max(0, (now - startedAt) / Duration));

        // A new press is required after completion or cancellation, even if the pointer returns.
        internal bool Update(string hoveredId, bool canPurchase, bool pressed, bool held, float now)
        {
            if (!held || !canPurchase || hoveredId == null) { Clear(); return false; }
            if (pressed) { NodeId = hoveredId; startedAt = now; completed = false; }
            if (NodeId != hoveredId) { Clear(); return false; }
            if (completed || Progress(now) < 1) return false;
            completed = true;
            return true;
        }

        internal void Clear() { NodeId = null; completed = false; }
    }
}
