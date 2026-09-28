using UnityEngine;

namespace ProjectDM
{
    /// <summary>Plays a one-shot transform motion on a pickup visual without moving its root.</summary>
    [DisallowMultipleComponent]
    public sealed class PickupInteractionVisual : MonoBehaviour
    {
        private Vector3 restingLocalPosition;
        private Vector3 restingLocalScale;
        private AnimationCurve yCurve;
        private AnimationCurve scaleCurve;
        private float duration;
        private float elapsed;
        private bool isPlaying;

        private void OnEnable()
        {
            restingLocalPosition = transform.localPosition;
            restingLocalScale = transform.localScale;
            isPlaying = false;
        }

        private void OnDisable()
        {
            ResetVisual();
        }

        /// <summary>Starts a one-shot local Y and scale motion from the authored resting transform.</summary>
        public void Play(AnimationCurve positionYCurve, AnimationCurve localScaleCurve, float animationDuration)
        {
            restingLocalPosition = transform.localPosition;
            restingLocalScale = transform.localScale;
            yCurve = positionYCurve;
            scaleCurve = localScaleCurve;
            duration = Mathf.Max(.05f, animationDuration);
            elapsed = 0f;
            isPlaying = true;
        }

        private void LateUpdate()
        {
            if (!isPlaying)
            {
                return;
            }

            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float yOffset = yCurve != null ? yCurve.Evaluate(normalizedTime) : 0f;
            float scaleMultiplier = scaleCurve != null ? scaleCurve.Evaluate(normalizedTime) : 1f;
            transform.localPosition = restingLocalPosition + Vector3.up * yOffset;
            transform.localScale = restingLocalScale * scaleMultiplier;
            if (normalizedTime >= 1f)
            {
                isPlaying = false;
                ResetVisual();
            }
        }

        private void ResetVisual()
        {
            transform.localPosition = restingLocalPosition;
            transform.localScale = restingLocalScale;
        }
    }
}
