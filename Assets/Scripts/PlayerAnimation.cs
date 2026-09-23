using UnityEngine;

namespace ProjectDM
{
    [DisallowMultipleComponent]
    public sealed class PlayerAnimation : MonoBehaviour
    {
        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private Transform visualTransform;
        private string currentState;
        private string facing = "Down";
        private Vector3 restingScale = Vector3.one;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                visualTransform = animator.transform;
                restingScale = visualTransform.localScale;
            }
        }

        public void Initialize(Sprite sprite, RuntimeAnimatorController controller)
        {
            spriteRenderer.sprite = sprite;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            currentState = null;
            PlayState(false);
        }

        public void UpdateFacing(Vector2 input)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            bool isMoving = input.sqrMagnitude > 0.01f;
            if (isMoving)
            {
                facing = Mathf.Abs(input.x) >= Mathf.Abs(input.y)
                    ? input.x < 0f ? "Left" : "Right"
                    : input.y < 0f ? "Down" : "Up";
            }

            PlayState(isMoving);
            UpdateBreathing(isMoving);
        }

        private void PlayState(bool isMoving)
        {
            // The player always settles into the same front-facing breathing pose at rest.
            // Facing direction is only meaningful while walking.
            string nextState = isMoving ? $"Walk_{facing}" : "Idle_Down";
            if (nextState == currentState)
            {
                return;
            }

            currentState = nextState;
            animator.Play(nextState);
        }

        private void UpdateBreathing(bool isMoving)
        {
            if (visualTransform == null)
            {
                return;
            }

            if (isMoving)
            {
                visualTransform.localScale = restingScale;
                return;
            }

            // The animator is attached to the Visual child, not the player root.
            // This gives a gentle breath without changing gameplay coordinates.
            float breath = Mathf.Sin(Time.time * 2.2f) * 0.0125f;
            visualTransform.localScale = new Vector3(
                restingScale.x * (1f - breath * .25f),
                restingScale.y * (1f + breath),
                restingScale.z);
        }
    }
}
