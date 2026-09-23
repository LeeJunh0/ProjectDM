using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace ProjectDM.Editor
{
    /// <summary>Creates the project's composition-root scene without overwriting an existing Main scene.</summary>
    public static class ProjectDMMainSceneSetup
    {
        private const string SceneFolder = "Assets/Scenes";
        private const string MainScenePath = SceneFolder + "/Main.unity";

        [MenuItem("Project DM/Create or Update Main Scene")]
        public static void CreateOrUpdateMainScene()
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            Scene scene;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }

            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            if (manager == null)
            {
                manager = new GameObject("GameManager").AddComponent<GameManager>();
            }

            Camera sceneCamera = Object.FindFirstObjectByType<Camera>();
            if (sceneCamera == null)
            {
                GameObject cameraObject = new("Main Camera");
                sceneCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                sceneCamera.orthographic = true;
                sceneCamera.orthographicSize = 5.5f;
                sceneCamera.backgroundColor = new Color(0.025f, 0.018f, 0.07f);
            }
            manager.ConfigureSceneCamera(sceneCamera);

            Transform layoutRoot = GetOrCreateChild(manager.transform, "Scene Layout");
            GameFieldBounds fieldBounds = GetOrCreateChild(layoutRoot, "Field Bounds").GetComponent<GameFieldBounds>();
            if (fieldBounds == null)
            {
                fieldBounds = layoutRoot.Find("Field Bounds").gameObject.AddComponent<GameFieldBounds>();
            }

            Transform playerSpawn = GetOrCreateChild(layoutRoot, "Player Spawn Point");
            if (playerSpawn.GetComponent<PlayerSpawnPointMarker>() == null)
            {
                playerSpawn.gameObject.AddComponent<PlayerSpawnPointMarker>();
            }

            MonsterSpawnAreaPreview monsterSpawnArea = GetOrCreateChild(layoutRoot, "Monster Spawn Area").GetComponent<MonsterSpawnAreaPreview>();
            if (monsterSpawnArea == null)
            {
                monsterSpawnArea = layoutRoot.Find("Monster Spawn Area").gameObject.AddComponent<MonsterSpawnAreaPreview>();
            }

            PlayerMovementAreaPreview playerMovementArea = GetOrCreateChild(layoutRoot, "Player Movement Bounds").GetComponent<PlayerMovementAreaPreview>();
            if (playerMovementArea == null)
            {
                playerMovementArea = layoutRoot.Find("Player Movement Bounds").gameObject.AddComponent<PlayerMovementAreaPreview>();
            }

            DungeonFloorTilemap dungeonFloor = GetOrCreateDungeonFloor();
            fieldBounds.SetDungeonFloor(dungeonFloor);
            manager.ConfigureSceneLayout(fieldBounds, playerSpawn, monsterSpawnArea, playerMovementArea, dungeonFloor);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);

            EditorSceneManager.SaveScene(scene, MainScenePath);
            AddMainSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Project DM Main scene is ready with its GameManager composition root.");
        }

        private static void AddMainSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
            int index = scenes.FindIndex(scene => scene.path == MainScenePath);
            if (index >= 0)
            {
                scenes[index] = new EditorBuildSettingsScene(MainScenePath, true);
            }
            else
            {
                scenes.Insert(0, new EditorBuildSettingsScene(MainScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new(childName);
            childObject.transform.SetParent(parent);
            childObject.transform.localPosition = Vector3.zero;
            return childObject.transform;
        }

        private static DungeonFloorTilemap GetOrCreateDungeonFloor()
        {
            GameObject gridObject = GameObject.Find("Dungeon Floor Grid");
            if (gridObject == null)
            {
                gridObject = new GameObject("Dungeon Floor Grid");
                gridObject.AddComponent<Grid>();
            }

            Transform tilemapTransform = GetOrCreateChild(gridObject.transform, "Dungeon Floor Tilemap");
            Tilemap tilemap = tilemapTransform.GetComponent<Tilemap>();
            if (tilemap == null)
            {
                tilemap = tilemapTransform.gameObject.AddComponent<Tilemap>();
            }

            TilemapRenderer renderer = tilemapTransform.GetComponent<TilemapRenderer>();
            if (renderer == null)
            {
                renderer = tilemapTransform.gameObject.AddComponent<TilemapRenderer>();
            }
            renderer.sortingOrder = -10;

            DungeonFloorTilemap dungeonFloor = tilemapTransform.GetComponent<DungeonFloorTilemap>();
            if (dungeonFloor == null)
            {
                dungeonFloor = tilemapTransform.gameObject.AddComponent<DungeonFloorTilemap>();
            }

            if (!dungeonFloor.HasCompletePalette)
            {
                TileBase[] floorTiles = new TileBase[16];
                for (int row = 0; row < 4; row++)
                {
                    for (int column = 0; column < 4; column++)
                    {
                        floorTiles[row * 4 + column] = AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/GameContent/Tiles/Floor_{column}_{row}.asset");
                    }
                }
                dungeonFloor.SetTilePalette(floorTiles);
            }

            TileBase[] borderTiles = new TileBase[16];
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    borderTiles[row * 4 + column] = AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/GameContent/Tiles/Borders/FloorBorder_{row}_{column}.asset");
                }
            }
            dungeonFloor.SetBorderTilePalette(borderTiles);

            return dungeonFloor;
        }
    }
}
