using UnityEngine;

namespace ProjectDM
{
    [RequireComponent(typeof(PlayerMovement), typeof(PlayerAnimation))]
    public sealed class Player : MonoBehaviour
    {
        private PlayerMovement movement;
        private PlayerAnimation animationController;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            animationController = GetComponent<PlayerAnimation>();
        }

        public void Initialize(Sprite sprite, RuntimeAnimatorController controller)
        {
            animationController.Initialize(sprite, controller);
        }

        public void SetMovementBounds(Vector2 bounds, Vector2 center)
        {
            movement.SetMovementBounds(bounds, center);
        }

        public void Tick(float deltaTime, float speed)
        {
            Vector2 input = movement.ReadAndMove(deltaTime, speed);
            animationController.UpdateFacing(input);
        }
    }
}
