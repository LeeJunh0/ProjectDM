using UnityEngine;

namespace ProjectDM
{
    /// <summary>Animates only a pickup visual child, leaving its root free for pickup state and movement.</summary>
    [DisallowMultipleComponent]
    public sealed class PickupFloatVisual : MonoBehaviour
    {
        private Vector3 restingLocalPosition;
        private float yAmplitude = .06f;
        private float loopDuration = 1.2f;
        private float phase;
        private AnimationCurve yCurve;
        private bool hasRestingPosition;

        private void OnEnable()
        {
            restingLocalPosition = transform.localPosition;
            hasRestingPosition = true;
        }

        private void OnDisable()
        {
            if (hasRestingPosition)
            {
                transform.localPosition = restingLocalPosition;
            }
        }

        /// <summary>Configures the visual-only float loop for the current pooled pickup instance.</summary>
        public void Configure(AnimationCurve curve, float amplitude, float duration, float startPhase)
        {
            if (!hasRestingPosition)
            {
                restingLocalPosition = transform.localPosition;
                hasRestingPosition = true;
            }

            yAmplitude = Mathf.Max(0f, amplitude);
            loopDuration = Mathf.Max(.05f, duration);
            phase = Mathf.Repeat(startPhase, 1f);
            yCurve = curve;
        }

        private void LateUpdate()
        {
            float normalizedTime = Mathf.Repeat(Time.time / loopDuration + phase, 1f);
            float yOffset = yCurve != null ? yCurve.Evaluate(normalizedTime) * yAmplitude : 0f;
            transform.localPosition = restingLocalPosition + Vector3.up * yOffset;
        }
    }
}
