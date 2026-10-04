using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Owns player health, contact damage, immunity and defeat.</summary>
    internal sealed class PlayerCombatSystem
    {
        private readonly GameSettings settings;
        private readonly GameTables tables;
        private readonly PlayerWorld playerWorld;
        private readonly RunController run;
        private readonly EnemySystem enemiesSystem;
        private readonly CombatSystem combat;
        private readonly DamagePresentation presentation;

        internal PlayerCombatSystem(GameSettings settings, GameTables tables, PlayerWorld playerWorld, RunController run, EnemySystem enemiesSystem, CombatSystem combat, DamagePresentation presentation)
        {
            this.settings = settings;
            this.tables = tables;
            this.playerWorld = playerWorld;
            this.run = run;
            this.enemiesSystem = enemiesSystem;
            this.combat = combat;
            this.presentation = presentation;
        }

        internal float playerHealth;

        internal float playerInvulnerabilityRemaining;

        internal bool playerDefeated;

        internal void ResetPlayerCombat()
        {
            playerHealth = tables.balance.playerHealth;
            playerInvulnerabilityRemaining = 0;
            playerDefeated = combat.forceCriticalHits = false;
        }

        internal void UpdatePlayerContactDamage(float dt)
        {
            playerInvulnerabilityRemaining = Mathf.Max(0, playerInvulnerabilityRemaining - dt);
            if (!settings.enablePlayerContactDamage || playerDefeated || playerInvulnerabilityRemaining > 0 || dt <= 0) return;
            float damage = 0;
            float radiusSquared = tables.balance.playerContactRadius * tables.balance.playerContactRadius;
            foreach (Enemy enemy in enemiesSystem.enemies)
                if (EnemySystem.IsLive(enemy) && (enemy.transform.position - playerWorld.player.position).sqrMagnitude <= radiusSquared)
                    damage = Mathf.Max(damage, tables.balance.monsters[(int)enemy.kind].contactDamage);
            if (damage > 0) ApplyPlayerDamage(damage);
        }

        internal void ApplyPlayerDamage(float damage)
        {
            if (run.runEnded || playerDefeated || playerInvulnerabilityRemaining > 0 || damage <= 0) return;
            playerHealth = Mathf.Max(0, playerHealth - damage);
            playerInvulnerabilityRemaining = tables.balance.playerInvulnerability;
            presentation.ShowCombatDamage(playerWorld.player.position, damage, CombatDamageKind.Received);
            if (playerHealth > 0) return;
            playerDefeated = true;
            run.EndRun();
        }
    }
}
