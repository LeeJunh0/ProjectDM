using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectDM
{
    /// <summary>
    /// Auto-selects one of the authored 3x3 ground tiles according to neighbouring cells.
    /// The visual palette stays authored in Tile assets; this class only owns connection rules.
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonGroundRuleTile", menuName = "Project DM/Tiles/Dungeon Ground Rule Tile")]
    public sealed class DungeonGroundRuleTile : TileBase
    {
        [SerializeField, HideInInspector] private TileBase[] floorTiles = new TileBase[16];
        [SerializeField, HideInInspector] private TileBase[] borderTiles = new TileBase[16];
        [SerializeField] private TileBase[] ruleTiles = new TileBase[9];
        [SerializeField] private RuleDefinition[] customRules = new RuleDefinition[0];

        public enum NeighborCondition
        {
            Any = 0,
            This = 1,
            NotThis = 2
        }

        [System.Serializable]
        public sealed class RuleDefinition
        {
            [SerializeField] private string displayName = "New Rule";
            [SerializeField] private NeighborCondition[] conditions = new NeighborCondition[9];
            [SerializeField] private TileBase outputTile;

            public bool HasOutput => outputTile != null;
            public TileBase OutputTile => outputTile;

            public bool Matches(Vector3Int position, ITilemap tilemap, DungeonGroundRuleTile source)
            {
                for (int row = 0; row < 3; row++)
                {
                    for (int column = 0; column < 3; column++)
                    {
                        int index = row * 3 + column;
                        if (index == 4)
                        {
                            continue;
                        }

                        NeighborCondition condition = conditions != null && index < conditions.Length
                            ? conditions[index]
                            : NeighborCondition.Any;
                        if (condition == NeighborCondition.Any)
                        {
                            continue;
                        }

                        Vector3Int offset = new Vector3Int(column - 1, 1 - row, 0);
                        bool isSameRuleTile = tilemap.GetTile(position + offset) == source;
                        if ((condition == NeighborCondition.This && !isSameRuleTile)
                            || (condition == NeighborCondition.NotThis && isSameRuleTile))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        public bool HasCompletePalette => HasNineTiles(ruleTiles) || (HasSixteenTiles(floorTiles) && HasSixteenTiles(borderTiles));
        public bool HasRuleTilePalette => HasNineTiles(ruleTiles);

        public void ConfigureLegacyPalette(TileBase[] interior, TileBase[] border)
        {
            floorTiles = ClonePalette(interior);
            borderTiles = ClonePalette(border);
        }

        public void ConfigureRuleTilePalette(TileBase[] tiles)
        {
            ruleTiles = CloneRulePalette(tiles);
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            TileBase selectedTile = SelectTile(position, tilemap);
            if (selectedTile == null)
            {
                tileData.sprite = null;
                tileData.colliderType = Tile.ColliderType.None;
                return;
            }

            selectedTile.GetTileData(position, tilemap, ref tileData);
            tileData.colliderType = Tile.ColliderType.None;
        }

        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    tilemap.RefreshTile(position + new Vector3Int(x, y, 0));
                }
            }
        }

        private TileBase SelectTile(Vector3Int position, ITilemap tilemap)
        {
            TileBase configuredTile = SelectCustomRuleTile(position, tilemap);
            if (configuredTile != null)
            {
                return configuredTile;
            }

            if (HasRuleTilePalette)
            {
                return SelectRulePaletteTile(position, tilemap);
            }

            if (!HasCompletePalette)
            {
                return null;
            }

            bool north = IsConnected(position + Vector3Int.up, tilemap);
            bool south = IsConnected(position + Vector3Int.down, tilemap);
            bool west = IsConnected(position + Vector3Int.left, tilemap);
            bool east = IsConnected(position + Vector3Int.right, tilemap);

            // Convex corners use their dedicated authored corner sprites.
            if (!north && !west) return Border(0, 0);
            if (!north && !east) return Border(0, 3);
            if (!south && !west) return Border(3, 0);
            if (!south && !east) return Border(3, 3);

            // Each side has two existing visual variants. Alternate those variants only
            // along the edge, keeping all connection decisions neighbour-driven.
            if (!north) return Border(0, 1 + PositiveModulo(position.x, 2));
            if (!south) return Border(3, 1 + PositiveModulo(position.x, 2));
            if (!west) return Border(1 + PositiveModulo(position.y, 2), 0);
            if (!east) return Border(1 + PositiveModulo(position.y, 2), 3);

            // Preserve the authored 4x4 interior surface and repeat the full pattern.
            int column = PositiveModulo(position.x, 4);
            int row = 3 - PositiveModulo(position.y, 4);
            return floorTiles[row * 4 + column];
        }

        private TileBase SelectRulePaletteTile(Vector3Int position, ITilemap tilemap)
        {
            bool north = IsConnected(position + Vector3Int.up, tilemap);
            bool south = IsConnected(position + Vector3Int.down, tilemap);
            bool west = IsConnected(position + Vector3Int.left, tilemap);
            bool east = IsConnected(position + Vector3Int.right, tilemap);

            if (!north && !west) return RuleTile(0, 0);
            if (!north && !east) return RuleTile(0, 2);
            if (!south && !west) return RuleTile(2, 0);
            if (!south && !east) return RuleTile(2, 2);
            if (!north) return RuleTile(0, 1);
            if (!south) return RuleTile(2, 1);
            if (!west) return RuleTile(1, 0);
            if (!east) return RuleTile(1, 2);
            return RuleTile(1, 1);
        }

        private TileBase SelectCustomRuleTile(Vector3Int position, ITilemap tilemap)
        {
            if (customRules == null)
            {
                return null;
            }

            // The first matching rule wins, matching Unity Rule Tile's ordered rule behaviour.
            foreach (RuleDefinition rule in customRules)
            {
                if (rule != null && rule.HasOutput && rule.Matches(position, tilemap, this))
                {
                    return rule.OutputTile;
                }
            }

            return null;
        }

        private bool IsConnected(Vector3Int position, ITilemap tilemap)
        {
            return tilemap.GetTile(position) == this;
        }

        private TileBase Border(int row, int column)
        {
            return borderTiles[row * 4 + column];
        }

        private TileBase RuleTile(int row, int column)
        {
            return ruleTiles[row * 3 + column];
        }

        private static TileBase[] ClonePalette(TileBase[] source)
        {
            TileBase[] copy = new TileBase[16];
            if (source != null)
            {
                System.Array.Copy(source, copy, Mathf.Min(source.Length, copy.Length));
            }

            return copy;
        }

        private static TileBase[] CloneRulePalette(TileBase[] source)
        {
            TileBase[] copy = new TileBase[9];
            if (source != null)
            {
                System.Array.Copy(source, copy, Mathf.Min(source.Length, copy.Length));
            }

            return copy;
        }

        private static bool HasSixteenTiles(TileBase[] palette)
        {
            if (palette == null || palette.Length != 16)
            {
                return false;
            }

            foreach (TileBase tile in palette)
            {
                if (tile == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasNineTiles(TileBase[] palette)
        {
            if (palette == null || palette.Length != 9)
            {
                return false;
            }

            foreach (TileBase tile in palette)
            {
                if (tile == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
