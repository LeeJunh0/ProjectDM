using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    /// <summary>Convenient editor command for regenerating the currently open floor Tilemap.</summary>
    public static class ProjectDMFloorTilemapTools
    {
        [MenuItem("Project DM/Rebuild Open Floor Tilemap")]
        public static void RebuildOpenFloorTilemap()
        {
            foreach (DungeonFloorTilemap floor in Object.FindObjectsByType<DungeonFloorTilemap>(FindObjectsSortMode.None))
            {
                floor.RebuildTiles();
                EditorUtility.SetDirty(floor);
            }

            Debug.Log("Project DM rebuilt the open floor Tilemap with contiguous interior tile ordering.");
        }
    }
}
