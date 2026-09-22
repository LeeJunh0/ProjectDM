using UnityEngine;

namespace ProjectDM
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class PlayerAnimation : MonoBehaviour
    {
        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private string currentState;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Initialize(Sprite sprite, RuntimeAnimatorController controller)
        {
            spriteRenderer.sprite = sprite;
            animator.runtimeAnimatorController = controller;
        }

        public void UpdateFacing(Vector2 input)
        {
            if (input.sqrMagnitude <= 0.01f || animator.runtimeAnimatorController == null)
            {
                return;
            }

            string nextState = Mathf.Abs(input.x) >= Mathf.Abs(input.y)
                ? input.x < 0f ? "Walk_Left" : "Walk_Right"
                : input.y < 0f ? "Walk_Down" : "Walk_Up";

            if (nextState == currentState)
            {
                return;
            }

            currentState = nextState;
            animator.Play(nextState);
        }
    }
}
