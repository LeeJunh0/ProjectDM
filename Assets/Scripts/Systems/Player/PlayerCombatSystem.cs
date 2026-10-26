using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Applies contact damage to mana, with immunity and defeat on depletion.</summary>
    internal sealed class PlayerCombatSystem
    {
        private readonly GameSettings settings;
        private readonly GameTables tables;
        private readonly PlayerWorld playerWorld;
        private readonly RunController run;
        private readonly EnemySystem enemiesSystem;
        private readonly CombatSystem combat;
        private readonly DamagePresentation presentation;
        private readonly PlayerStats stats;
        private readonly Action<float> onManaHit;

        internal PlayerCombatSystem(GameSettings settings, GameTables tables, PlayerWorld playerWorld, RunController run, EnemySystem enemiesSystem, CombatSystem combat, DamagePresentation presentation, PlayerStats stats, Action<float> onManaHit = null)
        {
            this.settings = settings;
            this.tables = tables;
            this.playerWorld = playerWorld;
            this.run = run;
            this.enemiesSystem = enemiesSystem;
            this.combat = combat;
            this.presentation = presentation;
            this.stats = stats;
            this.onManaHit = onManaHit;
        }

        internal float playerInvulnerabilityRemaining;

        internal bool playerDefeated;

        internal void ResetPlayerCombat()
        {
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
                if (EnemySystem.IsLive(enemy) && (enemy.transform.position - playerWorld.player.position).sqrMagnitude <= radiusSquared * enemy.hitRadiusMultiplier * enemy.hitRadiusMultiplier)
                    damage = Mathf.Max(damage, tables.balance.monsters[(int)enemy.kind].contactDamage);
            if (damage > 0) ApplyPlayerDamage(damage);
        }

        internal void ApplyPlayerDamage(float damage)
        {
            if (run.runEnded || playerDefeated || playerInvulnerabilityRemaining > 0 || damage <= 0) return;
            float before = run.mana.Current;
            run.mana.Consume(damage);
            float lostMana = before - run.mana.Current;
            if (lostMana > 0) onManaHit?.Invoke(lostMana);
            playerInvulnerabilityRemaining = stats.Invulnerability;
            presentation.ShowCombatDamage(playerWorld.player.position, damage, CombatDamageKind.Received);
            if (!run.mana.IsDepleted) return;
            playerDefeated = true;
            run.EndRun();
        }
    }
}
