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
        private readonly Action<Vector3, float> showManaGain;

        internal PickupSystem(GameSettings settings, SaveService save, GameTables tables, ProgressionService progression, WorldArtAssets art, PlayerWorld playerWorld, RunController run, GameObjectPool objectPool, Action<float> addExperience, PlayerStats playerStats, Action<Vector3, float> showManaGain = null)
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
            this.showManaGain = showManaGain;
        }

        internal readonly List<Pickup> pickups = new();
        readonly double[] currencyBonuses = new double[5];
        SpriteQuadBatch renderBatch;
        readonly Dictionary<GameObject,FrameSource> frames = new();
        int nextSerial, vacuumThrough;
        int visibleCount;
        Sprite moteSprite;
        sealed class FrameSource
        {
            internal GameObject sample;
            internal SpriteRenderer renderer;
            internal AnimationClip clip;
            internal Sprite sprite;
            internal PickupAmbientVfx ambient;
        }
        internal int VisiblePickupCount => visibleCount;
        FrameSource Frames(GameObject prefab)
        {
            if(frames.TryGetValue(prefab,out var result))return result;
            var source=prefab.GetComponentInChildren<SpriteRenderer>(true);
            var animator=prefab.GetComponentInChildren<Animator>(true);
            result=new FrameSource{sprite=source!=null?source.sprite:null,ambient=prefab.GetComponentInChildren<PickupAmbientVfx>(true)};
            if(animator!=null && animator.runtimeAnimatorController!=null && animator.runtimeAnimatorController.animationClips.Length>0)
            {
                result.sample=UnityEngine.Object.Instantiate(prefab);result.sample.name="드랍 애니메이션 공유 샘플";result.sample.hideFlags=HideFlags.HideAndDontSave;result.sample.SetActive(false);
                result.renderer=result.sample.GetComponentInChildren<SpriteRenderer>(true);
                result.clip=animator.runtimeAnimatorController.animationClips[0];
            }
            frames.Add(prefab,result);return result;
        }
        internal void CreatePickup(string name, Vector3 position, Sprite sprite, PickupKind kind, float amount, float scale, int grade = 1)
        {
            GameObject prefab = SelectPickupPrefab(kind, grade);
            if(prefab==null){Debug.LogWarning($"드랍 프리팹 누락: {kind}");return;}
            if(kind!=PickupKind.Chest)
            {
                var frame=Frames(prefab);
                float multiplier=kind==PickupKind.Experience?settings.experiencePickupScaleMultiplier:kind==PickupKind.Currency?settings.currencyPickupScaleMultiplier:settings.manaPickupScaleMultiplier;
                var item=new Pickup{batched=true,kind=kind,grade=grade,amount=amount,baseScale=scale*multiplier,serial=++nextSerial,bornAt=Time.time,
                    batchSprite=kind==PickupKind.Magnet?Resources.Load<Sprite>("ProjectDM/Skills/Magnet"):frame.sprite ?? sprite,floatPhase=Random.value};
                item.Position=position;item.VisualScale=item.baseScale;BeginPickupFountain(item,position);pickups.Add(item);return;
            }
            GameObject pickupObject = objectPool.Rent(prefab);
            pickupObject.name = name;
            pickupObject.transform.position = position;
            float pickupScaleMultiplier = kind switch
            {
                PickupKind.Experience => settings.experiencePickupScaleMultiplier,
                PickupKind.Currency => settings.currencyPickupScaleMultiplier,
                PickupKind.Mana => settings.manaPickupScaleMultiplier,
                PickupKind.Magnet => settings.manaPickupScaleMultiplier,
                _ => settings.chestPickupScaleMultiplier
            };
            pickupObject.transform.localScale = Vector3.one * (scale * pickupScaleMultiplier);
            SpriteRenderer renderer = pickupObject.GetComponentInChildren<SpriteRenderer>();
            if(kind==PickupKind.Magnet || kind==PickupKind.Mana){var selected=kind==PickupKind.Magnet?Resources.Load<Sprite>("ProjectDM/Skills/Magnet"):sprite;if(selected!=null)renderer.sprite=selected;renderer.color=Color.white;}
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
            if(kind==PickupKind.Magnet && animator!=null)animator.enabled=false;
            else if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.enabled=true;
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
                serial = ++nextSerial,
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
            pickup.VisualScale=pickup.baseScale*.7f;
        }

        internal GameObject SelectPickupPrefab(PickupKind kind, int grade = 1)
        {
            if (kind == PickupKind.Mana || kind == PickupKind.Magnet) return art.runtimeAssets.ManaPickupPrefab;
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
            if (dt <= 0 || run.runEnded || run.mana.IsDepleted) return;
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                Pickup pickup = pickups[i];
                if (!pickup.batched && pickup.transform == null)
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
                        if(pickup.transform!=null)objectPool.Return(pickup.transform.gameObject);
                        pickups.RemoveAt(i);
                    }
                    continue;
                }

                if(pickup.serial>0 && pickup.serial<=vacuumThrough){pickup.vacuum=true;pickup.isLaunching=false;pickup.VisualScale=pickup.baseScale;}
                if (pickup.isLaunching)
                {
                    UpdatePickupFountain(pickup, dt);
                    continue;
                }

                float distance = Vector2.Distance(playerWorld.player.position, pickup.Position);
                float magnetRadius = playerStats.MagnetRadius;
                if (pickup.vacuum || (magnetRadius > 0 && distance < magnetRadius && pickup.kind != PickupKind.Magnet))
                {
                    pickup.Position = Vector3.MoveTowards(pickup.Position, playerWorld.player.position,
                        (pickup.vacuum ? 16f + Mathf.Min(distance,30) : 2.5f + (1 - distance / magnetRadius) * 12f) * dt);
                }

                if (distance < 0.28f)
                {
                    if (pickup.kind == PickupKind.Chest)
                    {
                        PlayChestInteraction(pickup);
                        continue;
                    }

                    Collect(pickup);
                    if(pickup.transform!=null)objectPool.Return(pickup.transform.gameObject);
                    pickups.RemoveAt(i);
                }
            }
            DrawBatchedPickups();
        }
        void DrawBatchedPickups()
        {
            renderBatch ??= new SpriteQuadBatch("전체 드랍 일괄 그리기",1);renderBatch.Begin();visibleCount=0;
            moteSprite ??= Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),new Vector2(.5f,.5f),Texture2D.whiteTexture.width);
            foreach(var frame in frames.Values)
                if(frame.clip!=null && frame.sample!=null){frame.clip.SampleAnimation(frame.sample,Mathf.Repeat(Time.time,Mathf.Max(.01f,frame.clip.length)));frame.sprite=frame.renderer.sprite;}
            foreach(var p in pickups)
            {
                if(!p.batched || p.removed)continue;
                var source=Frames(SelectPickupPrefab(p.kind,p.grade));
                Sprite sprite=p.kind==PickupKind.Magnet?p.batchSprite:source.sprite ?? p.batchSprite;
                if(sprite==null)continue;
                var curve=p.kind==PickupKind.Currency?settings.currencyPickupFloatYCurve:settings.experiencePickupFloatYCurve;
                float phase=Mathf.Repeat(Time.time/Mathf.Max(.05f,settings.collectibleFloatLoopDuration)+p.floatPhase,1);
                Vector3 point=p.Position+Vector3.up*(curve?.Evaluate(phase)??0)*settings.collectibleFloatYAmplitude;
                renderBatch.Add(sprite,point,(Vector2)sprite.bounds.size*p.VisualScale,0,Color.white);
                visibleCount++;
                // Keep the per-drop rising motes, using the same mesh rather than thousands of particle components.
                var ambient=source.ambient;
                if(ambient!=null && ambient.emissionRate>0)
                {
                    float age=Time.time-p.bornAt-.18f,life=Mathf.Max(.1f,ambient.lifetime),height=sprite.bounds.size.y*p.VisualScale;
                    int count=Mathf.Min(ambient.maximumParticles,Mathf.CeilToInt(ambient.emissionRate*life));
                    for(int m=0;m<count;m++)
                    {
                        float offset=m/ambient.emissionRate;if(age<offset)continue;
                        float motePhase=Mathf.Repeat((age-offset)/life+p.floatPhase,1);
                        float spread=Mathf.Sin(p.serial*2.39996f+m*4.13f)*sprite.bounds.size.x*p.VisualScale*.35f;
                        Color tint=ambient.tint;tint.a=ambient.opacity*Mathf.Sin(motePhase*Mathf.PI);
                        Vector3 at=point+new Vector3(spread,(-.55f+motePhase*ambient.riseSpeed*life)*height);
                        renderBatch.Add(moteSprite,at,Vector2.one*height*.075f*(.55f+Mathf.Sin(motePhase*Mathf.PI)*.45f),0,tint);
                    }
                }
            }
            renderBatch.End();
        }
        internal void Dispose()
        {
            renderBatch?.Dispose();foreach(var frame in frames.Values)if(frame.sample!=null)UnityEngine.Object.Destroy(frame.sample);frames.Clear();if(moteSprite!=null)UnityEngine.Object.Destroy(moteSprite);
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
            if (run.runEnded || run.mana.IsDepleted) return;
            if(pickup.removed)return;
            pickup.removed=true;
            if(pickup.kind==PickupKind.Magnet)
            {
                vacuumThrough=nextSerial;
                return;
            }
            if (pickup.kind == PickupKind.Experience) { addExperience(pickup.amount); return; }
            if (pickup.kind == PickupKind.Mana)
            {
                float added = run.mana.Absorb(pickup.amount);
                if (added > 0) showManaGain?.Invoke(playerWorld.player.position, added);
                return;
            }
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
            pickup.Position = position;

            float popScale = Mathf.Lerp(.7f, 1f, easedProgress) + Mathf.Sin(progress * Mathf.PI) * .08f;
            pickup.VisualScale=pickup.baseScale*popScale;
            if (progress >= 1f)
            {
                pickup.Position = pickup.launchDestination;
                pickup.VisualScale=pickup.baseScale;
                pickup.isLaunching = false;
            }
        }

        internal void SpawnPrototypeLoot(Vector3 position, int kind, float rewardMultiplier = 1, bool guaranteedChest = false)
        {
            PrototypeMonster row = tables.balance.monsters[kind];
            // Unlocks follow actual permanent kill progress mid-run; purchased bonuses use the run snapshot.
            int stage = PrototypeRules.Stage(tables.balance, save.progress);
            int xpGrade = LootGradeRules.ResolveGrade(tables.balance, row.experienceGrade, stage,
                progression.PermanentAmount("experienceGradeChance"), Random.value);
            for (int grade = 2; grade <= 5; grade++) currencyBonuses[grade - 1] = progression.PermanentAmount(LootGradeRules.CurrencyGrowthId(grade));
            int coinGrade = row.currencyChances == null
                ? LootGradeRules.ResolveGrade(tables.balance, row.currencyGrade, stage, progression.PermanentAmount("currencyGradeChance"), Random.value)
                : LootGradeRules.ResolveCurrencyGrade(tables.balance, row, stage, currencyBonuses, Random.value);
            float xp = (float)LootGradeRules.Reward(tables.balance, row, xpGrade, false, progression.PermanentAmount("xp"), false, 0);
            int coins = (int)LootGradeRules.Reward(tables.balance, row, coinGrade, true, progression.PermanentAmount("yield"),
                Random.value < row.bonusChance, Random.value);
            xp *= rewardMultiplier;
            if (rewardMultiplier != 1) coins = PrototypeRules.StochasticCoins(coins * (double)rewardMultiplier, Random.value);
            CreatePickup($"{LootGradeRules.Name(tables.balance, xpGrade, false)} {xpGrade}등급 · {xp:0.##}", position, art.gemSprite, PickupKind.Experience, xp, .28f, xpGrade);
            if (coins > 0) CreatePickup($"{LootGradeRules.Name(tables.balance, coinGrade, true)} {coinGrade}등급 · {coins}", position, art.coinSprite, PickupKind.Currency, coins, .23f, coinGrade);
            float magnetChance = Mathf.Clamp01(progression.PermanentAmount("magnetDropChance"));
            if(magnetChance>0 && Random.value<magnetChance)
                CreatePickup("자석 · 맵 전체 아이템 끌어오기",position,art.gemSprite,PickupKind.Magnet,1,.3f);
            if (guaranteedChest || Random.value < tables.balance.chestChance)
            {
                int chest = PrototypeRules.StochasticCoins(tables.balance.chestCoins * (1.0 + progression.PermanentAmount("yield")) * rewardMultiplier, Random.value);
                if (guaranteedChest) chest = Mathf.Max(1, chest);
                if (chest > 0) CreatePickup("보물상자", position + Vector3.up * .2f, art.coinSprite, PickupKind.Chest, chest, .42f);
            }
            // Preserve the independent on-death chance; recovery happens only on collection.
            double chance = PrototypeRules.Chance(tables.balance.ResolvedManaAbsorbChance, progression.PermanentAmount("extension"), 0);
            if (!run.runEnded && tables.balance.ResolvedManaAbsorbAmount > 0 && (chance >= 1 || Random.value < chance))
                CreatePickup($"마나 · +{tables.balance.ResolvedManaAbsorbAmount:0.##} MP", position, null,
                    PickupKind.Mana, tables.balance.ResolvedManaAbsorbAmount, .28f);
        }

        internal void Reset()
        {
            foreach (Pickup pickup in pickups) if (pickup.transform != null) if(pickup.transform!=null)objectPool.Return(pickup.transform.gameObject);
            pickups.Clear();renderBatch?.Clear();visibleCount=0;nextSerial=vacuumThrough=0;
        }

    }
}
