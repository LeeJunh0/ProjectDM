using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Owns monster population, movement, damage, death and kill rewards.</summary>
    internal sealed class EnemySystem
    {
        private readonly GameSettings settings;
        private readonly SaveService save;
        private readonly GameTables tables;
        private readonly ProgressionService progression;
        private readonly WorldArtAssets art;
        private readonly PlayerWorld playerWorld;
        private readonly RunController run;
        private readonly PickupSystem pickupsSystem;
        private readonly BossMilestone boss;
        private readonly GameNotifications notifications;
        private readonly GameObjectPool objectPool;
        private readonly PrototypeSkillVisuals visuals;
        private readonly Action<Vector3, float, CombatDamageKind> showDamage;

        internal EnemySystem(GameSettings settings, SaveService save, GameTables tables, ProgressionService progression, WorldArtAssets art, PlayerWorld playerWorld, RunController run, PickupSystem pickupsSystem, BossMilestone boss, GameNotifications notifications, GameObjectPool objectPool, PrototypeSkillVisuals visuals, Action<Vector3, float, CombatDamageKind> showDamage)
        {
            this.settings = settings;
            this.save = save;
            this.tables = tables;
            this.progression = progression;
            this.art = art;
            this.playerWorld = playerWorld;
            this.run = run;
            this.pickupsSystem = pickupsSystem;
            this.boss = boss;
            this.notifications = notifications;
            this.objectPool = objectPool;
            this.visuals = visuals;
            this.showDamage = showDamage;
        }

        internal readonly List<Enemy> enemies = new();

        internal float nextSpawn;

        internal void SpawnEnemies(float time)
        {
            SpawnPrototypeRefill(time);
        }

        internal void UpdateEnemies(float dt)
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = enemies[i];
                if (enemy.transform == null)
                {
                    enemies.RemoveAt(i);
                    continue;
                }

                Vector2 direction = ((Vector2)playerWorld.player.position - (Vector2)enemy.transform.position).normalized;
                enemy.transform.position += (Vector3)(direction * enemy.speed * dt);
                enemy.animationTime += dt;
                if (enemy.alternateSprite != null && enemy.animationTime > 0.16f)
                {
                    enemy.animationTime = 0f;
                    enemy.showAlternate = !enemy.showAlternate;
                    enemy.renderer.sprite = enemy.showAlternate ? enemy.alternateSprite : enemy.baseSprite;
                }
                if (enemy.renderer != null && Mathf.Abs(direction.x) > 0.01f)
                {
                    enemy.renderer.flipX = direction.x > 0f;
                }
            }
        }

        internal int debugStageOverride = -1;

        internal int SpawnStage => debugStageOverride >= 0 ? debugStageOverride : PrototypeRules.Stage(tables.balance, save.progress);

        internal int FieldTarget => tables.balance.stages[SpawnStage].target + Mathf.FloorToInt(progression.PermanentAmount("population"));

        internal int FieldCap => tables.balance.stages[SpawnStage].cap + Mathf.FloorToInt(progression.PermanentAmount("population"));

        internal void SpawnPrototypeInitialEnemies()
        {
            PrototypeStage stage = tables.balance.stages[SpawnStage];
            int count = Mathf.Min(FieldCap, Mathf.CeilToInt(stage.initial * (float)FieldTarget / stage.target));
            for (int i = 0; i < count; i++) SpawnPrototypeEnemy(true);
            nextSpawn = tables.balance.stages[SpawnStage].refillSeconds / (1 + progression.PermanentAmount("respawn"));
        }

        internal void SpawnPrototypeRefill(float time)
        {
            if (time < nextSpawn || boss.bossArrived) return;
            PrototypeStage stage = tables.balance.stages[SpawnStage];
            nextSpawn = time + stage.refillSeconds / (1f + progression.PermanentAmount("respawn"));
            int count = Mathf.Min(stage.batch, Mathf.Min(FieldTarget, FieldCap) - enemies.Count);
            for (int i = 0; i < count; i++) SpawnPrototypeEnemy(false);
        }

        internal Vector3 PrototypeSpawnPosition(bool initial)
        {
            float min = initial ? tables.balance.initialSpawnMin : tables.balance.refillSpawnMin;
            float max = initial ? tables.balance.initialSpawnMax : tables.balance.refillSpawnMax;
            if (!settings.useTableSpawnDistances)
            {
                min = settings.monsterSpawnArea != null ? settings.monsterSpawnArea.MinimumDistance : settings.monsterSpawnMinimumDistance;
                max = settings.monsterSpawnArea != null ? settings.monsterSpawnArea.MaximumDistance : settings.monsterSpawnMaximumDistance;
            }
            Bounds bounds = new Bounds(settings.fieldBounds != null ? settings.fieldBounds.transform.position : Vector3.zero,
                settings.fieldBounds != null ? (Vector3)settings.fieldBounds.Size : (Vector3)settings.fieldSize);
            if (settings.dungeonFloor != null && settings.dungeonFloor.TryGetWorldBounds(out Bounds floor)) bounds = floor;
            // Retry before clamping so corners do not funnel all refills into one edge point.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                Vector3 candidate = playerWorld.player.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(min, max);
                if (candidate.x >= bounds.min.x + .5f && candidate.x <= bounds.max.x - .5f &&
                    candidate.y >= bounds.min.y + .5f && candidate.y <= bounds.max.y - .5f) return candidate;
            }
            Vector2 fallbackDirection = Random.insideUnitCircle.normalized;
            if (fallbackDirection == Vector2.zero) fallbackDirection = Vector2.right;
            Vector3 fallback = playerWorld.player.position + (Vector3)fallbackDirection * max;
            fallback.x = Mathf.Clamp(fallback.x, bounds.min.x + .5f, bounds.max.x - .5f);
            fallback.y = Mathf.Clamp(fallback.y, bounds.min.y + .5f, bounds.max.y - .5f);
            return fallback;
        }

        internal void SpawnPrototypeEnemy(bool initial)
        {
            int[] weights = tables.balance.stages[SpawnStage].weights;
            int sum = 0;
            foreach (int weight in weights) sum += weight;
            int roll = Random.Range(0, sum), kind = 0;
            while (kind < weights.Length - 1 && roll >= weights[kind]) { roll -= weights[kind]; kind++; }
            PrototypeMonster row = tables.balance.monsters[kind];
            GameObject instance = objectPool.Rent(art.runtimeAssets.EnemyPrefab);
            instance.name = row.name;
            instance.transform.position = PrototypeSpawnPosition(initial);
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * (kind == 4 ? .82f : kind == 3 ? .78f : .65f);
            SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>();
            Animator animator = instance.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = kind == 0 ? art.slimeController : kind == 1 ? art.skeletonController : null;
                animator.enabled = animator.runtimeAnimatorController != null;
                animator.applyRootMotion = false;
                if (animator.enabled) { animator.Rebind(); animator.Update(0f); }
            }
            renderer.sprite = art.MonsterSprite((MonsterKind)kind) ?? art.slimeSprite;
            renderer.color = Color.white;
            renderer.flipX = false;
            renderer.sortingOrder = 2;
            Sprite alternate = kind switch { 2 => art.goblinFrame2, 3 => art.mushroomFrame2, 4 => art.boarFrame2, _ => null };
            enemies.Add(new Enemy { kind = (MonsterKind)kind, transform = instance.transform, renderer = renderer,
                baseSprite = renderer.sprite, alternateSprite = alternate, hitPoints = row.hp, speed = row.speed });
        }

        internal static bool IsLive(Enemy enemy) => enemy != null && !enemy.dead && enemy.transform != null;

        internal void DamagePrototypeEnemy(Enemy enemy, float damage)
        {
            DamagePrototypeEnemyWithPresentation(enemy, damage, CombatDamageKind.Attack);
        }

        internal void DamagePrototypeEnemyWithPresentation(Enemy enemy, float damage, CombatDamageKind presentationKind)
        {
            if (!IsLive(enemy)) return;
            showDamage(enemy.transform.position, damage, presentationKind);
            enemy.hitPoints -= damage;
            if (enemy.hitPoints > 0) return;
            enemy.dead = true; // Single owner of death, rewards and pool return, including AoE overlaps.
            Vector3 position = enemy.transform.position;
            int kind = (int)enemy.kind;
            run.monsterKillCounts[kind]++;
            int oldStage = PrototypeRules.Stage(tables.balance, save.progress);
            if (save.progress.kills[kind] < int.MaxValue) save.progress.kills[kind]++;
            int newStage = PrototypeRules.Stage(tables.balance, save.progress);
            if (newStage > oldStage) notifications.Notice(tables.balance.monsters[newStage].name + " 등장 해금! 기존 몬스터와 함께 출현합니다.");
            pickupsSystem.SpawnPrototypeLoot(position, kind);
            // On-death roll is separate from on-hit effects and also runs for effect kills.
            double chance = PrototypeRules.Chance(tables.balance.timeRewardChance, progression.PermanentAmount("extension"), 0);
            if (chance >= 1 || Random.value < chance)
            {
                float oldDeadline = run.activeRunDuration;
                run.activeRunDuration = (float)PrototypeRules.ExtendDeadline(run.activeRunDuration, tables.balance.timeRewardSeconds, tables.balance.bossElapsedSeconds);
                float added = run.activeRunDuration - oldDeadline;
                run.timeRewardsThisRun += added;
                if (added > 0) visuals.TimeReward(position, added);
            }
            enemies.Remove(enemy);
            objectPool.Return(enemy.transform.gameObject);
            save.MarkPrototypeProgressDirty();
        }

        internal void Reset()
        {
            foreach (Enemy enemy in enemies) if (enemy.transform != null) objectPool.Return(enemy.transform.gameObject);
            enemies.Clear(); nextSpawn = 0; debugStageOverride = -1;
        }
    }
}
