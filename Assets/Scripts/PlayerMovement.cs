using UnityEngine;

namespace ProjectDM
{
    public sealed class PlayerMovement : MonoBehaviour
    {
        public Vector2 ReadAndMove(float deltaTime, float speed)
        {
            Vector2 input = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            transform.position += (Vector3)(input * speed * deltaTime);
            transform.position = new Vector3(
                Mathf.Clamp(transform.position.x, -8.4f, 8.4f),
                Mathf.Clamp(transform.position.y, -4.8f, 4.8f),
                0f);

            return input;
        }
    }
}
