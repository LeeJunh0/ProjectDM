using System;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Unscaled fade to black, screen swap, then fade back in.</summary>
    internal sealed class ScreenTransition
    {
        internal const float HalfDuration = .22f;
        private Action swap;
        private float elapsed;
        private bool swapped;
        internal bool Active { get; private set; }
        internal float Opacity => !Active ? 0f : Mathf.SmoothStep(0f, 1f,
            swapped ? 1f - Mathf.Clamp01((elapsed - HalfDuration) / HalfDuration) : Mathf.Clamp01(elapsed / HalfDuration));

        internal bool Begin(Action screenSwap)
        {
            if (Active || screenSwap == null) return false;
            swap = screenSwap;
            elapsed = 0f;
            swapped = false;
            Active = true;
            return true;
        }

        internal void Tick(float unscaledDeltaTime)
        {
            if (!Active || unscaledDeltaTime <= 0) return;
            // A stalled frame must not skip the black midpoint or both halves of the fade.
            elapsed += Mathf.Min(unscaledDeltaTime, .05f);
            if (!swapped && elapsed >= HalfDuration)
            {
                elapsed = HalfDuration;
                swapped = true;
                Action change = swap;
                swap = null;
                change();
                return;
            }
            if (swapped && elapsed >= HalfDuration * 2f) Active = false;
        }
    }
}
