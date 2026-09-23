using UnityEngine;

namespace ProjectDM
{
    /// <summary>Editor-visible annulus showing the minimum and maximum enemy spawn distance.</summary>
    [DisallowMultipleComponent]
    public sealed class MonsterSpawnAreaPreview : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float minimumDistance = 10f;
        [SerializeField, Min(0.1f)] private float maximumDistance = 12f;

        public float MinimumDistance => minimumDistance;
        public float MaximumDistance => maximumDistance;

        private void OnValidate()
        {
            minimumDistance = Mathf.Max(0.1f, minimumDistance);
            maximumDistance = Mathf.Max(minimumDistance, maximumDistance);
        }

        private void OnDrawGizmos()
        {
            DrawCircle(minimumDistance, new Color(1f, 0.66f, 0.18f, 0.95f));
            DrawCircle(maximumDistance, new Color(1f, 0.32f, 0.16f, 0.95f));

            Gizmos.color = new Color(1f, 0.47f, 0.12f, 0.85f);
            Gizmos.DrawLine(transform.position + Vector3.left * 0.35f, transform.position + Vector3.right * 0.35f);
            Gizmos.DrawLine(transform.position + Vector3.down * 0.35f, transform.position + Vector3.up * 0.35f);
        }

        private void DrawCircle(float radius, Color color)
        {
            const int segments = 48;
            Gizmos.color = color;
            Vector3 previous = transform.position + Vector3.right * radius;
            for (int index = 1; index <= segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                Vector3 next = transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
