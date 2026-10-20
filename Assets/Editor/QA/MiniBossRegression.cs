using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectDM.Editor
{
    public static class MiniBossRegression
    {
        private static int checks;
        private static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); checks++; }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;

        [MenuItem("Project DM/검증/중간보스·처치 파동 검증")]
        public static void RunMenu()
        {
            checks = 0;
            var random = UnityEngine.Random.state;
            float timeScale = Time.timeScale;
            GameObject root = new("Isolated mini boss QA"), player = new("QA player");
            EnemySystem enemies = null; PickupSystem drops = null;
            try
            {
                GameSettings settings = new(); GameTables tables = GameTables.LoadResources(settings);
                Check(PrototypeRules.Validate(tables.balance, tables.skillTable, tables.growthTable) == null, "Mini boss defaults validate");
                tables.balance.miniBossHealthMultiplier = 0;
                Check(PrototypeRules.Validate(tables.balance, tables.skillTable, tables.growthTable) != null, "Invalid mini boss health rejected");
                tables.balance.miniBossHealthMultiplier = 5;
                tables.balance.chestChance = 0; tables.balance.manaAbsorbChance = 0;
                foreach (var row in tables.balance.monsters) row.bonusChance = 0;
                SaveService save = new(tables, new MemoryProgressStore(), true); save.Load();
                ProgressionService growth = new(save, tables); growth.ResetSnapshot();
                PlayerStats stats = new(tables, growth);
                WorldArtAssets art = new() { stage = Resources.Load<StageDefinition>("ProjectDM/Stages/Stage1_Meadow"), runtimeAssets = new() };
                art.runtimeAssets.EnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Prefabs/Enemy.prefab");
                art.runtimeAssets.ChestPickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Prefabs/ChestPickup.prefab");
                art.runtimeAssets.ExperiencePickupPrefabs = new GameObject[5]; art.runtimeAssets.CurrencyPickupPrefabs = new GameObject[5];
                for (int i = 0; i < 5; i++)
                {
                    art.runtimeAssets.ExperiencePickupPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GameContent/Prefabs/ExperiencePickup_{i + 1:00}.prefab");
                    art.runtimeAssets.CurrencyPickupPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GameContent/Prefabs/CurrencyPickup_{i + 1:00}.prefab");
                }
                PlayerWorld world = new(settings, tables, art, stats) { player = player.transform };
                RunMana mana = new(); mana.Reset(1000, 1);
                RunController run = new(settings, save, tables, growth, world, () => { }, () => { }, () => { }, () => { }, () => { }, () => false, mana);
                var pool = root.AddComponent<GameObjectPool>(); var ripple = root.AddComponent<BossDeathRippleVisuals>();
                var boss = new BossMilestone(save, art, world, run);
                drops = new(settings, save, tables, growth, art, world, run, pool, _ => { }, stats);
                int deathEffects = 0;
                enemies = new(settings, save, tables, growth, art, world, run, drops, boss, new GameNotifications(), pool, (_, _, _) => { },
                    (position, finalBoss) => { deathEffects++; ripple.Emit(position, finalBoss); });
                enemies.SpawnPrototypeInitialEnemies(); enemies.SpawnEnemies(59.9f);
                Check(!enemies.enemies.Exists(e => e.rank != EnemyRank.Normal), "No early mini boss");
                enemies.SpawnEnemies(60);
                Enemy mini = enemies.enemies.Find(e => e.rank == EnemyRank.MiniBoss);
                Check(mini != null && (int)mini.kind == enemies.SpawnStage, "Current unlocked monster supplies mini boss artwork");
                Check(Near(mini.hitPoints, tables.balance.monsters[(int)mini.kind].hp * 5) && Near(mini.transform.localScale.x, .65f * 1.35f), "Mini boss health and scale multipliers");
                Check(Near(mini.hitRadiusMultiplier, 1.35f), "Projectile and contact bounds follow mini boss scale");
                enemies.SpawnEnemies(120);
                Check(enemies.enemies.FindAll(e => e.rank == EnemyRank.MiniBoss).Count == 1, "One living mini boss even across another interval");
                Vector3 point = new(0, 0, 0); mini.transform.position = point;
                int kills = save.progress.kills[(int)mini.kind];
                enemies.DamagePrototypeEnemy(mini, mini.hitPoints - 1);
                Check(deathEffects == 0 && drops.pickups.Count == 0, "Nonlethal hit never awards a chest or wave");
                enemies.DamagePrototypeEnemy(mini, 1);
                Check(deathEffects == 1 && ripple.ActiveCount == 1 && save.progress.kills[(int)mini.kind] == kills + 1, "Lethal hit emits one wave and records one actual kill");
                Check(drops.pickups.FindAll(p => p.kind == PickupKind.Chest).Count == 1, "Exactly one guaranteed chest when ordinary chance is zero");
                Check(Near(drops.pickups.Find(p => p.kind == PickupKind.Experience).amount, 3) && Near(drops.pickups.Find(p => p.kind == PickupKind.Currency).amount, 3), "Mini boss ordinary XP and currency are tripled");
                Check(Near(drops.pickups.Find(p => p.kind == PickupKind.Chest).amount, tables.balance.chestCoins * 3), "Guaranteed chest contents also use the reward multiplier");
                int dropCount = drops.pickups.Count; enemies.DamagePrototypeEnemy(mini, 100);
                Check(drops.pickups.Count == dropCount && deathEffects == 1, "Overlapping lethal damage cannot duplicate rewards or waves");
                drops.Reset();
                enemies.SpawnPrototypeEnemy(false);
                Enemy normal = enemies.enemies[^1];
                Check(normal.rank == EnemyRank.Normal && Near(normal.rewardMultiplier, 1) && Near(normal.hitRadiusMultiplier, 1) && Near(normal.hitPoints, tables.balance.monsters[(int)normal.kind].hp), "Pooled regular monster resets boss state and stats");
                enemies.DamagePrototypeEnemy(normal, normal.hitPoints);
                Check(deathEffects == 1 && !drops.pickups.Exists(p => p.kind == PickupKind.Chest), "Normal monsters keep ordinary chest chance and no death wave");
                drops.Reset();
                enemies.SpawnEnemies(120);
                Check(enemies.enemies.Exists(e => e.rank == EnemyRank.MiniBoss), "Next due mini boss arrives after the previous one dies");
                enemies.Reset(); enemies.SpawnEnemies(180);
                Check(enemies.enemies.Count == 0, "Final boss arrival boundary does not add a mini boss");
                enemies.SpawnPrototypeEnemy(false); Enemy futureBoss = enemies.enemies[^1]; futureBoss.rank = EnemyRank.Boss;
                tables.balance.chestChance = 1;
                enemies.DamagePrototypeEnemy(futureBoss, futureBoss.hitPoints);
                Check(deathEffects == 2 && drops.pickups.FindAll(p => p.kind == PickupKind.Chest).Count == 1, "Common boss death path guarantees one chest even at ordinary chance one");
                TestVisuals(ripple, root);
                Check(!save.progress.reachedBoss, "Mini bosses never mark final boss progression");
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/MiniBossRegression-result.txt", $"PASS: {checks} assertions; cadence, scaled stats/collisions, rewards, guaranteed chest, one death, reuse, ripple refraction/render/pause/reset/budget.\n");
                Debug.Log($"Mini boss regression: {checks} assertions PASS.");
            }
            finally
            {
                enemies?.Reset(); drops?.Reset(); Object.DestroyImmediate(root); Object.DestroyImmediate(player);
                UnityEngine.Random.state = random; Time.timeScale = timeScale;
            }
        }

        private static void TestVisuals(BossDeathRippleVisuals ripple, GameObject root)
        {
            ripple.Clear(); var settings = Resources.Load<BossDeathRippleSettings>("ProjectDM/Effects/BossDeathRipple");
            Check(settings != null && settings.material != null && !ShaderUtil.ShaderHasError(settings.material.shader), "Built-in refraction shader and referenced material load");
            var random = UnityEngine.Random.state;
            ripple.Emit(Vector3.zero, false); ripple.Tick(.35f);
            MeshRenderer active = Array.Find(root.GetComponentsInChildren<MeshRenderer>(), r => r.gameObject.name == "보스 처치 공간 파동");
            MaterialPropertyBlock before = new(); active.GetPropertyBlock(before); float progress = before.GetFloat("_Progress");
            ripple.Tick(0); active.GetPropertyBlock(before);
            Check(Near(before.GetFloat("_Progress"), progress), "Zero scaled delta freezes ripple progress");
            Check(active.transform.position == Vector3.zero && Near(active.transform.localScale.x, settings.miniBossRadius * 2), "Ripple remains at world death origin and authored radius");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) Render(ripple, root);
            ripple.Clear();
            for (int i = 0; i < settings.maximumActive + 4; i++) ripple.Emit(Vector3.zero, false);
            Check(ripple.ActiveCount == settings.maximumActive, "Overlapping deaths obey the visual budget");
            ripple.Tick(settings.duration + 1);
            Check(ripple.ActiveCount == 0, "Completed ripples are pooled");
            ripple.Emit(Vector3.zero, true);
            Check(ripple.ActiveCount == 1, "Pooled wave restarts for the boss variant");
            ripple.Clear(); Check(ripple.ActiveCount == 0, "Restart clears active waves");
            Check(random.Equals(UnityEngine.Random.state), "Death visuals never consume combat RNG");
        }

        private static void Render(BossDeathRippleVisuals ripple, GameObject root)
        {
            GameObject background = new("Ripple QA background"), cameraObject = new("Ripple QA camera");
            var texture = new Texture2D(128, 128); var target = new RenderTexture(960, 640, 24);
            var plain = new Texture2D(960, 640, TextureFormat.RGB24, false); var wave = new Texture2D(960, 640, TextureFormat.RGB24, false);
            Sprite sprite = null; var previous = RenderTexture.active;
            try
            {
                for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                    texture.SetPixel(x, y, (x / 5 + y / 5) % 2 == 0 ? new Color(.14f,.23f,.18f) : new Color(.26f,.32f,.25f));
                texture.filterMode = FilterMode.Point; texture.Apply();
                sprite = Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(.5f,.5f), 16);
                var body = background.AddComponent<SpriteRenderer>(); body.sprite = sprite; body.sortingOrder = -10; background.transform.localScale = new Vector3(2,2,1);
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                background.layer = 31;
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = 3.8f; camera.aspect = 1.5f; camera.transform.position = Vector3.back * 10;
                camera.cullingMask = 1 << 31; camera.targetTexture = target;
                void Capture(Texture2D image)
                { camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0,0,960,640),0,0); image.Apply(); }
                ripple.Clear(); Capture(plain);
                ripple.Emit(Vector3.zero, false); ripple.Tick(.48f); Capture(wave);
                Color32[] a = plain.GetPixels32(), b = wave.GetPixels32(); int changed = 0;
                for (int i = 0; i < a.Length; i++) if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 8) changed++;
                Check(changed > 100 && changed < a.Length / 3, "GPU render visibly refracts/tints a bounded annular region");
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/BossDeathRipple-preview.png", wave.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; Object.DestroyImmediate(background); Object.DestroyImmediate(cameraObject);
                if (sprite != null) Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(plain); Object.DestroyImmediate(wave);
            }
        }
    }
}
