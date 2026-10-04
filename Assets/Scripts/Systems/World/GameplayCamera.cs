using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Tracks the player and confines the viewport to the authored field.</summary>
    internal sealed class GameplayCamera
    {
        private readonly GameSettings settings;
        private readonly PlayerWorld playerWorld;

        internal GameplayCamera(GameSettings settings, PlayerWorld playerWorld)
        {
            this.settings = settings;
            this.playerWorld = playerWorld;
        }

        internal void SetupCamera()
        {
            Camera camera = settings.gameplayCamera != null ? settings.gameplayCamera : Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.orthographicSize = 5.5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.backgroundColor = new Color(0.025f, 0.018f, 0.07f);
            }

            settings.gameplayCamera = camera;
            // Preserve the intended 1920×1080 composition on any monitor by letter/pillarboxing the camera.
            const float targetAspect = 16f / 9f;
            float windowAspect = (float)Screen.width / Screen.height;
            float heightScale = windowAspect / targetAspect;
            camera.rect = heightScale < 1f
                ? new Rect(0f, (1f - heightScale) * .5f, 1f, heightScale)
                : new Rect((1f - 1f / heightScale) * .5f, 0f, 1f / heightScale, 1f);
            foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                light.enabled = false;
            }
        }

        internal void SnapCameraToPlayer()
        {
            if (settings.gameplayCamera == null || playerWorld.player == null)
            {
                return;
            }

            Vector3 cameraPosition = settings.gameplayCamera.transform.position;
            settings.gameplayCamera.transform.position = ClampCameraToFloor(new Vector3(playerWorld.player.position.x, playerWorld.player.position.y, cameraPosition.z));
        }

        internal void FollowPlayerWithCamera(float dt)
        {
            if (settings.gameplayCamera == null || playerWorld.player == null || settings.cameraFollowSpeed <= 0f)
            {
                return;
            }

            Vector3 cameraPosition = settings.gameplayCamera.transform.position;
            Vector3 targetPosition = ClampCameraToFloor(new Vector3(playerWorld.player.position.x, playerWorld.player.position.y, cameraPosition.z));
            settings.gameplayCamera.transform.position = Vector3.MoveTowards(cameraPosition, targetPosition, settings.cameraFollowSpeed * dt);
        }

        internal Vector3 ClampCameraToFloor(Vector3 position)
        {
            if (settings.gameplayCamera == null || settings.dungeonFloor == null || !settings.dungeonFloor.TryGetWorldBounds(out Bounds floorBounds))
            {
                return position;
            }

            float halfHeight = settings.gameplayCamera.orthographicSize;
            float halfWidth = halfHeight * settings.gameplayCamera.aspect;
            float minX = floorBounds.min.x + halfWidth;
            float maxX = floorBounds.max.x - halfWidth;
            float minY = floorBounds.min.y + halfHeight;
            float maxY = floorBounds.max.y - halfHeight;

            position.x = minX > maxX ? floorBounds.center.x : Mathf.Clamp(position.x, minX, maxX);
            position.y = minY > maxY ? floorBounds.center.y : Mathf.Clamp(position.y, minY, maxY);
            return position;
        }
    }
}
