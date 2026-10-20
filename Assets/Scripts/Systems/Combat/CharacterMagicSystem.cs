using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Personal magic only. Secondary hits cannot trigger the retired shared-skill pool.</summary>
    internal sealed class CharacterMagicSystem
    {
        private readonly CharacterSession session;
        private readonly System.Func<string, float> strength;
        private readonly System.Action<Enemy, float> applyHit;
        private readonly EnemySystem enemies;
        private readonly PrototypeSkillVisuals visuals;
        private readonly System.Func<float> damage;
        private readonly HashSet<Enemy> visited = new();
        private readonly List<Enemy> targets = new();
        private readonly List<Burn> burns = new();
        internal readonly FrostOrbitSystem frost;
        private sealed class Burn { internal Enemy enemy; internal float remaining, damage, accumulated; }

        internal CharacterMagicSystem(CharacterSession session, EnemySystem enemies, PrototypeSkillVisuals visuals,
            System.Func<float> damage, System.Func<string, float> strength, System.Action<Enemy, float> applyHit)
        {
            this.session = session; this.enemies = enemies; this.visuals = visuals; this.damage = damage; this.strength = strength; this.applyHit = applyHit;
            frost = new FrostOrbitSystem(enemies, visuals, damage, strength, applyHit);
        }

        internal void Cast(Vector3 origin, Enemy target, Transform caster)
        {
            if (session.Current.id == "lightning") Lightning(origin, target);
            else if (session.Current.id == "flame") Flame(origin, target);
            else if (session.Current.id == "frost") frost.Cast(session.Current, caster);
        }

        private void Lightning(Vector3 origin, Enemy target)
        {
            visited.Clear();
            int count = 1 + session.Current.extraTargets + Mathf.FloorToInt(strength("jumps"));
            float range = session.Current.range + strength("range");
            for (int hit = 0; hit < count && target != null; hit++)
            {
                Vector3 end = target.transform.position;
                visited.Add(target); visuals.Link(origin, end);
                float amount = damage() * (hit == 0 ? 1 : .5f);
                applyHit(target, amount);
                float echo = Mathf.Clamp01(strength("echo"));
                if (EnemySystem.IsLive(target) && echo > 0 && Random.value < echo)
                { visuals.Link(origin, end); applyHit(target, amount * .5f); }
                origin = end; target = null; float nearest = range * range;
                foreach (Enemy enemy in enemies.enemies)
                {
                    if (!EnemySystem.IsLive(enemy) || visited.Contains(enemy)) continue;
                    float distance = (enemy.transform.position - origin).sqrMagnitude;
                    if (distance <= nearest) { nearest = distance; target = enemy; }
                }
            }
        }

        private void Flame(Vector3 origin, Enemy target)
        {
            Vector2 direction = (target.transform.position - origin).normalized;
            if (direction == Vector2.zero) direction = Vector2.right;
            float range = session.Current.range + strength("range");
            float angle = Mathf.Clamp(session.Current.angle + strength("width"), 1, 180);
            visuals.FlameCone(origin, direction, range, angle);
            targets.Clear();
            foreach (Enemy enemy in enemies.enemies)
                if (EnemySystem.IsLive(enemy) && InCone((Vector2)(enemy.transform.position - origin), direction, range, angle)) targets.Add(enemy);
            foreach (Enemy enemy in targets)
            {
                applyHit(enemy, damage());
                float burn = strength("burn");
                if (!EnemySystem.IsLive(enemy) || burn <= 0) continue;
                Burn existing = burns.Find(b => b.enemy == enemy);
                if (existing == null) { existing = new Burn { enemy = enemy }; burns.Add(existing); }
                existing.remaining = 2; existing.damage = damage() * burn;
            }
        }

        internal static bool InCone(Vector2 offset, Vector2 direction, float range, float fullAngle) =>
            offset.sqrMagnitude <= range * range && (offset.sqrMagnitude < .000001f || Vector2.Angle(direction, offset) <= fullAngle * .5f);

        internal void Tick(float dt)
        {
            if (dt <= 0) return;
            frost.Tick(dt);
            for (int i = burns.Count - 1; i >= 0; i--)
            {
                Burn burn = burns[i];
                if (!EnemySystem.IsLive(burn.enemy)) { burns.RemoveAt(i); continue; }
                float step = Mathf.Min(dt, burn.remaining); burn.remaining -= step;
                burn.accumulated += step;
                // DOT is not a new cast, never pays MP, crits, or recursively adds burns.
                if (burn.accumulated >= .5f || burn.remaining <= 0)
                { enemies.DamageElementalEnemy(burn.enemy, burn.damage * burn.accumulated, CombatDamageKind.Attack, CombatDamageElement.Flame); burn.accumulated = 0; }
                if (burn.remaining <= 0) burns.RemoveAt(i);
            }
        }
        internal void Reset() { burns.Clear(); visited.Clear(); targets.Clear(); frost.Reset(); }
    }
}
