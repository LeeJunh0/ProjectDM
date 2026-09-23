using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>
    /// Scene-level composition root for Project DM. It initializes Addressables, then starts one run.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        // Keep the legacy key so existing incremental progress remains intact.
        private const string MetaCurrencyKey = "PROJECT_DM_META_GOLD";
        private const string DamageKey = "PROJECT_DM_DAMAGE_LEVEL";
        private const string HasteKey = "PROJECT_DM_HASTE_LEVEL";
        private const string FortuneKey = "PROJECT_DM_FORTUNE_LEVEL";

        [Header("Scene Layout (Edit Mode Preview)")]
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private GameFieldBounds fieldBounds;
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private MonsterSpawnAreaPreview monsterSpawnArea;
        [SerializeField] private PlayerMovementAreaPreview playerMovementArea;
        [SerializeField] private DungeonFloorTilemap dungeonFloor;
        [SerializeField, HideInInspector] private Vector2 fieldSize = new(25f, 17f);

        [SerializeField, HideInInspector] private Vector2 playerMovementBounds = new(8.4f, 4.8f);

        [SerializeField, HideInInspector] private float monsterSpawnMinimumDistance = 10f;
        [SerializeField, HideInInspector] private float monsterSpawnMaximumDistance = 12f;

        private readonly List<Enemy> enemies = new();
        private readonly List<Projectile> projectiles = new();
        private readonly List<Pickup> pickups = new();

        private Transform player;
        private Player playerController;
        private Sprite playerSprite;
        private Sprite slimeSprite;
        private Sprite skeletonSprite;
        private Sprite goblinSprite;
        private Sprite goblinFrame2;
        private Sprite mushroomSprite;
        private Sprite mushroomFrame2;
        private Sprite boarSprite;
        private Sprite boarFrame2;
        private Sprite gemSprite;
        private Sprite coinSprite;
        private Sprite boltSprite;
        private RuntimeAnimatorController slimeController;
        private RuntimeAnimatorController skeletonController;
        private RuntimeAnimatorController boltController;
        private RuntimeAnimatorController playerAnimatorController;
        private ProjectDMAssetLoader assetLoader;
        private GameObjectPool objectPool;
        private ProjectDMRuntimeAssets runtimeAssets;

        private int level = 1;
        private int experience;
        private int experienceToNext = 7;
        private int runCurrency;
        private int metaCurrency;
        private int damageLevel;
        private int hasteLevel;
        private int fortuneLevel;
        private int runDamageBonus;
        private int runHasteBonus;
        private int runFortuneBonus;
        private float nextSpawn;
        private float nextShot;
        private bool choosingUpgrade;
        private bool showMetaTree;
        private float elapsed;
        private GUIStyle titleStyle;
        private GUIStyle statStyle;
        private GUIStyle cardStyle;
        private bool isInitialized;

        /// <summary>Called by the Main scene setup utility to connect edit-time layout objects.</summary>
        public void ConfigureSceneLayout(
            GameFieldBounds field,
            Transform playerStart,
            MonsterSpawnAreaPreview spawnArea,
            PlayerMovementAreaPreview movementArea,
            DungeonFloorTilemap floor)
        {
            fieldBounds = field;
            playerSpawnPoint = playerStart;
            monsterSpawnArea = spawnArea;
            playerMovementArea = movementArea;
            dungeonFloor = floor;
        }

        /// <summary>Assigns the camera that is authored in the Main scene.</summary>
        public void ConfigureSceneCamera(Camera sceneCamera)
        {
            gameplayCamera = sceneCamera;
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            SetupCamera();
            LoadMetaProgress();
            objectPool = gameObject.AddComponent<GameObjectPool>();
            assetLoader = gameObject.AddComponent<ProjectDMAssetLoader>();
            StartCoroutine(InitializeGame());
        }

        private IEnumerator InitializeGame()
        {
            yield return assetLoader.LoadAsync();
            if (!assetLoader.IsReady)
            {
                Debug.LogError(assetLoader.Error ?? "Project DM Addressables bootstrap failed.");
                yield break;
            }

            runtimeAssets = assetLoader.Assets;
            LoadSprites(runtimeAssets);
            CreateBackdrop();
            CreatePlayer();
            isInitialized = true;
        }

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                showMetaTree = !showMetaTree;
            }

            if (choosingUpgrade || showMetaTree)
            {
                return;
            }

            float dt = Time.deltaTime;
            elapsed += dt;
            MovePlayer(dt);
            if (monsterSpawnArea != null)
            {
                monsterSpawnArea.transform.position = player.position;
            }
            SpawnEnemies(elapsed);
            FireAtNearestEnemy(elapsed);
            UpdateEnemies(dt);
            UpdateProjectiles(dt);
            UpdatePickups(dt);
        }

        private void SetupCamera()
        {
            Camera camera = gameplayCamera != null ? gameplayCamera : Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.orthographicSize = 5.5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.backgroundColor = new Color(0.025f, 0.018f, 0.07f);
            }

            gameplayCamera = camera;
            // Preserve the intended 1920×1080 composition on any monitor by letter/pillarboxing the camera.
            const float targetAspect = 16f / 9f;
            float windowAspect = (float)Screen.width / Screen.height;
            float heightScale = windowAspect / targetAspect;
            camera.rect = heightScale < 1f
                ? new Rect(0f, (1f - heightScale) * .5f, 1f, heightScale)
                : new Rect((1f - 1f / heightScale) * .5f, 0f, 1f / heightScale, 1f);
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                light.enabled = false;
            }
        }

        private void LoadMetaProgress()
        {
            metaCurrency = PlayerPrefs.GetInt(MetaCurrencyKey, 0);
            damageLevel = PlayerPrefs.GetInt(DamageKey, 0);
            hasteLevel = PlayerPrefs.GetInt(HasteKey, 0);
            fortuneLevel = PlayerPrefs.GetInt(FortuneKey, 0);
        }

        private void SaveMetaProgress()
        {
            PlayerPrefs.SetInt(MetaCurrencyKey, metaCurrency);
            PlayerPrefs.SetInt(DamageKey, damageLevel);
            PlayerPrefs.SetInt(HasteKey, hasteLevel);
            PlayerPrefs.SetInt(FortuneKey, fortuneLevel);
            PlayerPrefs.Save();
        }

        private void LoadSprites(ProjectDMRuntimeAssets assets)
        {
            ProjectDMSpriteCatalog spriteCatalog = assets.SpriteCatalog;
            if (spriteCatalog != null)
            {
                playerSprite = FirstFrame(spriteCatalog.playerDownFrames);
                slimeSprite = FirstFrame(spriteCatalog.slimeFrames);
                skeletonSprite = FirstFrame(spriteCatalog.skeletonFrames);
            }
            if (assets.GameplaySheet != null)
            {
                gemSprite = Slice(assets.GameplaySheet, .385f, .025f, .080f, .180f, 64f);
                coinSprite = Slice(assets.GameplaySheet, .545f, .025f, .060f, .160f, 64f);
                boltSprite = Slice(assets.GameplaySheet, .020f, .025f, .100f, .180f, 64f);
            }

            playerAnimatorController = assets.PlayerAnimatorController;
            slimeController = assets.SlimeAnimatorController;
            skeletonController = assets.SkeletonAnimatorController;
            boltController = assets.BoltAnimatorController;
            Texture2D extras = assets.ExtraMonsterSheet;
            if (extras != null)
            {
                goblinSprite = Slice(extras, 0f, 2f / 3f, .5f, 1f / 3f, 96f); goblinFrame2 = Slice(extras, .5f, 2f / 3f, .5f, 1f / 3f, 96f);
                mushroomSprite = Slice(extras, 0f, 1f / 3f, .5f, 1f / 3f, 96f); mushroomFrame2 = Slice(extras, .5f, 1f / 3f, .5f, 1f / 3f, 96f);
                boarSprite = Slice(extras, 0f, 0f, .5f, 1f / 3f, 96f); boarFrame2 = Slice(extras, .5f, 0f, .5f, 1f / 3f, 96f);
            }

            playerSprite ??= PixelSprite(new Color(0.45f, 0.18f, 0.8f), new Color(0.05f, 0.9f, 1f));
            slimeSprite ??= PixelSprite(new Color(0.18f, 0.9f, 0.22f), new Color(0.7f, 1f, 0.15f));
            skeletonSprite ??= PixelSprite(new Color(0.75f, 0.70f, 0.62f), new Color(0.22f, 0.12f, 0.14f));
            gemSprite ??= PixelSprite(new Color(0.08f, 0.55f, 1f), Color.white);
            coinSprite ??= PixelSprite(new Color(1f, 0.68f, 0.08f), Color.white);
            boltSprite ??= PixelSprite(new Color(0.85f, 0.2f, 1f), Color.white);
        }

        private static Sprite Slice(Texture2D sheet, float x, float y, float width, float height, float pixelsPerUnit = 32f)
        {
            return Sprite.Create(
                sheet,
                new Rect(sheet.width * x, sheet.height * y, sheet.width * width, sheet.height * height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
        }

        private static Sprite FirstFrame(Sprite[] frames)
        {
            return frames != null && frames.Length > 0 ? frames[0] : null;
        }

        private static Sprite PixelSprite(Color fill, Color core)
        {
            const int size = 16;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f));
                    texture.SetPixel(x, y, distance < 7.3f ? (distance < 3.5f ? core : fill) : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }

        private void CreateBackdrop()
        {
            for (int i = 0; i < 80; i++)
            {
                GameObject star = new("Ambient pixel");
                star.transform.position = new Vector3(Random.Range(-10f, 10f), Random.Range(-6f, 6f), 3f);
                SpriteRenderer renderer = star.AddComponent<SpriteRenderer>();
                renderer.sprite = PixelSprite(new Color(0.12f, 0.06f, 0.25f), new Color(0.18f, 0.08f, 0.32f));
                renderer.color = new Color(1f, 1f, 1f, Random.Range(0.15f, 0.55f));
                renderer.sortingOrder = -5;
                star.transform.localScale = Vector3.one * Random.Range(0.018f, 0.05f);
            }
        }

        private void CreatePlayer()
        {
            GameObject avatar = Instantiate(runtimeAssets.PlayerPrefab);
            avatar.name = "Arcane Hunter";
            avatar.transform.position = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero;
            avatar.transform.localScale = Vector3.one * 0.95f;
            SpriteRenderer renderer = avatar.GetComponentInChildren<SpriteRenderer>();
            renderer.sortingOrder = 3;
            player = avatar.transform;
            Animator animator = avatar.GetComponentInChildren<Animator>();
            animator.applyRootMotion = false;
            playerController = avatar.GetComponent<Player>();
            playerController.Initialize(
                playerSprite,
                playerAnimatorController);
            Vector2 movementCenter = playerMovementArea != null ? playerMovementArea.transform.position : Vector2.zero;
            Vector2 movementBounds = playerMovementArea != null ? playerMovementArea.HalfExtents : playerMovementBounds;
            playerController.SetMovementBounds(movementBounds, movementCenter);
        }

        private void MovePlayer(float dt)
        {
            float speed = 3.4f + 0.15f * (hasteLevel + runHasteBonus);
            playerController.Tick(dt, speed);
        }

        private void SpawnEnemies(float time)
        {
            if (time < nextSpawn)
            {
                return;
            }

            float difficulty = 1f + elapsed / 35f;
            nextSpawn = time + Mathf.Max(0.12f, 0.70f - elapsed / 180f);
            int burst = Mathf.Min(1 + Mathf.FloorToInt(elapsed / 60f), 4);
            for (int i = 0; i < burst; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                if (direction == Vector2.zero)
                {
                    direction = Vector2.right;
                }

                GameObject enemyObject = objectPool.Rent(runtimeAssets.EnemyPrefab);
                enemyObject.name = "Void Slime";
                float minimumDistance = monsterSpawnArea != null ? monsterSpawnArea.MinimumDistance : monsterSpawnMinimumDistance;
                float maximumDistance = monsterSpawnArea != null ? monsterSpawnArea.MaximumDistance : monsterSpawnMaximumDistance;
                enemyObject.transform.position = (Vector2)player.position + direction * Random.Range(minimumDistance, maximumDistance);
                enemyObject.transform.localScale = Vector3.one * Random.Range(0.55f, 0.78f);
                int monsterKind = Random.Range(0, 5);
                bool skeleton = monsterKind == 1;
                SpriteRenderer renderer = enemyObject.GetComponentInChildren<SpriteRenderer>();
                Sprite sprite = monsterKind switch { 1 => skeletonSprite, 2 => goblinSprite, 3 => mushroomSprite, 4 => boarSprite, _ => slimeSprite };
                Sprite alternate = monsterKind switch { 2 => goblinFrame2, 3 => mushroomFrame2, 4 => boarFrame2, _ => null };
                renderer.sprite = sprite ?? slimeSprite;
                renderer.sortingOrder = 2;
                Animator animator = enemyObject.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = skeleton ? skeletonController : slimeController;
                animator.applyRootMotion = false;
                if (skeleton)
                {
                    enemyObject.transform.localScale *= 0.9f;
                }
                enemies.Add(new Enemy { transform = enemyObject.transform, renderer = renderer, baseSprite = renderer.sprite, alternateSprite = alternate, hitPoints = Mathf.CeilToInt((skeleton ? 3f : 2f) + difficulty), speed = (skeleton ? 0.82f : 1f) + difficulty * 0.12f });
            }
        }

        private void FireAtNearestEnemy(float time)
        {
            if (time < nextShot || enemies.Count == 0)
            {
                return;
            }

            Enemy target = null;
            float nearest = float.MaxValue;
            foreach (Enemy enemy in enemies)
            {
                if (enemy.transform == null)
                {
                    continue;
                }

                float distance = ((Vector2)enemy.transform.position - (Vector2)player.position).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    target = enemy;
                }
            }

            if (target == null)
            {
                return;
            }

            nextShot = time + Mathf.Max(0.18f, 0.62f - 0.035f * (hasteLevel + runHasteBonus));
            Vector2 direction = ((Vector2)target.transform.position - (Vector2)player.position).normalized;
            GameObject bolt = objectPool.Rent(runtimeAssets.ProjectilePrefab);
            bolt.name = "Arcane Bolt";
            bolt.transform.position = player.position;
            bolt.transform.localScale = Vector3.one * 0.32f;
            SpriteRenderer renderer = bolt.GetComponentInChildren<SpriteRenderer>();
            renderer.sprite = boltSprite;
            renderer.sortingOrder = 4;
            renderer.color = new Color(1f, 0.7f, 1f);
            Animator animator = bolt.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = boltController;
            animator.applyRootMotion = false;
            projectiles.Add(new Projectile { transform = bolt.transform, direction = direction, damage = 1 + damageLevel + runDamageBonus, lifetime = 1.5f });
        }

        private void UpdateEnemies(float dt)
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = enemies[i];
                if (enemy.transform == null)
                {
                    enemies.RemoveAt(i);
                    continue;
                }

                Vector2 direction = ((Vector2)player.position - (Vector2)enemy.transform.position).normalized;
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

        private void UpdateProjectiles(float dt)
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile bolt = projectiles[i];
                if (bolt.transform == null)
                {
                    projectiles.RemoveAt(i);
                    continue;
                }

                bolt.transform.position += (Vector3)(bolt.direction * 9f * dt);
                bolt.lifetime -= dt;
                bool hit = false;
                for (int j = enemies.Count - 1; j >= 0; j--)
                {
                    Enemy enemy = enemies[j];
                    if (enemy.transform != null && Vector2.Distance(bolt.transform.position, enemy.transform.position) < 0.55f)
                    {
                        enemy.hitPoints -= bolt.damage;
                        hit = true;
                        if (enemy.hitPoints <= 0)
                        {
                            SpawnLoot(enemy.transform.position);
                            objectPool.Return(enemy.transform.gameObject);
                            enemies.RemoveAt(j);
                        }

                        break;
                    }
                }

                if (hit || bolt.lifetime <= 0f)
                {
                    objectPool.Return(bolt.transform.gameObject);
                    projectiles.RemoveAt(i);
                }
            }
        }

        private void SpawnLoot(Vector3 position)
        {
            CreatePickup("Experience", position, gemSprite, PickupKind.Experience, 1, 0.28f);
            if (Random.value < 0.28f + 0.025f * (fortuneLevel + runFortuneBonus))
            {
                CreatePickup("Currency", position + (Vector3)Random.insideUnitCircle * 0.25f, coinSprite, PickupKind.Currency, 1, 0.23f);
            }

            if (Random.value < 0.018f + 0.003f * (fortuneLevel + runFortuneBonus))
            {
                CreatePickup("Treasure Chest", position + Vector3.up * 0.20f, coinSprite, PickupKind.Chest, 6, 0.42f);
            }
        }

        private void CreatePickup(string name, Vector3 position, Sprite sprite, PickupKind kind, int amount, float scale)
        {
            GameObject prefab = SelectPickupPrefab(kind);
            if (prefab == null)
            {
                Debug.LogWarning($"Project DM could not spawn {kind}: no pooled prefab is configured.");
                return;
            }
            GameObject pickupObject = objectPool.Rent(prefab);
            pickupObject.name = name;
            pickupObject.transform.position = position;
            pickupObject.transform.localScale = Vector3.one * scale;
            SpriteRenderer renderer = pickupObject.GetComponentInChildren<SpriteRenderer>();
            renderer.sortingOrder = 1;
            Animator animator = pickupObject.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.applyRootMotion = false;
                animator.Rebind();
                animator.Play("Loop", 0, 0f);
            }
            else
            {
                // Keeps old catalog/builds playable while the new controllers are being imported.
                renderer.sprite = sprite;
            }
            pickups.Add(new Pickup { transform = pickupObject.transform, kind = kind, amount = amount });
        }

        private GameObject SelectPickupPrefab(PickupKind kind)
        {
            if (kind == PickupKind.Chest)
            {
                return runtimeAssets.ChestPickupPrefab;
            }

            GameObject[] variants = kind == PickupKind.Currency
                ? runtimeAssets.CurrencyPickupPrefabs
                : runtimeAssets.ExperiencePickupPrefabs;
            return variants != null && variants.Length > 0
                ? variants[Random.Range(0, variants.Length)]
                : null;
        }

        private void UpdatePickups(float dt)
        {
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                Pickup pickup = pickups[i];
                if (pickup.transform == null)
                {
                    pickups.RemoveAt(i);
                    continue;
                }

                float distance = Vector2.Distance(player.position, pickup.transform.position);
                if (distance < 2.0f)
                {
                    pickup.transform.position = Vector3.MoveTowards(pickup.transform.position, player.position, (2.5f + (2f - distance) * 6f) * dt);
                }

                if (distance < 0.28f)
                {
                    Collect(pickup);
                    objectPool.Return(pickup.transform.gameObject);
                    pickups.RemoveAt(i);
                }
            }
        }

        private void Collect(Pickup pickup)
        {
            if (pickup.kind == PickupKind.Experience)
            {
                experience += pickup.amount;
                if (experience >= experienceToNext)
                {
                    experience -= experienceToNext;
                    level++;
                    experienceToNext = 7 + level * 4;
                    choosingUpgrade = true;
                }
                return;
            }

            int currency = pickup.amount;
            runCurrency += currency;
            metaCurrency += currency;
            SaveMetaProgress();
        }

        private void ChooseRunUpgrade(RunUpgrade upgrade)
        {
            switch (upgrade)
            {
                case RunUpgrade.Damage:
                    runDamageBonus++;
                    break;
                case RunUpgrade.Haste:
                    runHasteBonus++;
                    break;
                case RunUpgrade.Fortune:
                    runFortuneBonus++;
                    break;
            }

            choosingUpgrade = false;
        }

        private int MetaCost(int currentLevel) => 8 + currentLevel * 7;

        private void BuyMetaUpgrade(MetaUpgrade upgrade)
        {
            int currentLevel = upgrade switch
            {
                MetaUpgrade.Damage => damageLevel,
                MetaUpgrade.Haste => hasteLevel,
                _ => fortuneLevel
            };
            int cost = MetaCost(currentLevel);
            if (metaCurrency < cost)
            {
                return;
            }

            metaCurrency -= cost;
            switch (upgrade)
            {
                case MetaUpgrade.Damage: damageLevel++; break;
                case MetaUpgrade.Haste: hasteLevel++; break;
                case MetaUpgrade.Fortune: fortuneLevel++; break;
            }
            SaveMetaProgress();
        }

        private void OnGUI()
        {
            SetupGuiStyles();
            if (!isInitialized)
            {
                string message = assetLoader != null && !string.IsNullOrEmpty(assetLoader.Error)
                    ? "ASSET LOAD FAILED — check the Console"
                    : "LOADING ADDRESSABLE ASSETS...";
                GUI.Label(new Rect(0, Screen.height * .45f, Screen.width, 42), message, new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            GUI.Label(new Rect(20, 16, 620, 38), "PROJECT DM  //  ARCANE SURVIVOR", titleStyle);
            GUI.Label(new Rect(22, 56, 600, 28), $"Level {level}    경험치 {experience}/{experienceToNext}    획득 재화 +{runCurrency}    영구 재화 {metaCurrency}", statStyle);
            GUI.Label(new Rect(22, 84, 650, 26), $"Arcane Bolt {1 + damageLevel + runDamageBonus} DMG   |   Cast {Mathf.Max(0.18f, 0.62f - 0.035f * (hasteLevel + runHasteBonus)):0.00}s   |   Fortune +{(fortuneLevel + runFortuneBonus) * 3}%", statStyle);

            if (GUI.Button(new Rect(Screen.width - 190, 20, 165, 36), "META TREE  [TAB]"))
            {
                showMetaTree = !showMetaTree;
            }

            if (choosingUpgrade)
            {
                DrawRunUpgradeSelection();
            }

            if (showMetaTree)
            {
                DrawMetaTree();
            }
        }

        private void SetupGuiStyles()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.48f, 1f) }
            };
            statStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = new Color(0.72f, 0.82f, 1f) }
            };
            cardStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }

        private void DrawRunUpgradeSelection()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.Label(new Rect(0, Screen.height * 0.20f, Screen.width, 48), "LEVEL UP — CHOOSE ONE", new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 30 });
            float width = 210f;
            float x = (Screen.width - (width * 3 + 30f)) / 2f;
            if (GUI.Button(new Rect(x, Screen.height * 0.38f, width, 150), "ARCANE EDGE\n\nArcane Bolt damage +1", cardStyle))
            {
                ChooseRunUpgrade(RunUpgrade.Damage);
            }
            if (GUI.Button(new Rect(x + width + 15f, Screen.height * 0.38f, width, 150), "QUICKENING\n\nCast interval -0.035 sec", cardStyle))
            {
                ChooseRunUpgrade(RunUpgrade.Haste);
            }
            if (GUI.Button(new Rect(x + (width + 15f) * 2f, Screen.height * 0.38f, width, 150), "GILDED FATE\n\nMore currency and chest drops", cardStyle))
            {
                ChooseRunUpgrade(RunUpgrade.Fortune);
            }
        }

        private void DrawMetaTree()
        {
            float panelWidth = Mathf.Min(620, Screen.width - 60);
            float x = (Screen.width - panelWidth) / 2f;
            float y = 135f;
            GUI.Box(new Rect(x, y, panelWidth, 360), "영구 성장 — 저장된 재화 사용", new GUIStyle(GUI.skin.box) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, padding = new RectOffset(10, 10, 16, 10) });
            GUI.Label(new Rect(x + 28, y + 55, panelWidth - 56, 32), $"보유 영구 재화: {metaCurrency}", titleStyle);
            DrawMetaButton(x + 28, y + 105, MetaUpgrade.Damage, "ARCANE ROOT", "Start each run with +1 bolt damage", damageLevel);
            DrawMetaButton(x + 28, y + 175, MetaUpgrade.Haste, "SWIFT ROOT", "Start each run with faster casting", hasteLevel);
            DrawMetaButton(x + 28, y + 245, MetaUpgrade.Fortune, "GILDED ROOT", "Start each run with +3% loot chance", fortuneLevel);
            if (GUI.Button(new Rect(x + panelWidth - 130, y + 310, 100, 30), "CLOSE"))
            {
                showMetaTree = false;
            }
        }

        private void DrawMetaButton(float x, float y, MetaUpgrade upgrade, string name, string description, int currentLevel)
        {
            int cost = MetaCost(currentLevel);
            GUI.Label(new Rect(x, y, 260, 25), $"{name}  Lv.{currentLevel}", titleStyle);
            GUI.Label(new Rect(x, y + 28, 330, 24), description, statStyle);
            if (GUI.Button(new Rect(x + 355, y + 8, 195, 45), $"UNLOCK  {cost} 재화"))
            {
                BuyMetaUpgrade(upgrade);
            }
        }

        private enum RunUpgrade { Damage, Haste, Fortune }
        private enum MetaUpgrade { Damage, Haste, Fortune }
    }
}
