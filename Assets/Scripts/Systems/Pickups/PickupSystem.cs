using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectDM
{
    /// <summary>Owns pooled drops, fountain motion, chest interaction and collection.</summary>
    internal sealed class PickupSystem
    {
        private readonly GameSettings settings;
        private readonly SaveService save;
        private readonly GameTables tables;
        private readonly ProgressionService progression;
        private readonly WorldArtAssets art;
        private readonly PlayerWorld playerWorld;
        private readonly RunController run;
        private readonly GameObjectPool objectPool;
        private readonly Action<float> addExperience;
        private readonly PlayerStats playerStats;

        internal PickupSystem(GameSettings settings, SaveService save, GameTables tables, ProgressionService progression, WorldArtAssets art, PlayerWorld playerWorld, RunController run, GameObjectPool objectPool, Action<float> addExperience, PlayerStats playerStats)
        {
            this.settings = settings;
            this.save = save;
            this.tables = tables;
            this.progression = progression;
            this.art = art;
            this.playerWorld = playerWorld;
            this.run = run;
            this.objectPool = objectPool;
            this.addExperience = addExperience;
            this.playerStats = playerStats;
        }

        internal readonly List<Pickup> pickups = new();

        internal void CreatePickup(string name, Vector3 position, Sprite sprite, PickupKind kind, float amount, float scale, int grade = 1)
        {
            GameObject prefab = SelectPickupPrefab(kind, grade);
            if (prefab == null)
            {
                Debug.LogWarning($"Project DM could not spawn {kind}: no pooled prefab is configured.");
                return;
            }
            GameObject pickupObject = objectPool.Rent(prefab);
            pickupObject.name = name;
            pickupObject.transform.position = position;
            float pickupScaleMultiplier = kind == PickupKind.Experience
                ? settings.experiencePickupScaleMultiplier
                : kind == PickupKind.Currency
                    ? settings.currencyPickupScaleMultiplier
                    : settings.chestPickupScaleMultiplier;
            pickupObject.transform.localScale = Vector3.one * (scale * pickupScaleMultiplier);
            SpriteRenderer renderer = pickupObject.GetComponentInChildren<SpriteRenderer>();
            renderer.sortingOrder = 1;
            PickupFloatVisual floatVisual = pickupObject.GetComponentInChildren<PickupFloatVisual>();
            if (floatVisual != null)
            {
                AnimationCurve floatCurve = kind == PickupKind.Currency
                    ? settings.currencyPickupFloatYCurve
                    : settings.experiencePickupFloatYCurve;
                // A randomized phase keeps nearby pickups from bobbing in lockstep.
                floatVisual.Configure(floatCurve, settings.collectibleFloatYAmplitude, settings.collectibleFloatLoopDuration, Random.value);
            }
            Animator animator = pickupObject.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.applyRootMotion = false;
                animator.Rebind();
                animator.Play("Loop", 0, 0f);
                animator.speed = kind == PickupKind.Chest ? 0f : 1f;
            }
            else
            {
                // Keeps old catalog/builds playable while preserving a static prefab-assigned variant sprite.
                if (renderer.sprite == null)
                {
                    renderer.sprite = sprite;
                }
            }
            Pickup pickup = new()
            {
                transform = pickupObject.transform,
                kind = kind,
                grade = kind == PickupKind.Chest ? 0 : grade,
                amount = amount,
                baseScale = scale * pickupScaleMultiplier
            };
            BeginPickupFountain(pickup, position);
            pickups.Add(pickup);
        }

        internal void BeginPickupFountain(Pickup pickup, Vector3 origin)
        {
            if (pickup.kind == PickupKind.Chest)
            {
                return;
            }

            Vector2 direction = Random.insideUnitCircle;
            if (direction.sqrMagnitude < .001f)
            {
                direction = Vector2.right;
            }

            float distance = Random.Range(settings.pickupFountainDistance * .62f, settings.pickupFountainDistance);
            pickup.isLaunching = true;
            pickup.launchOrigin = origin;
            pickup.launchDestination = origin + (Vector3)(direction.normalized * distance);
            pickup.launchElapsed = 0f;
            pickup.launchDuration = Mathf.Max(.05f, settings.pickupFountainDuration * Random.Range(.82f, 1.12f));
            pickup.launchArcHeight = settings.pickupFountainArcHeight * Random.Range(.78f, 1.18f);
            pickup.transform.localScale = Vector3.one * (pickup.baseScale * .7f);
        }

        internal GameObject SelectPickupPrefab(PickupKind kind, int grade = 1)
        {
            if (kind == PickupKind.Chest)
            {
                return art.runtimeAssets.ChestPickupPrefab;
            }

            GameObject[] variants = kind == PickupKind.Currency
                ? art.runtimeAssets.CurrencyPickupPrefabs
                : art.runtimeAssets.ExperiencePickupPrefabs;
            int index = LootGradeRules.PrefabIndex(tables.balance, grade);
            return variants != null && index >= 0 && index < variants.Length ? variants[index] : null;
        }

        internal void UpdatePickups(float dt)
        {
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                Pickup pickup = pickups[i];
                if (pickup.transform == null)
                {
                    pickups.RemoveAt(i);
                    continue;
                }

                if (pickup.isInteracting)
                {
                    pickup.interactionRemaining -= dt;
                    if (pickup.interactionRemaining <= 0f)
                    {
                        Collect(pickup);
                        objectPool.Return(pickup.transform.gameObject);
                        pickups.RemoveAt(i);
                    }
                    continue;
                }

                if (pickup.isLaunching)
                {
                    UpdatePickupFountain(pickup, dt);
                    continue;
                }

                float distance = Vector2.Distance(playerWorld.player.position, pickup.transform.position);
                float magnetRadius = playerStats.MagnetRadius;
                if (distance < magnetRadius)
                {
                    pickup.transform.position = Vector3.MoveTowards(pickup.transform.position, playerWorld.player.position, (2.5f + (1 - distance / magnetRadius) * 12f) * dt);
                }

                if (distance < 0.28f)
                {
                    if (pickup.kind == PickupKind.Chest)
                    {
                        PlayChestInteraction(pickup);
                        continue;
                    }

                    Collect(pickup);
                    objectPool.Return(pickup.transform.gameObject);
                    pickups.RemoveAt(i);
                }
            }
        }

        internal void PlayChestInteraction(Pickup pickup)
        {
            pickup.isInteracting = true;
            pickup.interactionRemaining = settings.chestInteractionDuration;
            Animator animator = pickup.transform.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.speed = 1f;
                animator.Rebind();
                animator.Play("Loop", 0, 0f);
            }

            PickupInteractionVisual interactionVisual = pickup.transform.GetComponentInChildren<PickupInteractionVisual>();
            if (interactionVisual != null)
            {
                interactionVisual.Play(settings.chestInteractionYCurve, settings.chestInteractionScaleCurve, settings.chestInteractionDuration);
            }
        }

        internal void Collect(Pickup pickup)
        {
            if (pickup.kind == PickupKind.Experience) { addExperience(pickup.amount); return; }
            int currency = (int)System.Math.Min(int.MaxValue, System.Math.Max(0, System.Math.Round((double)pickup.amount)));
            run.runCurrency = (int)System.Math.Min(int.MaxValue, (long)run.runCurrency + currency);
            save.metaCurrency = (int)System.Math.Min(int.MaxValue, (long)save.metaCurrency + currency);
            save.SaveMetaProgress();
        }

        internal static void UpdatePickupFountain(Pickup pickup, float dt)
        {
            pickup.launchElapsed += dt;
            float progress = Mathf.Clamp01(pickup.launchElapsed / pickup.launchDuration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            Vector3 position = Vector3.LerpUnclamped(pickup.launchOrigin, pickup.launchDestination, easedProgress);
            position.y += 4f * pickup.launchArcHeight * progress * (1f - progress);
            pickup.transform.position = position;

            float popScale = Mathf.Lerp(.7f, 1f, easedProgress) + Mathf.Sin(progress * Mathf.PI) * .08f;
            pickup.transform.localScale = Vector3.one * (pickup.baseScale * popScale);
            if (progress >= 1f)
            {
                pickup.transform.position = pickup.launchDestination;
                pickup.transform.localScale = Vector3.one * pickup.baseScale;
                pickup.isLaunching = false;
            }
        }

        internal void SpawnPrototypeLoot(Vector3 position, int kind)
        {
            PrototypeMonster row = tables.balance.monsters[kind];
            // Unlocks follow actual permanent kill progress mid-run; purchased bonuses use the run snapshot.
            int stage = PrototypeRules.Stage(tables.balance, save.progress);
            int xpGrade = LootGradeRules.ResolveGrade(tables.balance, row.experienceGrade, stage,
                progression.PermanentAmount("experienceGradeChance"), Random.value);
            double[] currencyBonuses = new double[5];
            for (int grade = 2; grade <= 5; grade++) currencyBonuses[grade - 1] = progression.PermanentAmount(LootGradeRules.CurrencyGrowthId(grade));
            int coinGrade = row.currencyChances == null
                ? LootGradeRules.ResolveGrade(tables.balance, row.currencyGrade, stage, progression.PermanentAmount("currencyGradeChance"), Random.value)
                : LootGradeRules.ResolveCurrencyGrade(tables.balance, row, stage, currencyBonuses, Random.value);
            float xp = (float)LootGradeRules.Reward(tables.balance, row, xpGrade, false, progression.PermanentAmount("xp"), false, 0);
            int coins = (int)LootGradeRules.Reward(tables.balance, row, coinGrade, true, progression.PermanentAmount("yield"),
                Random.value < row.bonusChance, Random.value);
            CreatePickup($"{LootGradeRules.Name(tables.balance, xpGrade, false)} {xpGrade}등급 · {xp:0.##}", position, art.gemSprite, PickupKind.Experience, xp, .28f, xpGrade);
            if (coins > 0) CreatePickup($"{LootGradeRules.Name(tables.balance, coinGrade, true)} {coinGrade}등급 · {coins}", position, art.coinSprite, PickupKind.Currency, coins, .23f, coinGrade);
            if (Random.value < tables.balance.chestChance)
            {
                int chest = PrototypeRules.StochasticCoins(tables.balance.chestCoins * (1.0 + progression.PermanentAmount("yield")), Random.value);
                if (chest > 0) CreatePickup("보물상자", position + Vector3.up * .2f, art.coinSprite, PickupKind.Chest, chest, .42f);
            }
        }

        internal void Reset()
        {
            foreach (Pickup pickup in pickups) if (pickup.transform != null) objectPool.Return(pickup.transform.gameObject);
            pickups.Clear();
        }
    }
}
