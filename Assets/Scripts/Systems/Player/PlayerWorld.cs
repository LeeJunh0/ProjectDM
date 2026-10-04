using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Creates, moves and resets the player within authored movement bounds.</summary>
    internal sealed class PlayerWorld
    {
        private readonly GameSettings settings;
        private readonly GameTables tables;
        private readonly WorldArtAssets art;

        internal PlayerWorld(GameSettings settings, GameTables tables, WorldArtAssets art)
        {
            this.settings = settings;
            this.tables = tables;
            this.art = art;
        }

        internal Transform player;

        internal Player playerController;

        internal void CreatePlayer()
        {
            GameObject avatar = UnityEngine.Object.Instantiate(art.runtimeAssets.PlayerPrefab);
            avatar.name = "Arcane Hunter";
            avatar.transform.position = settings.playerSpawnPoint != null ? settings.playerSpawnPoint.position : Vector3.zero;
            avatar.transform.localScale = Vector3.one * 0.95f;
            SpriteRenderer renderer = avatar.GetComponentInChildren<SpriteRenderer>();
            renderer.sortingOrder = 3;
            player = avatar.transform;
            Animator animator = avatar.GetComponentInChildren<Animator>();
            animator.applyRootMotion = false;
            playerController = avatar.GetComponent<Player>();
            playerController.Initialize(
                art.playerSprite,
                art.playerAnimatorController);
            Vector2 movementCenter = settings.playerMovementArea != null ? settings.playerMovementArea.transform.position : Vector2.zero;
            Vector2 movementBounds = settings.playerMovementArea != null ? settings.playerMovementArea.HalfExtents : settings.playerMovementBounds;
            playerController.SetMovementBounds(movementBounds, movementCenter);

        }

        internal void MovePlayer(float dt)
        {
            float speed = tables.balance.moveSpeed;
            playerController.Tick(dt, speed);
        }

        internal void ResetPosition()
        {
            player.position = settings.playerSpawnPoint != null ? settings.playerSpawnPoint.position : Vector3.zero;
            playerController.Initialize(art.playerSprite, art.playerAnimatorController);
        }
    }
}
