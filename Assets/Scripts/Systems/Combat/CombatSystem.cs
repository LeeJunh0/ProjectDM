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
        private readonly PlayerStats playerStats;
        private readonly RunMana mana;

        internal CombatSystem(GameSettings settings, GameTables tables, ProgressionService progression, WorldArtAssets art, PlayerWorld playerWorld, EnemySystem enemiesSystem, SkillSystem skills, CombatStats stats, GameObjectPool objectPool, PlayerStats playerStats, RunMana mana)
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
            this.playerStats = playerStats;
            this.mana = mana;
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
            if (!skills.CanCastPersonalMagic(playerWorld.player.position, volleyTargets[0])) return;
            // One payment for the entire volley. No targets/cooldown/extra projectiles do not charge again.
            if (!mana.SpendForBasicAttack()) return;
            nextShot = time + stats.AttackInterval;
            if (skills.Character != null && skills.Character.id != "projectile")
            { skills.CastPersonalMagic(playerWorld.player.position, volleyTargets[0], playerWorld.player); return; }
            int count = 1 + Mathf.FloorToInt(skills.Character != null ? skills.PersonalAmount("count") + progression.PermanentAmount("multishot") : progression.PermanentAmount("multishot"));
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
                    damage = stats.MainDamage * (i == 0 ? 1f : tables.GrowthRow("multishot").damageRatio), lifetime = tables.balance.projectileLifetime,
                    pierces = Mathf.FloorToInt(skills.PersonalAmount("pierce")) });
            }
        }

        internal void UpdatePrototypeProjectiles(float dt)
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile bolt = projectiles[i];
                if (bolt.transform == null) { projectiles.RemoveAt(i); continue; }
                Vector2 from = bolt.transform.position;
                float homing = skills.PersonalAmount("homing");
                if (homing > 0)
                {
                    Enemy nearest = null; float distance = float.MaxValue;
                    foreach (Enemy enemy in enemiesSystem.enemies)
                        if (EnemySystem.IsLive(enemy) && !bolt.hits.Contains(enemy) && ((Vector2)enemy.transform.position - from).sqrMagnitude < distance)
                        { nearest = enemy; distance = ((Vector2)enemy.transform.position - from).sqrMagnitude; }
                    if (nearest != null)
                    {
                        float turn = Mathf.Clamp(Vector2.SignedAngle(bolt.direction, (Vector2)nearest.transform.position - from), -homing * dt, homing * dt);
                        bolt.direction = Quaternion.Euler(0, 0, turn) * bolt.direction;
                        bolt.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(bolt.direction.y, bolt.direction.x) * Mathf.Rad2Deg);
                    }
                }
                Vector2 to = from + bolt.direction * tables.balance.projectileSpeed * dt;
                bolt.transform.position = to;
                bolt.lifetime -= dt;
                // Swept collision prevents faster projectiles from tunneling past small targets.
                Enemy hit = null;
                Vector2 segment = to - from;
                bool finished = false;
                do
                {
                    hit = null; float nearestAlong = float.MaxValue;
                    foreach (Enemy enemy in enemiesSystem.enemies)
                    {
                        if (!EnemySystem.IsLive(enemy) || bolt.hits.Contains(enemy)) continue;
                        Vector2 center = enemy.transform.position;
                        float along = segment.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(center - from, segment) / segment.sqrMagnitude) : 0;
                        if ((center - (from + segment * along)).sqrMagnitude < .55f * .55f && along < nearestAlong)
                        { nearestAlong = along; hit = enemy; }
                    }
                    if (hit != null) { bolt.hits.Add(hit); skills.ApplyPrototypeBasicHit(hit, bolt.damage); finished = bolt.pierces-- <= 0; }
                } while (hit != null && !finished);
                if (finished || bolt.lifetime <= 0)
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
            return forceCriticalHits || playerStats.CriticalChance >= 1 ||
                (playerStats.CriticalChance > 0 && UnityEngine.Random.value < playerStats.CriticalChance);
        }

        internal void Reset()
        {
            foreach (Projectile projectile in projectiles) if (projectile.transform != null) objectPool.Return(projectile.transform.gameObject);
            projectiles.Clear(); nextShot = 0; forceCriticalHits = false;
        }
    }
}
