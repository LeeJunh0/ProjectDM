using UnityEngine;

namespace ProjectDM
{
    /// <summary>Editor-visible rectangle for the area in which the player can move.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMovementAreaPreview : MonoBehaviour
    {
        [SerializeField] private Vector2 halfExtents = new(8.4f, 4.8f);

        public Vector2 HalfExtents => halfExtents;

        private void OnValidate()
        {
            halfExtents = new Vector2(Mathf.Max(0f, halfExtents.x), Mathf.Max(0f, halfExtents.y));
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.22f, 1f, 0.46f, 0.95f);
            Gizmos.DrawWireCube(transform.position, new Vector3(halfExtents.x * 2f, halfExtents.y * 2f, 0.05f));

            Gizmos.color = new Color(0.22f, 1f, 0.46f, 0.45f);
            const float crossSize = 0.25f;
            Gizmos.DrawLine(transform.position + Vector3.left * crossSize, transform.position + Vector3.right * crossSize);
            Gizmos.DrawLine(transform.position + Vector3.down * crossSize, transform.position + Vector3.up * crossSize);
        }
    }
}
