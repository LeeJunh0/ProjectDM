using UnityEngine;

namespace ProjectDM
{
    /// <summary>Editor-visible marker for the position at which a run begins.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnPointMarker : MonoBehaviour
    {
        [SerializeField, Min(0f), InspectorName("직접 획득 반경"), Tooltip("아이템에 닿아 획득하는 실제 반경입니다. 씬 뷰에서 노란 핸들을 끌어 조절할 수도 있습니다.")]
        private float pickupRadius = .28f;
        [SerializeField, Min(0f), InspectorName("끌어오기 추가 반경 미리보기"), Tooltip("편집 중 직접 획득 반경에 더해 파란 원으로 표시합니다. 실제 끌어오기 범위나 성장 수치는 바꾸지 않습니다.")]
        private float previewMagnetRadius;

        private Transform runtimePlayer;
        private float runtimeMagnetRadius;
        public float PickupRadius => Mathf.Max(0f, pickupRadius);
        public Vector3 RangeCenter => Application.isPlaying && runtimePlayer != null ? runtimePlayer.position : transform.position;

        internal static float SyncRange(Transform start, Transform player, float magnetBonus)
        {
            if (start == null || !start.TryGetComponent(out PlayerSpawnPointMarker marker)) return .28f;
            if (player != null) marker.SetRuntimeRange(player, marker.PickupRadius + Mathf.Max(0f, magnetBonus));
            return marker.PickupRadius;
        }

        internal void SetRuntimeRange(Transform player, float magnetRadius)
        {
            runtimePlayer = player;
            runtimeMagnetRadius = Mathf.Max(0f, magnetRadius);
        }

        private void OnValidate()
        {
            pickupRadius = Mathf.Max(0f, pickupRadius);
            previewMagnetRadius = Mathf.Max(0f, previewMagnetRadius);
        }

        private void OnDrawGizmos()
        {
            Vector3 position = transform.position;
            Gizmos.color = new Color(0.55f, 0.3f, 1f, 1f);
            Gizmos.DrawWireSphere(position, 0.38f);
            Gizmos.DrawLine(position, position + Vector3.up * 0.75f);
            Gizmos.DrawLine(position + Vector3.up * 0.75f, position + new Vector3(-0.16f, 0.52f, 0f));
            Gizmos.DrawLine(position + Vector3.up * 0.75f, position + new Vector3(0.16f, 0.52f, 0f));
            DrawCircle(RangeCenter, PickupRadius, new Color(1f, .78f, .22f, 1f));
            float magnetRadius = Application.isPlaying ? runtimeMagnetRadius : PickupRadius + previewMagnetRadius;
            if (magnetRadius > PickupRadius + .0001f) DrawCircle(RangeCenter, magnetRadius, new Color(.25f, .8f, 1f, .9f));
        }

        private static void DrawCircle(Vector3 center, float radius, Color color)
        {
            if (radius <= 0f) return;
            const int segments = 64;
            Gizmos.color = color;
            Vector3 previous = center + Vector3.right * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
