using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProjectDM
{
    /// <summary>
    /// Scene-level composition root for Project DM. It initializes Addressables, then starts one run.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        private const string MetaGoldKey = "PROJECT_DM_META_GOLD";
        private const string DamageKey = "PROJECT_DM_DAMAGE_LEVEL";
        private const string HasteKey = "PROJECT_DM_HASTE_LEVEL";
        private const string FortuneKey = "PROJECT_DM_FORTUNE_LEVEL";

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

        private int level = 1;
        private int experience;
        private int experienceToNext = 7;
        private int runGold;
        private int metaGold;
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

        private void Awake()
        {
            Application.targetFrameRate = 60;
            SetupCamera();
            LoadMetaProgress();
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

            LoadSprites(assetLoader.Assets);
            CreateDungeonFloor(assetLoader.Assets.FloorSheet);
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
            SpawnEnemies(elapsed);
            FireAtNearestEnemy(elapsed);
            UpdateEnemies(dt);
            UpdateProjectiles(dt);
            UpdatePickups(dt);
        }

        private void SetupCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
            }

            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.backgroundColor = new Color(0.025f, 0.018f, 0.07f);
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
            metaGold = PlayerPrefs.GetInt(MetaGoldKey, 0);
            damageLevel = PlayerPrefs.GetInt(DamageKey, 0);
            hasteLevel = PlayerPrefs.GetInt(HasteKey, 0);
            fortuneLevel = PlayerPrefs.GetInt(FortuneKey, 0);
        }

        private void SaveMetaProgress()
        {
            PlayerPrefs.SetInt(MetaGoldKey, metaGold);
            PlayerPrefs.SetInt(DamageKey, damageLevel);
            PlayerPrefs.SetInt(HasteKey, hasteLevel);
            PlayerPrefs.SetInt(FortuneKey, fortuneLevel);
            PlayerPrefs.Save();
        }

        private void LoadSprites(ProjectDMRuntimeAssets assets)
        {
            if (assets.PlayerSheet != null)
            {
                playerSprite = SliceGrid(assets.PlayerSheet, 1, 0, 4, 2, 96f);
            }
            if (assets.MonsterSheet != null)
            {
                slimeSprite = SliceGrid(assets.MonsterSheet, 0, 0, 2, 2, 64f);
                skeletonSprite = SliceGrid(assets.MonsterSheet, 0, 1, 2, 2, 64f);
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
                goblinSprite = Slice(extras, 0f, 2f / 3f, .5f, 1f / 3f); goblinFrame2 = Slice(extras, .5f, 2f / 3f, .5f, 1f / 3f);
                mushroomSprite = Slice(extras, 0f, 1f / 3f, .5f, 1f / 3f); mushroomFrame2 = Slice(extras, .5f, 1f / 3f, .5f, 1f / 3f);
                boarSprite = Slice(extras, 0f, 0f, .5f, 1f / 3f); boarFrame2 = Slice(extras, .5f, 0f, .5f, 1f / 3f);
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

        private static Sprite SliceGrid(Texture2D sheet, int column, int row, int columns, int rows, float pixelsPerUnit)
        {
            const float inset = 1f;
            float cellWidth = sheet.width / (float)columns;
            float cellHeight = sheet.height / (float)rows;
            return Sprite.Create(
                sheet,
                new Rect(column * cellWidth + inset, sheet.height - (row + 1) * cellHeight + inset, cellWidth - inset * 2f, cellHeight - inset * 2f),
                new Vector2(.5f, .5f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
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

        private void CreateDungeonFloor(Texture2D floorSheet)
        {
            if (floorSheet == null)
            {
                return;
            }

            TileBase[] tiles = new TileBase[16];
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    Tile tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = SliceGrid(floorSheet, column, row, 4, 4, 64f);
                    tiles[row * 4 + column] = tile;
                }
            }

            GameObject gridObject = new("Dungeon Floor Grid");
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            GameObject tilemapObject = new("Dungeon Floor Tilemap");
            tilemapObject.transform.SetParent(gridObject.transform);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer tilemapRenderer = tilemapObject.AddComponent<TilemapRenderer>();
            tilemapRenderer.sortingOrder = -10;
            for (int x = -12; x <= 12; x++)
            {
                for (int y = -8; y <= 8; y++)
                {
                    int tileIndex = Mathf.Abs(x * 17 + y * 31) % tiles.Length;
                    tilemap.SetTile(new Vector3Int(x, y, 0), tiles[tileIndex]);
                }
            }
        }

        private void CreatePlayer()
        {
            GameObject avatar = new("Arcane Hunter");
            avatar.transform.position = Vector3.zero;
            avatar.transform.localScale = Vector3.one * 0.95f;
            SpriteRenderer renderer = avatar.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 3;
            player = avatar.transform;
            avatar.AddComponent<Animator>();
            avatar.AddComponent<PlayerMovement>();
            avatar.AddComponent<PlayerAnimation>();
            playerController = avatar.AddComponent<Player>();
            playerController.Initialize(
                playerSprite,
                playerAnimatorController);
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

                GameObject enemyObject = new("Void Slime");
                enemyObject.transform.position = (Vector2)player.position + direction * Random.Range(7.2f, 9.3f);
                enemyObject.transform.localScale = Vector3.one * Random.Range(0.55f, 0.78f);
                int monsterKind = Random.Range(0, 5);
                bool skeleton = monsterKind == 1;
                SpriteRenderer renderer = enemyObject.AddComponent<SpriteRenderer>();
                Sprite sprite = monsterKind switch { 1 => skeletonSprite, 2 => goblinSprite, 3 => mushroomSprite, 4 => boarSprite, _ => slimeSprite };
                Sprite alternate = monsterKind switch { 2 => goblinFrame2, 3 => mushroomFrame2, 4 => boarFrame2, _ => null };
                renderer.sprite = sprite ?? slimeSprite;
                renderer.sortingOrder = 2;
                Animator animator = enemyObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = skeleton ? skeletonController : slimeController;
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
            GameObject bolt = new("Arcane Bolt");
            bolt.transform.position = player.position;
            bolt.transform.localScale = Vector3.one * 0.32f;
            SpriteRenderer renderer = bolt.AddComponent<SpriteRenderer>();
            renderer.sprite = boltSprite;
            renderer.sortingOrder = 4;
            renderer.color = new Color(1f, 0.7f, 1f);
            Animator animator = bolt.AddComponent<Animator>();
            animator.runtimeAnimatorController = boltController;
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
                            Destroy(enemy.transform.gameObject);
                            enemies.RemoveAt(j);
                        }

                        break;
                    }
                }

                if (hit || bolt.lifetime <= 0f)
                {
                    Destroy(bolt.transform.gameObject);
                    projectiles.RemoveAt(i);
                }
            }
        }

        private void SpawnLoot(Vector3 position)
        {
            CreatePickup("Experience Gem", position, gemSprite, PickupKind.Experience, 1, 0.28f);
            if (Random.value < 0.28f + 0.025f * (fortuneLevel + runFortuneBonus))
            {
                CreatePickup("Gold", position + (Vector3)Random.insideUnitCircle * 0.25f, coinSprite, PickupKind.Gold, 1, 0.23f);
            }

            if (Random.value < 0.018f + 0.003f * (fortuneLevel + runFortuneBonus))
            {
                CreatePickup("Treasure Chest", position + Vector3.up * 0.20f, coinSprite, PickupKind.Chest, 6, 0.42f);
            }
        }

        private void CreatePickup(string name, Vector3 position, Sprite sprite, PickupKind kind, int amount, float scale)
        {
            GameObject pickupObject = new(name);
            pickupObject.transform.position = position;
            pickupObject.transform.localScale = Vector3.one * scale;
            SpriteRenderer renderer = pickupObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 1;
            pickups.Add(new Pickup { transform = pickupObject.transform, kind = kind, amount = amount });
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
                    Destroy(pickup.transform.gameObject);
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

            int gold = pickup.amount;
            runGold += gold;
            metaGold += gold;
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
            if (metaGold < cost)
            {
                return;
            }

            metaGold -= cost;
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
            GUI.Label(new Rect(22, 56, 600, 28), $"Level {level}    XP {experience}/{experienceToNext}    Run Gold +{runGold}    Permanent Gold {metaGold}", statStyle);
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
            if (GUI.Button(new Rect(x + (width + 15f) * 2f, Screen.height * 0.38f, width, 150), "GILDED FATE\n\nMore gold and chest drops", cardStyle))
            {
                ChooseRunUpgrade(RunUpgrade.Fortune);
            }
        }

        private void DrawMetaTree()
        {
            float panelWidth = Mathf.Min(620, Screen.width - 60);
            float x = (Screen.width - panelWidth) / 2f;
            float y = 135f;
            GUI.Box(new Rect(x, y, panelWidth, 360), "PERMANENT GROWTH — spends saved gold", new GUIStyle(GUI.skin.box) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, padding = new RectOffset(10, 10, 16, 10) });
            GUI.Label(new Rect(x + 28, y + 55, panelWidth - 56, 32), $"Available permanent gold: {metaGold}", titleStyle);
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
            if (GUI.Button(new Rect(x + 355, y + 8, 195, 45), $"UNLOCK  {cost} gold"))
            {
                BuyMetaUpgrade(upgrade);
            }
        }

        private enum RunUpgrade { Damage, Haste, Fortune }
        private enum MetaUpgrade { Damage, Haste, Fortune }
    }
}
