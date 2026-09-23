using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectDM
{
    /// <summary>Owns the authored floor Tilemap and fills its initial field footprint.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
    public sealed class DungeonFloorTilemap : MonoBehaviour
    {
        [SerializeField, HideInInspector] private Tilemap tilemap;
        [SerializeField, HideInInspector] private TileBase[] floorTiles = new TileBase[16];
        [SerializeField, HideInInspector] private TileBase[] borderTiles = new TileBase[16];
        [SerializeField, HideInInspector] private Vector2Int generatedSize;
        [SerializeField, HideInInspector] private Vector2 generatedFieldSize;
        [Header("Tile Appearance")]
        [SerializeField, Min(0.25f)] private float tileScale = 2f;

        public bool HasCompletePalette
        {
            get
            {
                if (floorTiles == null || floorTiles.Length != 16)
                {
                    return false;
                }

                foreach (TileBase tile in floorTiles)
                {
                    if (tile == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public void SetTilePalette(TileBase[] tiles)
        {
            floorTiles = tiles;
            CacheComponents();
        }

        public void SetBorderTilePalette(TileBase[] tiles)
        {
            borderTiles = tiles;
            CacheComponents();
            RebuildInitialTiles();
        }

        [ContextMenu("Rebuild Floor Tiles")]
        public void RebuildTiles()
        {
            RebuildInitialTiles();
        }

        public void Configure(Vector2 fieldSize)
        {
            generatedFieldSize = new Vector2(Mathf.Max(1f, fieldSize.x), Mathf.Max(1f, fieldSize.y));
            tileScale = Mathf.Max(0.25f, tileScale);
            transform.localScale = Vector3.one * tileScale;
            Vector2Int requestedSize = new(
                Mathf.Max(1, Mathf.CeilToInt(generatedFieldSize.x / tileScale)),
                Mathf.Max(1, Mathf.CeilToInt(generatedFieldSize.y / tileScale)));

            CacheComponents();
            if (generatedSize == requestedSize && tilemap != null && tilemap.GetUsedTilesCount() > 0)
            {
                return;
            }

            generatedSize = requestedSize;
            RebuildInitialTiles();
        }

        private void OnValidate()
        {
            CacheComponents();
            TilemapRenderer renderer = GetComponent<TilemapRenderer>();
            renderer.sortingOrder = -10;
            tileScale = Mathf.Max(0.25f, tileScale);
            if (generatedFieldSize.x > 0f && generatedFieldSize.y > 0f)
            {
                Configure(generatedFieldSize);
            }
            else
            {
                GameFieldBounds fieldBounds = FindFirstObjectByType<GameFieldBounds>();
                if (fieldBounds != null)
                {
                    Configure(fieldBounds.Size);
                }
                else
                {
                    transform.localScale = Vector3.one * tileScale;
                }
            }
        }

        private void CacheComponents()
        {
            tilemap ??= GetComponent<Tilemap>();
        }

        private void RebuildInitialTiles()
        {
            if (tilemap == null || !HasCompletePalette)
            {
                return;
            }

            tilemap.ClearAllTiles();
            int minX = -generatedSize.x / 2;
            int minY = -generatedSize.y / 2;
            for (int x = 0; x < generatedSize.x; x++)
            {
                for (int y = 0; y < generatedSize.y; y++)
                {
                    int worldX = minX + x;
                    int worldY = minY + y;
                    tilemap.SetTile(new Vector3Int(worldX, worldY, 0), SelectTile(x, y, worldX, worldY));
                }
            }
        }

        private TileBase SelectTile(int x, int y, int worldX, int worldY)
        {
            if (HasCompleteBorderPalette && (x == 0 || y == 0 || x == generatedSize.x - 1 || y == generatedSize.y - 1))
            {
                int borderRow = y == generatedSize.y - 1 ? 0 : y == 0 ? 3 : ((worldY & 1) == 0 ? 1 : 2);
                int borderColumn = x == 0 ? 0 : x == generatedSize.x - 1 ? 3 : ((worldX & 1) == 0 ? 1 : 2);
                return borderTiles[borderRow * 4 + borderColumn];
            }

            // The 4×4 interior sheet is painted as one continuous surface. Preserve that spatial
            // arrangement instead of randomizing fragments, then repeat the complete super-tile.
            int sourceColumn = PositiveModulo(worldX, 4);
            int sourceRow = 3 - PositiveModulo(worldY, 4);
            return floorTiles[sourceRow * 4 + sourceColumn];
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private bool HasCompleteBorderPalette
        {
            get
            {
                if (borderTiles == null || borderTiles.Length != 16)
                {
                    return false;
                }

                foreach (TileBase tile in borderTiles)
                {
                    if (tile == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
