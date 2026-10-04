using UnityEngine;

namespace ProjectDM
{
    /// <summary>Editor-visible marker for the position at which a run begins.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnPointMarker : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Vector3 position = transform.position;
            Gizmos.color = new Color(0.55f, 0.3f, 1f, 1f);
            Gizmos.DrawWireSphere(position, 0.38f);
            Gizmos.DrawLine(position, position + Vector3.up * 0.75f);
            Gizmos.DrawLine(position + Vector3.up * 0.75f, position + new Vector3(-0.16f, 0.52f, 0f));
            Gizmos.DrawLine(position + Vector3.up * 0.75f, position + new Vector3(0.16f, 0.52f, 0f));
        }
    }
}
