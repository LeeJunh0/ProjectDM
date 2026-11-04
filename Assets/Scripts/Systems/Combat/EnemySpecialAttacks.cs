using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Bounded special attacks; body motion and world-space indicators are independent.</summary>
    internal sealed class EnemySpecialAttacks : IDisposable
    {
        internal const float WarningSeconds = .18f, SporeSpeed = 1.65f, SporeLifetime = 1.2f, SporeRadius = .22f;
        private sealed class Attack
        {
            internal Enemy enemy;
            internal float age;
            internal bool fired, hit;
            internal Vector2 direction;
            internal Vector3 visualScale, visualPosition;
            internal LineRenderer indicator;
        }
        private sealed class Spore
        {
            internal Vector2 position, direction;
            internal float age, damage;
            internal LineRenderer body, indicator;
        }
        private readonly List<Attack> active = new();
        private readonly List<Spore> spores = new();
        private readonly Queue<LineRenderer> available = new();
        private readonly List<LineRenderer> visuals = new();
        internal Action<float> HitPlayer;
        internal int ActiveCount => active.Count;
        internal int SporeCount => spores.Count;
        internal static float Windup(MonsterAttackKind kind) => kind == MonsterAttackKind.ChainStrike ? .9f : kind == MonsterAttackKind.Charge ? .7f : .6f;
        private static float Reach(MonsterAttackKind kind) => kind == MonsterAttackKind.Spore ? 1.9f : kind == MonsterAttackKind.ArmorSweep ? 1.15f : kind == MonsterAttackKind.ChainStrike ? 4 : 4.5f;

        internal static Vector2 Track(Vector2 current, Vector2 target, float dt)
        {
            if (target.sqrMagnitude < .0001f) return current;
            float angle = Mathf.MoveTowardsAngle(Mathf.Atan2(current.y, current.x) * Mathf.Rad2Deg,
                Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg, 120 * Mathf.Max(0, dt));
            return new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        }

        internal static bool SegmentHits(Vector2 from, Vector2 to, Vector2 target, float radius)
        {
            Vector2 delta = to - from;
            float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(target - from, delta) / delta.sqrMagnitude) : 0;
            return (target - from - delta * t).sqrMagnitude <= radius * radius;
        }

        internal void Tick(List<Enemy> enemies, Vector2 player, float dt, PrototypeBalance balance, Bounds bounds)
        {
            if (dt <= 0) return;
            // Existing projectiles advance before new shots, so a newly fired spore gets its full lifetime.
            for (int i = spores.Count - 1; i >= 0; i--)
            {
                var s = spores[i];
                float step = Mathf.Min(dt, Mathf.Max(0, SporeLifetime - s.age));
                Vector2 next = s.position + s.direction * (SporeSpeed * step);
                bool hit = SegmentHits(s.position, next, player, SporeRadius);
                s.position = next; s.age += dt;
                if (hit || s.age >= SporeLifetime) { if (hit) HitPlayer?.Invoke(s.damage); Return(s.body); Return(s.indicator); spores.RemoveAt(i); continue; }
                float fade = Mathf.Clamp01((SporeLifetime - s.age) / .3f);
                Stroke(s.body, s.position, s.position + s.direction * .025f, .13f, new Color(.7f, .83f, .35f, fade));
                Circle(s.indicator, s.position, SporeRadius, new Color(1, .43f, .22f, fade * .8f));
            }
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var a = active[i]; var e = a.enemy;
                if (!EnemySystem.IsLive(e)) { Finish(a); active.RemoveAt(i); continue; }
                float step = Mathf.Max(0, dt - e.freezeRemaining);
                if (step <= 0) continue;
                float windup = Windup(e.specialAttack), before = a.age;
                a.age += step;
                // The red warning locks aim; tracking before that has a fixed angular speed.
                float tracking = Mathf.Max(0, Mathf.Min(a.age, windup - WarningSeconds) - before);
                if (tracking > 0) a.direction = Track(a.direction, player - (Vector2)e.transform.position, tracking);
                if (e.renderer != null && e.mirrorToMovement && Mathf.Abs(a.direction.x) > .01f) e.renderer.flipX = a.direction.x > 0;
                bool red = a.age >= windup - WarningSeconds;
                Color tint = red ? new Color(1, .18f, .12f, .85f) : new Color(1, .72f, .28f, .55f);
                if (a.age < windup)
                {
                    if (e.specialAttack == MonsterAttackKind.ArmorSweep) Circle(a.indicator, e.transform.position, Reach(e.specialAttack), tint);
                    else if (e.specialAttack != MonsterAttackKind.Spore) Corridor(a.indicator, e.transform.position, a.direction, Reach(e.specialAttack), e.specialAttack == MonsterAttackKind.Charge ? .35f : .23f, tint);
                }
                if (!a.fired && a.age >= windup)
                {
                    a.fired = true;
                    float damage = balance.monsters[(int)e.kind].contactDamage;
                    if (e.specialAttack == MonsterAttackKind.Spore && spores.Count < 12)
                    {
                        var spore = new Spore { position = e.transform.position, direction = a.direction, damage = damage,
                            body = Rent(e.renderer), indicator = Rent(e.renderer) };
                        Stroke(spore.body, spore.position, spore.position + spore.direction * .025f, .13f, new Color(.7f, .83f, .35f));
                        Circle(spore.indicator, spore.position, SporeRadius, new Color(1, .43f, .22f, .8f));
                        spores.Add(spore);
                    }
                    else if (e.specialAttack == MonsterAttackKind.ArmorSweep)
                    {
                        if (Vector2.Distance(player, e.transform.position) <= Reach(e.specialAttack)) HitPlayer?.Invoke(damage);
                    }
                    else if (e.specialAttack == MonsterAttackKind.ChainStrike)
                    {
                        if (SegmentHits(e.transform.position, (Vector2)e.transform.position + a.direction * Reach(e.specialAttack), player, .23f)) HitPlayer?.Invoke(damage);
                    }
                    a.indicator.gameObject.SetActive(false);
                }
                if (e.specialAttack == MonsterAttackKind.Charge && a.age >= windup)
                {
                    float dashTime = Mathf.Max(0, Mathf.Min(a.age - windup, .5f) - Mathf.Max(0, before - windup));
                    Vector2 from = e.transform.position, to = from + a.direction * (9 * dashTime);
                    to.x = Mathf.Clamp(to.x, bounds.min.x + .3f, bounds.max.x - .3f);
                    to.y = Mathf.Clamp(to.y, bounds.min.y + .3f, bounds.max.y - .3f);
                    e.transform.position = new Vector3(to.x, to.y, e.transform.position.z);
                    if (!a.hit && SegmentHits(from, to, player, .35f)) { a.hit = true; HitPlayer?.Invoke(balance.monsters[(int)e.kind].contactDamage); }
                }
                ApplyMotion(a);
                float duration = windup + (e.specialAttack == MonsterAttackKind.Charge ? 1.3f : .65f);
                if (a.age >= duration) { Finish(a); active.RemoveAt(i); }
            }
            foreach (var e in enemies)
            {
                if (!EnemySystem.IsLive(e) || e.specialAttack == MonsterAttackKind.None || e.specialAttacking) continue;
                e.attackCooldown = Mathf.Max(0, e.attackCooldown - dt);
                if (e.attackCooldown > 0 || e.freezeRemaining > 0 || active.Count >= 2 || Vector2.Distance(player, e.transform.position) > Reach(e.specialAttack)) continue;
                e.specialAttacking = true;
                var a = new Attack { enemy = e, direction = (player - (Vector2)e.transform.position).normalized, indicator = Rent(e.renderer),
                    visualScale = e.renderer.transform.localScale, visualPosition = e.renderer.transform.localPosition };
                if (a.direction == Vector2.zero) a.direction = Vector2.right;
                active.Add(a);
                if (e.specialAttack == MonsterAttackKind.Spore) a.indicator.gameObject.SetActive(false);
                else if (e.specialAttack == MonsterAttackKind.ArmorSweep) Circle(a.indicator, e.transform.position, Reach(e.specialAttack), new Color(1, .72f, .28f, .55f));
                else Corridor(a.indicator, e.transform.position, a.direction, Reach(e.specialAttack), e.specialAttack == MonsterAttackKind.Charge ? .35f : .23f, new Color(1, .72f, .28f, .55f));
            }
        }

        private static void ApplyMotion(Attack a)
        {
            var e = a.enemy;
            if (e.renderer == null || (e.specialAttack != MonsterAttackKind.Spore && e.specialAttack != MonsterAttackKind.ArmorSweep)) return;
            e.renderer.sprite = e.baseSprite;
            Vector2 scale = MonsterAttackMotion.Scale(e.specialAttack, a.age);
            e.renderer.transform.localScale = Vector3.Scale(a.visualScale, new Vector3(scale.x, scale.y, 1));
            // Only the visual child moves; gameplay position and hit radii stay independent.
            if (e.renderer.transform != e.transform) e.renderer.transform.localPosition = a.visualPosition + Vector3.right * MonsterAttackMotion.Shake(e.specialAttack, a.age);
        }
        private void Finish(Attack a)
        {
            var e = a.enemy;
            if (e.renderer != null) { e.renderer.transform.localScale = a.visualScale; if (e.renderer.transform != e.transform) e.renderer.transform.localPosition = a.visualPosition; }
            e.specialAttacking = false; e.attackCooldown = e.specialAttack == MonsterAttackKind.ChainStrike ? 6 : 4.5f;
            Return(a.indicator);
        }
        internal void Remove(Enemy enemy)
        {
            for (int i = active.Count - 1; i >= 0; i--) if (active[i].enemy == enemy) { Finish(active[i]); active.RemoveAt(i); }
        }
        private LineRenderer Rent(SpriteRenderer source)
        {
            var line = available.Count > 0 ? available.Dequeue() : new GameObject("Monster attack indicator").AddComponent<LineRenderer>();
            if (!visuals.Contains(line)) visuals.Add(line);
            line.gameObject.SetActive(true); line.sharedMaterial = source.sharedMaterial; line.useWorldSpace = true; line.sortingOrder = 1;
            line.textureMode = LineTextureMode.Stretch; line.numCapVertices = 0; line.numCornerVertices = 0;
            return line;
        }
        private void Return(LineRenderer line) { if (line == null) return; line.gameObject.SetActive(false); available.Enqueue(line); }
        private static void Stroke(LineRenderer line, Vector2 from, Vector2 to, float width, Color tint)
        {
            line.loop = false; line.positionCount = 2; line.startWidth = line.endWidth = width; line.startColor = line.endColor = tint;
            line.SetPosition(0, from); line.SetPosition(1, to);
        }
        private static void Circle(LineRenderer line, Vector2 center, float radius, Color tint)
        {
            line.gameObject.SetActive(true); line.loop = true; line.positionCount = 32; line.startWidth = line.endWidth = .025f; line.startColor = line.endColor = tint;
            for (int i = 0; i < 32; i++) { float a = i * Mathf.PI * 2 / 32; line.SetPosition(i, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius); }
        }
        private static void Corridor(LineRenderer line, Vector2 center, Vector2 direction, float length, float halfWidth, Color tint)
        {
            line.gameObject.SetActive(true); line.loop = true; line.positionCount = 4; line.startWidth = line.endWidth = .025f; line.startColor = line.endColor = tint;
            Vector2 side = new Vector2(-direction.y, direction.x) * halfWidth;
            line.SetPosition(0, center - side); line.SetPosition(1, center + direction * length - side);
            line.SetPosition(2, center + direction * length + side); line.SetPosition(3, center + side);
        }
        internal void Reset()
        {
            foreach (var a in active) Finish(a); active.Clear();
            foreach (var s in spores) { Return(s.body); Return(s.indicator); } spores.Clear();
        }
        public void Dispose() { Reset(); foreach (var line in visuals) if (line != null) UnityEngine.Object.Destroy(line.gameObject); visuals.Clear(); available.Clear(); }
    }
}
