using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>One paid cast owns a temporary orbit. Shards never consume mana on contact.</summary>
    internal sealed class FrostOrbitSystem
    {
        private readonly EnemySystem enemies;
        private readonly PrototypeSkillVisuals visuals;
        private readonly Func<string, float> strength;
        private readonly Func<float> damage;
        private readonly Action<Enemy, float> hit;
        private readonly List<Dictionary<Enemy, float>> nextHits = new();
        private readonly List<Enemy> targets = new();
        private CharacterDefinition definition;
        private Transform caster;
        private Vector3 previousCenter;
        private float age, duration, radius, shardRadius, angularSpeed;
        internal bool Active { get; private set; }
        internal int Count => nextHits.Count;
        internal float Age => age;

        internal FrostOrbitSystem(EnemySystem enemies, PrototypeSkillVisuals visuals, Func<float> damage,
            Func<string, float> strength, Action<Enemy, float> hit)
        { this.enemies = enemies; this.visuals = visuals; this.damage = damage; this.strength = strength; this.hit = hit; }

        internal void Cast(CharacterDefinition definition, Transform caster)
        {
            Reset(); this.definition = definition; this.caster = caster;
            duration = definition.duration; radius = definition.range + strength("range");
            shardRadius = definition.shardRadius + strength("size"); angularSpeed = definition.orbitSpeed + strength("orbitSpeed");
            int count = Mathf.Clamp(definition.shardCount + Mathf.FloorToInt(strength("count")), 1, 16);
            for (int i = 0; i < count; i++) nextHits.Add(new Dictionary<Enemy, float>());
            previousCenter = caster.position; Active = true;
            visuals.BeginFrostOrbit(count, shardRadius);
            Show(previousCenter);
        }

        internal Vector3 Position(int index, Vector3 center, float atAge)
        {
            float angle = (atAge * angularSpeed + index * 360f / Count) * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        internal void Tick(float dt)
        {
            if (!Active || dt <= 0) return;
            if (caster == null) { Reset(); return; }
            float step = Mathf.Min(dt, Mathf.Max(0, duration - age));
            float until = age + step;
            Vector3 center = Vector3.Lerp(previousCenter, caster.position, step / dt);
            // Sample the curved path as well as player movement so fast rotation does not skip targets.
            int samples = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(step * angularSpeed / 8f,
                Vector3.Distance(previousCenter, center) / .2f)), 1, 256);
            float collisionRadius = shardRadius + .35f;
            for (int sample = 1; sample <= samples; sample++)
            {
                float t = (float)sample / samples, atAge = Mathf.Lerp(age, until, t);
                Vector3 atCenter = Vector3.Lerp(previousCenter, center, t);
                for (int shard = 0; shard < Count; shard++)
                {
                    Vector3 position = Position(shard, atCenter, atAge); targets.Clear();
                    foreach (Enemy enemy in enemies.enemies)
                        if (EnemySystem.IsLive(enemy) && (enemy.transform.position - position).sqrMagnitude <= collisionRadius * collisionRadius &&
                            (!nextHits[shard].TryGetValue(enemy, out float next) || atAge >= next)) targets.Add(enemy);
                    foreach (Enemy enemy in targets)
                    {
                        nextHits[shard][enemy] = atAge + definition.hitInterval;
                        hit(enemy, damage());
                        if (EnemySystem.IsLive(enemy)) ApplyCold(enemy);
                    }
                }
            }
            age = until; previousCenter = center;
            if (age >= duration) { Reset(); return; }
            Show(center);
        }

        private void ApplyCold(Enemy enemy)
        {
            enemy.chillRemaining = definition.chillDuration; enemy.chillSlow = definition.slowFraction;
            if (enemy.freezeRemaining > 0) return; // Frozen targets cannot build another freeze while locked.
            enemy.chillStacks += 1 + Mathf.FloorToInt(strength("chill"));
            if (enemy.chillStacks < definition.chillThreshold) return;
            enemy.chillStacks = 0; enemy.freezeRemaining = definition.freezeSeconds + strength("freezeDuration");
        }
        private void Show(Vector3 center)
        {
            for (int i = 0; i < Count; i++) visuals.MoveFrostShard(i, Position(i, center, age), age * angularSpeed);
        }
        internal void Reset()
        {
            Active = false; age = 0; caster = null; nextHits.Clear(); targets.Clear(); visuals.EndFrostOrbit();
        }
    }
}
