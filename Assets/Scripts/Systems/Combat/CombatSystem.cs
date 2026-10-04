using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Owns volleys, projectiles and critical-hit rolls.</summary>
    internal sealed class CombatSystem
    {
        private readonly GameSettings settings;
        private readonly GameTables tables;
        private readonly ProgressionService progression;
        private readonly WorldArtAssets art;
        private readonly PlayerWorld playerWorld;
        private readonly EnemySystem enemiesSystem;
        private readonly SkillSystem skills;
        private readonly CombatStats stats;
        private readonly GameObjectPool objectPool;

        internal CombatSystem(GameSettings settings, GameTables tables, ProgressionService progression, WorldArtAssets art, PlayerWorld playerWorld, EnemySystem enemiesSystem, SkillSystem skills, CombatStats stats, GameObjectPool objectPool)
        {
            this.settings = settings;
            this.tables = tables;
            this.progression = progression;
            this.art = art;
            this.playerWorld = playerWorld;
            this.enemiesSystem = enemiesSystem;
            this.skills = skills;
            this.stats = stats;
            this.objectPool = objectPool;
        }

        internal readonly List<Projectile> projectiles = new();

        internal float nextShot;

        internal void FireAtNearestEnemy(float time)
        {
            FirePrototypeVolley(time);
        }

        internal void UpdateProjectiles(float dt)
        {
            UpdatePrototypeProjectiles(dt);
        }

        internal readonly List<Enemy> volleyTargets = new();

        internal void FirePrototypeVolley(float time)
        {
            if (time < nextShot || enemiesSystem.enemies.Count == 0) return;
            volleyTargets.Clear();
            foreach (Enemy enemy in enemiesSystem.enemies) if (EnemySystem.IsLive(enemy)) volleyTargets.Add(enemy);
            volleyTargets.Sort((a, b) => ((a.transform.position - playerWorld.player.position).sqrMagnitude)
                .CompareTo((b.transform.position - playerWorld.player.position).sqrMagnitude));
            if (volleyTargets.Count == 0) return;
            nextShot = time + stats.AttackInterval;
            int count = 1 + Mathf.FloorToInt(progression.PermanentAmount("multishot"));
            for (int i = 0; i < count; i++)
            {
                Enemy target = volleyTargets[i % volleyTargets.Count];
                Vector2 direction = ((Vector2)target.transform.position - (Vector2)playerWorld.player.position).normalized;
                if (direction == Vector2.zero) direction = Vector2.right;
                GameObject bolt = objectPool.Rent(art.runtimeAssets.ProjectilePrefab);
                bolt.name = i == 0 ? "기본 공격" : "다중 공격";
                bolt.transform.position = playerWorld.player.position;
                bolt.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                bolt.transform.localScale = Vector3.one * .32f;
                SpriteRenderer renderer = bolt.GetComponentInChildren<SpriteRenderer>();
                renderer.sprite = art.boltSprite;
                renderer.sortingOrder = 4;
                renderer.color = new Color(1f, .7f, 1f);
                Animator animator = bolt.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.runtimeAnimatorController = art.boltController;
                    animator.enabled = art.boltController != null;
                    animator.applyRootMotion = false;
                    if (animator.enabled) { animator.Rebind(); animator.Update(0f); }
                }
                projectiles.Add(new Projectile { transform = bolt.transform, direction = direction,
                    damage = stats.MainDamage * (i == 0 ? 1f : tables.GrowthRow("multishot").damageRatio), lifetime = tables.balance.projectileLifetime });
            }
        }

        internal void UpdatePrototypeProjectiles(float dt)
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile bolt = projectiles[i];
                if (bolt.transform == null) { projectiles.RemoveAt(i); continue; }
                Vector2 from = bolt.transform.position;
                Vector2 to = from + bolt.direction * tables.balance.projectileSpeed * dt;
                bolt.transform.position = to;
                bolt.lifetime -= dt;
                // Swept collision prevents faster projectiles from tunneling past small targets.
                Enemy hit = null;
                float nearestAlong = float.MaxValue;
                Vector2 segment = to - from;
                foreach (Enemy enemy in enemiesSystem.enemies)
                {
                    if (!EnemySystem.IsLive(enemy)) continue;
                    Vector2 center = enemy.transform.position;
                    float along = segment.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(center - from, segment) / segment.sqrMagnitude) : 0;
                    if ((center - (from + segment * along)).sqrMagnitude < .55f * .55f && along < nearestAlong)
                    { nearestAlong = along; hit = enemy; }
                }
                if (hit != null) skills.ApplyPrototypeBasicHit(hit, bolt.damage);
                if (hit != null || bolt.lifetime <= 0)
                {
                    objectPool.Return(bolt.transform.gameObject);
                    projectiles.RemoveAt(i);
                }
            }
        }

        internal bool forceCriticalHits;

        internal bool RollCriticalHit()
        {
            if (!settings.enableCriticalHits) return false;
            return forceCriticalHits || tables.balance.criticalChance >= 1 ||
                (tables.balance.criticalChance > 0 && UnityEngine.Random.value < tables.balance.criticalChance);
        }

        internal void Reset()
        {
            foreach (Projectile projectile in projectiles) if (projectile.transform != null) objectPool.Return(projectile.transform.gameObject);
            projectiles.Clear(); nextShot = 0; forceCriticalHits = false;
        }
    }
}
