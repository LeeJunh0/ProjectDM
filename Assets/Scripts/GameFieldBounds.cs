using UnityEngine;

namespace ProjectDM
{
    /// <summary>Scene object that previews the tile field footprint while editing Main.</summary>
    [DisallowMultipleComponent]
    public sealed class GameFieldBounds : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new(25f, 17f);
        [SerializeField, HideInInspector] private DungeonFloorTilemap dungeonFloor;

        public Vector2 Size => size;

        public void SetDungeonFloor(DungeonFloorTilemap floor)
        {
            dungeonFloor = floor;
            UpdateFloorSize();
        }

        private void OnValidate()
        {
            size = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            UpdateFloorSize();
        }

        private void UpdateFloorSize()
        {
            dungeonFloor ??= FindFirstObjectByType<DungeonFloorTilemap>();
            if (dungeonFloor != null)
            {
                dungeonFloor.Configure(size);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.28f, 0.78f, 1f, 0.95f);
            Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, 0.05f));

            Gizmos.color = new Color(0.28f, 0.78f, 1f, 0.45f);
            const float crossSize = 0.35f;
            Gizmos.DrawLine(transform.position + Vector3.left * crossSize, transform.position + Vector3.right * crossSize);
            Gizmos.DrawLine(transform.position + Vector3.down * crossSize, transform.position + Vector3.up * crossSize);
        }
    }
}
