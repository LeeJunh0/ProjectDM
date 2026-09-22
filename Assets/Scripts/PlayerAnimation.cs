using UnityEngine;

namespace ProjectDM
{
    [DisallowMultipleComponent]
    public sealed class PlayerAnimation : MonoBehaviour
    {
        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private string currentState;
        private string facing = "Down";

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
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
        }

        private void PlayState(bool isMoving)
        {
            string nextState = $"{(isMoving ? "Walk" : "Idle")}_{facing}";
            if (nextState == currentState)
            {
                return;
            }

            currentState = nextState;
            animator.Play(nextState);
        }
    }
}
