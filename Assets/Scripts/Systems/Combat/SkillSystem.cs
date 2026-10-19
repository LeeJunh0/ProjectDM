using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Owns temporary skill ranks, independent hit-effect rolls and pending meteors.</summary>
    internal sealed class SkillSystem
    {
        private readonly GameTables tables;
        private readonly ProgressionService progression;
        private readonly WorldArtAssets art;
        private readonly RunController run;
        private readonly EnemySystem enemiesSystem;
        private readonly PrototypeSkillVisuals skillVisuals;
        private readonly Func<bool> criticalRoll;
        private readonly Func<float> mainDamage;
        private readonly PlayerStats playerStats;
        private readonly CharacterMagicSystem personalMagic;

        internal SkillSystem(GameTables tables, ProgressionService progression, WorldArtAssets art, RunController run, EnemySystem enemiesSystem, PrototypeSkillVisuals skillVisuals, Func<bool> criticalRoll, Func<float> mainDamage, PlayerStats playerStats)
        {
            this.tables = tables;
            this.progression = progression;
            this.art = art;
            this.run = run;
            this.enemiesSystem = enemiesSystem;
            this.skillVisuals = skillVisuals;
            this.criticalRoll = criticalRoll;
            this.mainDamage = mainDamage;
            this.playerStats = playerStats;
            personalMagic = new CharacterMagicSystem(run.characters, enemiesSystem, skillVisuals, mainDamage, PersonalAmount, ApplyPersonalHit);
        }

        internal readonly Dictionary<string, int> runSkillRanks = new();

        internal readonly List<Enemy> damageTargets = new();

        internal readonly HashSet<Enemy> chainVisited = new();

        internal readonly List<PendingMeteor> meteors = new();

        internal bool forceAttackEffects;

        internal sealed class PendingMeteor
        {
            public Vector3 center;
            public float impactAt, radius, damage;
        }

        internal int RunRank(string id) => runSkillRanks.TryGetValue(id, out int value) ? value : 0;
        internal CharacterDefinition Character => run.characters?.Current;
        internal float PersonalAmount(string id)
        {
            if (Character == null) return 0;
            CharacterUpgrade row = System.Array.Find(Character.upgrades, u => u.id == id);
            return row == null ? 0 : progression.PermanentAmount(row.Key(Character.id)) + RunRank(row.Key(Character.id)) * row.step;
        }
        internal FrostOrbitSystem Frost => personalMagic.frost;
        internal void CastPersonalMagic(Vector3 origin, Enemy target, Transform caster) => personalMagic.Cast(origin, target, caster);
        internal bool CanCastPersonalMagic(Vector3 origin, Enemy target)
        {
            if (Character?.id == "frost") return !Frost.Active && (target.transform.position - origin).sqrMagnitude <=
                Mathf.Pow(Character.range + PersonalAmount("range") + Character.shardRadius + PersonalAmount("size") + .35f, 2);
            return Character?.id != "flame" || (target.transform.position - origin).sqrMagnitude <= Mathf.Pow(Character.range + PersonalAmount("range"), 2);
        }
        internal void ApplyPersonalHit(Enemy target, float damage)
        {
            if (!EnemySystem.IsLive(target)) return;
            bool critical = criticalRoll();
            enemiesSystem.DamagePrototypeEnemyWithPresentation(target, damage * (critical ? playerStats.CriticalDamage : 1),
                critical ? CombatDamageKind.Critical : CombatDamageKind.Attack);
        }

        internal double AttackEffectChance(PrototypeSkill row)
        {
            return PrototypeRules.Chance(row.chance + Mathf.Max(0, RunRank(row.id) - 1) * row.rankChanceStep,
                progression.PermanentAmount(row.id + "Chance"), RunRank("proc") * tables.SkillRow("proc").step);
        }

        internal float EffectDamage(PrototypeSkill row) => mainDamage() *
            (row.damageRatio + Mathf.Max(0, RunRank(row.id) - 1) * row.rankDamageStep);

        internal void ApplyPrototypeBasicHit(Enemy target, float damage)
        {
            if (Character != null) { ApplyPersonalHit(target, damage); return; }
            if (!EnemySystem.IsLive(target)) return;
            Vector3 origin = target.transform.position;
            // Capture independent rolls BEFORE a lethal direct hit. Damage effects never call this method.
            bool chain = RollAttackEffect("chain"), splash = RollAttackEffect("splash"), meteor = RollAttackEffect("meteor");
            bool critical = criticalRoll();
            enemiesSystem.DamagePrototypeEnemyWithPresentation(target, damage * (critical ? playerStats.CriticalDamage : 1),
                critical ? CombatDamageKind.Critical : CombatDamageKind.Attack);
            if (chain) TriggerPrototypeChain(origin, target);
            if (splash)
            {
                PrototypeSkill row = tables.SkillRow("splash");
                skillVisuals.Burst(origin, row.radius, new Color(.35f, .75f, 1f), .3f);
                DamagePrototypeArea(origin, row.radius, EffectDamage(row));
            }
            if (meteor)
            {
                PrototypeSkill row = tables.SkillRow("meteor");
                meteors.Add(new PendingMeteor { center = origin, impactAt = run.elapsed + row.delay,
                    radius = row.radius, damage = EffectDamage(row) });
                skillVisuals.Meteor(origin, row.radius, row.delay);
            }
        }

        internal bool RollAttackEffect(string id)
        {
            if (RunRank(id) < 1) return false;
            double chance = AttackEffectChance(tables.SkillRow(id));
            return forceAttackEffects || chance >= 1 || Random.value < chance;
        }

        internal void TriggerPrototypeChain(Vector3 origin, Enemy original)
        {
            PrototypeSkill row = tables.SkillRow("chain");
            chainVisited.Clear();
            chainVisited.Add(original);
            for (int hop = 0; hop < row.targets; hop++)
            {
                Enemy next = null;
                float nearest = row.range * row.range;
                foreach (Enemy enemy in enemiesSystem.enemies)
                {
                    if (!EnemySystem.IsLive(enemy) || chainVisited.Contains(enemy)) continue;
                    float distance = (enemy.transform.position - origin).sqrMagnitude;
                    if (distance <= nearest) { nearest = distance; next = enemy; }
                }
                if (next == null) break;
                Vector3 end = next.transform.position;
                chainVisited.Add(next);
                skillVisuals.Link(origin, end);
                enemiesSystem.DamagePrototypeEnemy(next, EffectDamage(row));
                origin = end;
            }
        }

        internal void DamagePrototypeArea(Vector3 center, float radius, float damage)
        {
            damageTargets.Clear();
            foreach (Enemy enemy in enemiesSystem.enemies)
                if (EnemySystem.IsLive(enemy) && (enemy.transform.position - center).sqrMagnitude <= radius * radius) damageTargets.Add(enemy);
            foreach (Enemy enemy in damageTargets) enemiesSystem.DamagePrototypeEnemy(enemy, damage);
        }

        internal void UpdatePrototypeEffects(float dt)
        {
            skillVisuals.Tick(dt);
            personalMagic.Tick(dt);
            for (int i = meteors.Count - 1; i >= 0; i--)
            {
                PendingMeteor meteor = meteors[i];
                if (run.elapsed < meteor.impactAt) continue;
                DamagePrototypeArea(meteor.center, meteor.radius, meteor.damage);
                skillVisuals.MeteorImpact(meteor.center, meteor.radius);
                meteors.RemoveAt(i);
            }
        }

        internal void Reset()
        {
            personalMagic.Reset();
            runSkillRanks.Clear(); meteors.Clear(); forceAttackEffects = false;
            skillVisuals.Clear();
            skillVisuals.Configure(art.boltSprite ?? art.slimeSprite, art.gemSprite ?? art.slimeSprite, art.skillVisualAssets);
        }
    }
}
