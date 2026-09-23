using UnityEngine;

namespace ProjectDM
{
    public sealed class PlayerMovement : MonoBehaviour
    {
        private Vector2 movementBounds = new(8.4f, 4.8f);
        private Vector2 movementBoundsCenter;

        public void SetMovementBounds(Vector2 bounds, Vector2 center)
        {
            movementBounds = new Vector2(Mathf.Max(0f, bounds.x), Mathf.Max(0f, bounds.y));
            movementBoundsCenter = center;
        }

        public Vector2 ReadAndMove(float deltaTime, float speed)
        {
            Vector2 input = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            transform.position += (Vector3)(input * speed * deltaTime);
            transform.position = new Vector3(
                Mathf.Clamp(transform.position.x, movementBoundsCenter.x - movementBounds.x, movementBoundsCenter.x + movementBounds.x),
                Mathf.Clamp(transform.position.y, movementBoundsCenter.y - movementBounds.y, movementBoundsCenter.y + movementBounds.y),
                0f);

            return input;
        }
    }
}
