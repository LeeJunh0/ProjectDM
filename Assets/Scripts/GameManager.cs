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

        [SerializeField, Min(1f), Tooltip("한 회차의 플레이 제한 시간(초)입니다. 변경한 값은 다음 회차부터 적용됩니다.")]
        private float runDuration = 40f;

        [SerializeField, Min(0f), Tooltip("각 통계 숫자가 0에서 실제 값에 도달하는 시간(초)입니다. 0이면 즉시 표시합니다.")]
        private float resultCountUpDuration = 1.1f;
        [SerializeField, Min(0f), Tooltip("재화, 총 처치, 몬스터별 처치 숫자의 등장 간격(초)입니다. 0이면 함께 시작합니다.")]
        private float resultCountUpStagger = .07f;
        [SerializeField, Range(0f, 1f), Tooltip("결과창 위·아래 주황빛 그라데이션의 불투명도입니다. 0이면 숨깁니다.")]
        private float resultGlowIntensity = .16f;
        [SerializeField, Min(0f), Tooltip("주황빛이 은은하게 밝아졌다 어두워지는 주기(초)입니다. 0이면 밝기를 고정합니다.")]
        private float resultGlowPulseDuration = 3f;

        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private GameFieldBounds fieldBounds;
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private MonsterSpawnAreaPreview monsterSpawnArea;
        [SerializeField] private PlayerMovementAreaPreview playerMovementArea;
        [SerializeField] private DungeonFloorTilemap dungeonFloor;

        [SerializeField, Min(0f), Tooltip("초당 카메라 이동 거리입니다. 0이면 카메라가 고정됩니다.")]
        private float cameraFollowSpeed = 8f;

        [SerializeField, HideInInspector] private Vector2 fieldSize = new(25f, 17f);

        [SerializeField, HideInInspector] private Vector2 playerMovementBounds = new(8.4f, 4.8f);

        [SerializeField, HideInInspector] private float monsterSpawnMinimumDistance = 10f;
        [SerializeField, HideInInspector] private float monsterSpawnMaximumDistance = 12f;

        [SerializeField, Min(0.1f), Tooltip("경험치 픽업 비주얼의 크기 배율입니다. 기본값은 원본의 3배입니다.")]
        private float experiencePickupScaleMultiplier = 3f;
        [SerializeField, Min(0.1f), Tooltip("재화 픽업 비주얼의 크기 배율입니다. 기본값은 원본의 3배입니다.")]
        private float currencyPickupScaleMultiplier = 3f;
        [SerializeField, Min(0.1f), Tooltip("보물상자 픽업 비주얼의 크기 배율입니다.")]
        private float chestPickupScaleMultiplier = 1f;

        [SerializeField, Min(0f), Tooltip("비주얼 자식 오브젝트가 부유하는 로컬 Y축 최대 높이입니다.")]
        private float collectibleFloatYAmplitude = .06f;
        [SerializeField, Min(0.05f), Tooltip("부유 모션이 한 번 반복되는 시간(초)입니다.")]
        private float collectibleFloatLoopDuration = 1.2f;
        [SerializeField, Tooltip("경험치 픽업 비주얼의 정규화된 로컬 Y축 부유 곡선입니다.")]
        private AnimationCurve experiencePickupFloatYCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(.25f, 1f), new Keyframe(.5f, 0f), new Keyframe(.75f, -.67f), new Keyframe(1f, 0f));
        [SerializeField, Tooltip("재화 픽업 비주얼의 정규화된 로컬 Y축 부유 곡선입니다.")]
        private AnimationCurve currencyPickupFloatYCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(.25f, 1f), new Keyframe(.5f, 0f), new Keyframe(.75f, -.67f), new Keyframe(1f, 0f));

        [SerializeField, Min(0.05f), Tooltip("보물상자를 획득하기 전 상호작용 애니메이션이 재생되는 시간(초)입니다.")]
        private float chestInteractionDuration = .8f;
        [SerializeField, Tooltip("플레이어가 보물상자를 획득할 때 비주얼에 적용되는 로컬 Y축 모션입니다.")]
        private AnimationCurve chestInteractionYCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(.35f, .18f), new Keyframe(.7f, .06f), new Keyframe(1f, 0f));
        [SerializeField, Tooltip("플레이어가 보물상자를 획득할 때 비주얼에 적용되는 크기 모션입니다.")]
        private AnimationCurve chestInteractionScaleCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(.28f, 1.15f), new Keyframe(.65f, .95f), new Keyframe(1f, 1f));

        [SerializeField, Min(0.05f), Tooltip("경험치·재화 픽업이 몬스터 처치 지점에서 튀어나오는 시간(초)입니다.")]
        private float pickupFountainDuration = .28f;
        [SerializeField, Min(0f), Tooltip("픽업이 분수처럼 흩어져 착지하는 거리입니다.")]
        private float pickupFountainDistance = .62f;
        [SerializeField, Min(0f), Tooltip("픽업이 튀어나오는 동안 위로 솟는 포물선 높이입니다.")]
        private float pickupFountainArcHeight = .34f;

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
        private Vector2 metaTreePan;
        private float metaTreeZoom = 1f;
        private int selectedMetaTreeNode;
        private int hoveredMetaTreeNode = -1;
        private bool isPanningMetaTree;
        private Vector2 metaTreeLastPointer;
        private float elapsed;
        private float activeRunDuration;
        private float remainingTimeFraction = 1f;
        private bool runEnded;
        private readonly int[] monsterKillCounts = new int[5];
        private GUIStyle timerTrackStyle;
        private Texture2D timerIconTexture;
        private Texture2D timerTrackTexture;
        private Texture2D timerFillTexture;
        private Texture2D resultPanelTexture;
        private Texture2D resultSecondaryButtonTexture;
        private Texture2D resultPrimaryButtonTexture;
        private Texture2D resultGlowTexture;
        private float resultPresentationOpenedAt;

        // UV crops select the artwork within the generated transparent canvases, preserving source PNGs.
        private static readonly Rect TimerIconUv = new Rect(129f / 1254f, 39f / 1254f, 994f / 1254f, 1152f / 1254f);
        private static readonly Rect TimerTrackUv = new Rect(40f / 2172f, 309f / 724f, 2092f / 2172f, 119f / 724f);
        private static readonly Rect TimerFillUv = new Rect(40f / 1774f, 411f / 887f, 1694f / 1774f, 65f / 887f);
        private static readonly Rect ResultPanelUv = new Rect(56f / 1205f, 59f / 1305f, 1095f / 1205f, 1188f / 1305f);
        private static readonly Rect ResultSecondaryButtonUv = new Rect(83f / 2036f, 177f / 772f, 1870f / 2036f, 416f / 772f);
        private static readonly Rect ResultPrimaryButtonUv = new Rect(91f / 2146f, 156f / 733f, 1964f / 2146f, 421f / 733f);
        private float upgradeSelectionOpenedAt;
        private readonly float[] upgradeCardHoverTilts = new float[TemporaryRunUpgrades.Length];
        private readonly bool[] upgradeCardWasHovered = new bool[TemporaryRunUpgrades.Length];
        private GUIStyle titleStyle;
        private GUIStyle statStyle;
        private GUIStyle cardStyle;
        private Texture2D upgradeCardFrameTexture;
        private bool isInitialized;

        [SerializeField, Range(-25f, 0f), Tooltip("음수 값일수록 호버한 레벨업 카드가 왼쪽으로 더 기울어집니다.")]
        private float upgradeCardHoverLeftTiltAngle = -4f;
        [SerializeField, Min(1f), Tooltip("레벨업 카드가 호버 기울기까지 도달하는 속도입니다.")]
        private float upgradeCardHoverTiltSpeed = 180f;
        [SerializeField, Range(1f, 1.2f), Tooltip("레벨업 카드에 마우스를 올렸을 때 적용되는 확대 배율입니다.")]
        private float upgradeCardHoverScale = 1.035f;

        // Temporary presentation data. Replace this with the future skill/effect table without changing the selection UI.
        private static readonly RunUpgradePresentation[] TemporaryRunUpgrades =
        {
            new(RunUpgrade.Damage, "✦", "BRONZE", "ARCANE EDGE", "Arcane Bolt damage  +1", new Color(.72f, .42f, .20f)),
            new(RunUpgrade.Haste, "≫", "SILVER", "QUICKENING", "Cast interval  -0.035 sec", new Color(.72f, .76f, .81f)),
            new(RunUpgrade.Fortune, "$", "GOLD", "GILDED FATE", "Currency & chest chance  +3%", new Color(.96f, .72f, .23f))
        };

        // UI-only placeholder layout. Future table rows can replace these entries directly.
        private static readonly MetaTreeNodePresentation[] TemporaryMetaTreeNodes =
        {
            new("ROOT", "기원의 문", "영구 성장의 시작점", new Vector2(0f, 0f), -1, MetaTreeNodeState.Completed, new Color(.91f, .69f, .30f)),
            new("COMBAT", "전투 경로", "공격 계열 노드 슬롯", new Vector2(-300f, -145f), 0, MetaTreeNodeState.Reachable, new Color(.87f, .37f, .30f)),
            new("SURVIVAL", "생존 경로", "방어 계열 노드 슬롯", new Vector2(-265f, 175f), 0, MetaTreeNodeState.Reachable, new Color(.42f, .73f, .53f)),
            new("GROWTH", "성장 경로", "재화 계열 노드 슬롯", new Vector2(300f, 155f), 0, MetaTreeNodeState.Reachable, new Color(.38f, .64f, .92f)),
            new("ARCANE", "비전 노드", "효과 데이터 대기", new Vector2(-560f, -285f), 1, MetaTreeNodeState.Locked, new Color(.64f, .43f, .87f)),
            new("PRECISION", "정밀 노드", "효과 데이터 대기", new Vector2(-555f, 15f), 1, MetaTreeNodeState.Locked, new Color(.88f, .52f, .39f)),
            new("VITALITY", "활력 노드", "효과 데이터 대기", new Vector2(-55f, -420f), 1, MetaTreeNodeState.Locked, new Color(.42f, .78f, .60f)),
            new("WARD", "수호 노드", "효과 데이터 대기", new Vector2(-20f, 415f), 2, MetaTreeNodeState.Locked, new Color(.33f, .67f, .64f)),
            new("FORTUNE", "행운 노드", "효과 데이터 대기", new Vector2(565f, 15f), 3, MetaTreeNodeState.Locked, new Color(.94f, .72f, .27f)),
            new("MASTERY", "숙련 노드", "효과 데이터 대기", new Vector2(535f, 325f), 3, MetaTreeNodeState.Locked, new Color(.45f, .65f, .96f))
        };

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
            Time.timeScale = 1f;
            Application.targetFrameRate = 60;
            upgradeCardFrameTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_UpgradeCardFrame_Neutral_v1");
            timerIconTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_TimerIcon_v1");
            timerTrackTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_TimerTrack_v1");
            timerFillTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_TimerFill_v1");
            resultPanelTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_ResultPanel_v1");
            resultSecondaryButtonTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_ResultButtonSecondary_v1");
            resultPrimaryButtonTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_ResultButtonPrimary_v1");
            resultGlowTexture = Resources.Load<Texture2D>("ProjectDM/UI/ProjectDM_ResultGlowOrange_v1");
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
            BeginRun();
            isInitialized = true;
        }

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            if (choosingUpgrade)
            {
                HandleRunUpgradeShortcuts();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleMetaTree();
            }

            if (showMetaTree)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    showMetaTree = false;
                    RefreshPauseState();
                }

                return;
            }

            // A completed run remains frozen, but the growth screen can still be opened and closed.
            if (runEnded) return;

            float dt = Mathf.Min(Time.deltaTime, Mathf.Max(0f, activeRunDuration - elapsed));
            elapsed += dt;
            remainingTimeFraction = Mathf.Clamp01(1f - elapsed / activeRunDuration);
            MovePlayer(dt);
            FollowPlayerWithCamera(dt);
            if (monsterSpawnArea != null)
            {
                monsterSpawnArea.transform.position = player.position;
            }
            SpawnEnemies(elapsed);
            FireAtNearestEnemy(elapsed);
            UpdateEnemies(dt);
            UpdateProjectiles(dt);
            UpdatePickups(dt);
            if (elapsed >= activeRunDuration)
            {
                EndRun();
            }
        }

        private void BeginRun()
        {
            foreach (Enemy enemy in enemies)
            {
                if (enemy.transform != null) objectPool.Return(enemy.transform.gameObject);
            }
            foreach (Projectile projectile in projectiles)
            {
                if (projectile.transform != null) objectPool.Return(projectile.transform.gameObject);
            }
            foreach (Pickup pickup in pickups)
            {
                if (pickup.transform != null) objectPool.Return(pickup.transform.gameObject);
            }
            enemies.Clear();
            projectiles.Clear();
            pickups.Clear();
            System.Array.Clear(monsterKillCounts, 0, monsterKillCounts.Length);
            level = 1;
            experience = 0;
            experienceToNext = 7;
            runCurrency = 0;
            runDamageBonus = runHasteBonus = runFortuneBonus = 0;
            elapsed = nextSpawn = nextShot = 0f;
            activeRunDuration = Mathf.Max(1f, runDuration);
            remainingTimeFraction = 1f;
            runEnded = choosingUpgrade = showMetaTree = isPanningMetaTree = false;
            player.position = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero;
            playerController.Initialize(playerSprite, playerAnimatorController);
            SnapCameraToPlayer();
            RefreshPauseState();
        }

        private void EndRun()
        {
            if (runEnded) return;
            elapsed = activeRunDuration;
            remainingTimeFraction = 0f;
            runEnded = true;
            resultPresentationOpenedAt = Time.unscaledTime;
            choosingUpgrade = showMetaTree = isPanningMetaTree = false;
            RefreshPauseState();
            // Collected currency is already saved by Collect. Do not award it again here.
        }

        private void ToggleMetaTree()
        {
            if (choosingUpgrade) return;
            showMetaTree = !showMetaTree;
            isPanningMetaTree = false;
            RefreshPauseState();
        }

        private void ReturnToGrowthMap()
        {
            showMetaTree = true;
            isPanningMetaTree = false;
            hoveredMetaTreeNode = -1;
            RefreshPauseState();
        }

        private void RefreshPauseState()
        {
            Time.timeScale = runEnded || choosingUpgrade || showMetaTree ? 0f : 1f;
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
                // First coin only: measured in the 1774 x 887 source atlas, excluding the next frame.
                coinSprite = Slice(assets.GameplaySheet, 960f / 1774f, 58f / 887f, 82f / 1774f, 88f / 887f, 64f);
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
            SnapCameraToPlayer();
        }

        private void MovePlayer(float dt)
        {
            float speed = 3.4f + 0.15f * (hasteLevel + runHasteBonus);
            playerController.Tick(dt, speed);
        }

        private void SnapCameraToPlayer()
        {
            if (gameplayCamera == null || player == null)
            {
                return;
            }

            Vector3 cameraPosition = gameplayCamera.transform.position;
            gameplayCamera.transform.position = ClampCameraToFloor(new Vector3(player.position.x, player.position.y, cameraPosition.z));
        }

        private void FollowPlayerWithCamera(float dt)
        {
            if (gameplayCamera == null || player == null || cameraFollowSpeed <= 0f)
            {
                return;
            }

            Vector3 cameraPosition = gameplayCamera.transform.position;
            Vector3 targetPosition = ClampCameraToFloor(new Vector3(player.position.x, player.position.y, cameraPosition.z));
            gameplayCamera.transform.position = Vector3.MoveTowards(cameraPosition, targetPosition, cameraFollowSpeed * dt);
        }

        private Vector3 ClampCameraToFloor(Vector3 position)
        {
            if (gameplayCamera == null || dungeonFloor == null || !dungeonFloor.TryGetWorldBounds(out Bounds floorBounds))
            {
                return position;
            }

            float halfHeight = gameplayCamera.orthographicSize;
            float halfWidth = halfHeight * gameplayCamera.aspect;
            float minX = floorBounds.min.x + halfWidth;
            float maxX = floorBounds.max.x - halfWidth;
            float minY = floorBounds.min.y + halfHeight;
            float maxY = floorBounds.max.y - halfHeight;

            position.x = minX > maxX ? floorBounds.center.x : Mathf.Clamp(position.x, minX, maxX);
            position.y = minY > maxY ? floorBounds.center.y : Mathf.Clamp(position.y, minY, maxY);
            return position;
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
                enemies.Add(new Enemy { kind = (MonsterKind)monsterKind, transform = enemyObject.transform, renderer = renderer, baseSprite = renderer.sprite, alternateSprite = alternate, hitPoints = Mathf.CeilToInt((skeleton ? 3f : 2f) + difficulty), speed = (skeleton ? 0.82f : 1f) + difficulty * 0.12f });
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
            // The sprite is authored facing +X. Set this once at launch so pooled bolts retain
            // their original firing direction instead of inheriting a prior instance rotation.
            bolt.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
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
                            monsterKillCounts[(int)enemy.kind]++;
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
                CreatePickup("Currency", position, coinSprite, PickupKind.Currency, 1, 0.23f);
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
            float pickupScaleMultiplier = kind == PickupKind.Experience
                ? experiencePickupScaleMultiplier
                : kind == PickupKind.Currency
                    ? currencyPickupScaleMultiplier
                    : chestPickupScaleMultiplier;
            pickupObject.transform.localScale = Vector3.one * (scale * pickupScaleMultiplier);
            SpriteRenderer renderer = pickupObject.GetComponentInChildren<SpriteRenderer>();
            renderer.sortingOrder = 1;
            PickupFloatVisual floatVisual = pickupObject.GetComponentInChildren<PickupFloatVisual>();
            if (floatVisual != null)
            {
                AnimationCurve floatCurve = kind == PickupKind.Currency
                    ? currencyPickupFloatYCurve
                    : experiencePickupFloatYCurve;
                // A randomized phase keeps nearby pickups from bobbing in lockstep.
                floatVisual.Configure(floatCurve, collectibleFloatYAmplitude, collectibleFloatLoopDuration, Random.value);
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
                amount = amount,
                baseScale = scale * pickupScaleMultiplier
            };
            BeginPickupFountain(pickup, position);
            pickups.Add(pickup);
        }

        private void BeginPickupFountain(Pickup pickup, Vector3 origin)
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

            float distance = Random.Range(pickupFountainDistance * .62f, pickupFountainDistance);
            pickup.isLaunching = true;
            pickup.launchOrigin = origin;
            pickup.launchDestination = origin + (Vector3)(direction.normalized * distance);
            pickup.launchElapsed = 0f;
            pickup.launchDuration = Mathf.Max(.05f, pickupFountainDuration * Random.Range(.82f, 1.12f));
            pickup.launchArcHeight = pickupFountainArcHeight * Random.Range(.78f, 1.18f);
            pickup.transform.localScale = Vector3.one * (pickup.baseScale * .7f);
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

                float distance = Vector2.Distance(player.position, pickup.transform.position);
                if (distance < 2.0f)
                {
                    pickup.transform.position = Vector3.MoveTowards(pickup.transform.position, player.position, (2.5f + (2f - distance) * 6f) * dt);
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

        private void PlayChestInteraction(Pickup pickup)
        {
            pickup.isInteracting = true;
            pickup.interactionRemaining = chestInteractionDuration;
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
                interactionVisual.Play(chestInteractionYCurve, chestInteractionScaleCurve, chestInteractionDuration);
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
                    OpenRunUpgradeSelection();
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
            if (runEnded) return;
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
            RefreshPauseState();
        }

        private void OpenRunUpgradeSelection()
        {
            if (runEnded) return;
            choosingUpgrade = true;
            upgradeSelectionOpenedAt = Time.unscaledTime;
            System.Array.Clear(upgradeCardHoverTilts, 0, upgradeCardHoverTilts.Length);
            System.Array.Clear(upgradeCardWasHovered, 0, upgradeCardWasHovered.Length);
            RefreshPauseState();
        }

        private void TriggerDebugLevelUp()
        {
            if (choosingUpgrade || runEnded || showMetaTree)
            {
                return;
            }

            level++;
            experienceToNext = 7 + level * 4;
            OpenRunUpgradeSelection();
        }

        private void HandleRunUpgradeShortcuts()
        {
            for (int i = 0; i < TemporaryRunUpgrades.Length; i++)
            {
                KeyCode alphaKey = (KeyCode)((int)KeyCode.Alpha1 + i);
                KeyCode keypadKey = (KeyCode)((int)KeyCode.Keypad1 + i);
                if (Input.GetKeyDown(alphaKey) || Input.GetKeyDown(keypadKey))
                {
                    ChooseRunUpgrade(TemporaryRunUpgrades[i].upgrade);
                    return;
                }
            }
        }

        private static void UpdatePickupFountain(Pickup pickup, float dt)
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

        private void OnDisable()
        {
            Time.timeScale = 1f;
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

            if (showMetaTree)
            {
                DrawMetaTree();
                return;
            }

            if (runEnded)
            {
                DrawRunTimer();
                DrawRunResults();
                return;
            }

            DrawRunHudInformation();
            DrawRunTimer();

            if (GUI.Button(new Rect(Screen.width - 190, 20, 165, 36), "META TREE  [TAB]"))
            {
                ToggleMetaTree();
            }

            if (!choosingUpgrade && !showMetaTree && GUI.Button(new Rect(Screen.width - 190, 64, 165, 30), "DEBUG LEVEL UP"))
            {
                TriggerDebugLevelUp();
            }

            if (choosingUpgrade)
            {
                DrawRunUpgradeSelection();
            }

        }

        private void DrawRunHudInformation()
        {
            float width = Mathf.Min(650f, Screen.width - 44f);
            float x = Screen.width - width - 22f;
            float y = Screen.height - 78f;
            GUIStyle informationStyle = new GUIStyle(statStyle)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = Screen.width < 650 ? 12 : 15
            };
            GUI.Label(new Rect(x, y, width, 28f), $"Level {level}    경험치 {experience}/{experienceToNext}    획득 재화 +{runCurrency}    영구 재화 {metaCurrency}", informationStyle);
            GUI.Label(new Rect(x, y + 28f, width, 26f), $"Arcane Bolt {1 + damageLevel + runDamageBonus} DMG   |   Cast {Mathf.Max(0.18f, 0.62f - 0.035f * (hasteLevel + runHasteBonus)):0.00}s   |   Fortune +{(fortuneLevel + runFortuneBonus) * 3}%", informationStyle);
        }

        private void DrawRunTimer()
        {
            float width = Mathf.Min(480f, Mathf.Max(140f, Screen.width - 234f));
            float y = 24f;
            float x = 22f;
            float remaining = Mathf.Max(0f, activeRunDuration - elapsed);
            float fraction = remainingTimeFraction;
            Color accent = remaining <= 10f ? new Color(.96f, .38f, .23f) : new Color(.95f, .72f, .28f);

            Color artworkTint = remaining <= 10f ? new Color(1f, .52f, .35f) : Color.white;
            if (timerIconTexture != null)
            {
                DrawTimerTexture(new Rect(x, y - 3f, 26f, 30f), timerIconTexture, TimerIconUv, artworkTint);
            }
            Rect track = new Rect(x + 36f, y, width - 119f, 24f);
            DrawTimerTrack(track);
            Rect fill = new Rect(track.x + 5f, track.y + 4f, Mathf.Max(0f, track.width - 10f), track.height - 8f);
            if (timerFillTexture != null && fraction > 0f)
            {
                Rect fillUv = TimerFillUv;
                fillUv.width *= fraction;
                fill.width *= fraction;
                DrawTimerTexture(fill, timerFillTexture, fillUv, artworkTint);
            }

            // Display-only slider: no handle, keyboard focus or pointer interaction.
            timerTrackStyle ??= new GUIStyle(GUIStyle.none) { fixedHeight = 24f };
            bool wasEnabled = GUI.enabled;
            GUI.enabled = false;
            GUI.HorizontalSlider(track, fraction, 0f, 1f, timerTrackStyle, GUIStyle.none);
            GUI.enabled = wasEnabled;

            GUIStyle timeStyle = new GUIStyle(statStyle)
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = accent }
            };
            GUI.Label(new Rect(track.xMax + 12f, y - 3f, 71f, 30f), remaining.ToString("00.00", System.Globalization.CultureInfo.InvariantCulture), timeStyle);
        }

        private void DrawTimerTrack(Rect rect)
        {
            if (timerTrackTexture == null) return;
            // Three horizontal slices keep the end caps intact when the HUD width changes.
            float capWidth = Mathf.Min(8f, rect.width * .5f);
            float uvCapWidth = 36f / 2172f;
            DrawTimerTexture(new Rect(rect.x, rect.y, capWidth, rect.height), timerTrackTexture,
                new Rect(TimerTrackUv.x, TimerTrackUv.y, uvCapWidth, TimerTrackUv.height), Color.white);
            DrawTimerTexture(new Rect(rect.x + capWidth, rect.y, rect.width - capWidth * 2f, rect.height), timerTrackTexture,
                new Rect(TimerTrackUv.x + uvCapWidth, TimerTrackUv.y, TimerTrackUv.width - uvCapWidth * 2f, TimerTrackUv.height), Color.white);
            DrawTimerTexture(new Rect(rect.xMax - capWidth, rect.y, capWidth, rect.height), timerTrackTexture,
                new Rect(TimerTrackUv.xMax - uvCapWidth, TimerTrackUv.y, uvCapWidth, TimerTrackUv.height), Color.white);
        }

        private static void DrawTimerTexture(Rect rect, Texture2D texture, Rect uv, Color tint)
        {
            Color previousColor = GUI.color;
            GUI.color = previousColor * tint;
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
            GUI.color = previousColor;
        }

        private void DrawRunResults()
        {
            DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(.015f, .018f, .04f, .6f));
            // The approved preview has a tall panel; scale the complete composition from 1920 x 1080.
            const float panelWidth = 720f;
            const float panelHeight = 780f;
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = previousMatrix * Matrix4x4.TRS(new Vector3((Screen.width - panelWidth * scale) * .5f, (Screen.height - panelHeight * scale) * .5f, 0f), Quaternion.identity, Vector3.one * scale);
            bool returnToMap = false;
            bool restart = false;
            try
            {
                DrawResultArtwork(new Rect(0f, 0f, panelWidth, panelHeight), resultPanelTexture, ResultPanelUv, Color.white);
                float presentationElapsed = Mathf.Max(0f, Time.unscaledTime - resultPresentationOpenedAt);
                DrawResultEdgeGlow(panelWidth, panelHeight, presentationElapsed);
                Color ivory = new Color(.96f, .94f, .87f);
                Color subdued = new Color(.49f, .48f, .48f);
                GUIStyle heading = new GUIStyle(titleStyle) { fontSize = 36, alignment = TextAnchor.MiddleCenter, normal = { textColor = ivory } };
                GUIStyle body = new GUIStyle(statStyle) { fontSize = 26, alignment = TextAnchor.MiddleLeft, normal = { textColor = ivory } };
                GUIStyle value = new GUIStyle(body) { alignment = TextAnchor.MiddleRight };
                GUIStyle section = new GUIStyle(body) { fontSize = 30, fontStyle = FontStyle.Bold };
                GUIStyle mutedBody = new GUIStyle(body) { normal = { textColor = subdued } };
                GUIStyle mutedValue = new GUIStyle(value) { normal = { textColor = subdued } };
                GUI.Label(new Rect(44f, 24f, 632f, 48f), "진행 종료", heading);
                DrawResultDivider(92f);
                GUI.Label(new Rect(44f, 116f, 632f, 34f), "획득 재화", new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.58f, .55f, .50f) } });

                GUIStyle currencyStyle = new GUIStyle(heading) { fontSize = 68, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(.92f, .68f, .34f) } };
                // Reserve the final value's width, so the centered coin/value group never shifts as digits increase.
                string finalAmount = $"+{runCurrency:N0}";
                string amount = $"+{AnimatedResultCount(runCurrency, 0, presentationElapsed):N0}";
                float amountWidth = Mathf.Min(500f, currencyStyle.CalcSize(new GUIContent(finalAmount)).x);
                // Fit unusually large balances without letting the text escape the panel.
                while (currencyStyle.fontSize > 24 && currencyStyle.CalcSize(new GUIContent(finalAmount)).x > 500f) currencyStyle.fontSize--;
                amountWidth = Mathf.Min(amountWidth, currencyStyle.CalcSize(new GUIContent(finalAmount)).x);
                float currencyX = (panelWidth - amountWidth - 82f) * .5f;
                DrawResultSprite(new Rect(currencyX, 163f, 64f, 64f), coinSprite);
                GUI.Label(new Rect(currencyX + 82f, 151f, amountWidth + 4f, 90f), amount, currencyStyle);
                DrawResultDivider(258f);

                int totalKills = 0;
                foreach (int count in monsterKillCounts) totalKills += count;
                GUI.Label(new Rect(62f, 280f, 350f, 42f), "총 처치", section);
                GUI.Label(new Rect(446f, 280f, 210f, 42f), $"{AnimatedResultCount(totalKills, 1, presentationElapsed):N0}", new GUIStyle(value) { fontSize = 30, fontStyle = FontStyle.Bold });
                for (int i = 0; i < monsterKillCounts.Length; i++)
                {
                    float rowY = 342f + i * 57f;
                    bool empty = monsterKillCounts[i] == 0;
                    Color previousColor = GUI.color;
                    GUI.color = previousColor * (empty ? new Color(.5f, .5f, .5f) : Color.white);
                    DrawMonsterResultIcon(new Rect(74f, rowY + 3f, 36f, 36f), (MonsterKind)i);
                    GUI.color = previousColor;
                    GUI.Label(new Rect(136f, rowY, 300f, 42f), MonsterDisplayName((MonsterKind)i), empty ? mutedBody : body);
                    GUI.Label(new Rect(446f, rowY, 210f, 42f), $"{AnimatedResultCount(monsterKillCounts[i], i + 2, presentationElapsed):N0}", empty ? mutedValue : value);
                }
                DrawResultDivider(632f);
                returnToMap = DrawResultButton(new Rect(38f, 658f, 310f, 78f), "돌아가기", false);
                restart = DrawResultButton(new Rect(372f, 658f, 310f, 78f), "다시 시작", true);
            }
            finally { GUI.matrix = previousMatrix; }
            if (returnToMap) ReturnToGrowthMap();
            else if (restart) BeginRun();
        }

        private static void DrawResultDivider(float y)
        {
            DrawSolidRect(new Rect(46f, y, 628f, 2f), new Color(.55f, .50f, .43f, .65f));
        }

        private int AnimatedResultCount(int target, int sequence, float elapsedTime)
        {
            if (resultCountUpDuration <= 0f) return target;
            return RunResultPresentation.CountUp(target, elapsedTime, resultCountUpDuration, sequence * Mathf.Max(0f, resultCountUpStagger));
        }

        private void DrawResultEdgeGlow(float panelWidth, float panelHeight, float elapsedTime)
        {
            if (resultGlowTexture == null || resultGlowIntensity <= 0f) return;
            // Unscaled time keeps the count-up and glow playing while EndRun pauses gameplay.
            float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedTime / .35f));
            float pulse = resultGlowPulseDuration > 0f
                ? .9f + .1f * Mathf.Sin(elapsedTime * Mathf.PI * 2f / resultGlowPulseDuration)
                : 1f;
            Color tint = new Color(1f, 1f, 1f, resultGlowIntensity * fadeIn * pulse);
            Rect uv = new Rect(0f, 0f, 1f, 1f);
            // Draw beneath all labels and buttons: their colors and hit areas remain unchanged.
            DrawTimerTexture(new Rect(-70f, -85f, panelWidth + 140f, 170f), resultGlowTexture, uv, tint);
            DrawTimerTexture(new Rect(-70f, panelHeight - 85f, panelWidth + 140f, 170f), resultGlowTexture, uv, tint);
        }

        private static void DrawResultArtwork(Rect rect, Texture2D texture, Rect uv, Color tint)
        {
            if (texture != null) DrawTimerTexture(rect, texture, uv, tint);
            else
            {
                DrawSolidRect(rect, new Color(.055f, .065f, .095f));
                DrawOutline(rect, new Color(.72f, .52f, .25f), 2f);
            }
        }

        private bool DrawResultButton(Rect rect, string label, bool primary)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            DrawResultArtwork(rect, primary ? resultPrimaryButtonTexture : resultSecondaryButtonTexture,
                primary ? ResultPrimaryButtonUv : ResultSecondaryButtonUv, hovered ? new Color(1.12f, 1.12f, 1.12f) : Color.white);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.Label(rect, label, new GUIStyle(statStyle) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.96f, .94f, .87f) } });
            return clicked;
        }

        private void DrawMonsterResultIcon(Rect rect, MonsterKind kind)
        {
            Sprite sprite = kind switch { MonsterKind.Skeleton => skeletonSprite, MonsterKind.Goblin => goblinSprite, MonsterKind.Mushroom => mushroomSprite, MonsterKind.Boar => boarSprite, _ => slimeSprite };
            DrawResultSprite(rect, sprite);
        }

        private static void DrawResultSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null) return;
            Rect pixels = sprite.textureRect;
            Texture2D texture = sprite.texture;
            Rect uv = new Rect(pixels.x / texture.width, pixels.y / texture.height, pixels.width / texture.width, pixels.height / texture.height);
            float aspect = pixels.width / pixels.height;
            Rect image = aspect > 1f
                ? new Rect(rect.x, rect.center.y - rect.width / aspect * .5f, rect.width, rect.width / aspect)
                : new Rect(rect.center.x - rect.height * aspect * .5f, rect.y, rect.height * aspect, rect.height);
            GUI.DrawTextureWithTexCoords(image, texture, uv);
        }

        private static string MonsterDisplayName(MonsterKind kind)
        {
            return kind switch { MonsterKind.Skeleton => "스켈레톤", MonsterKind.Goblin => "고블린", MonsterKind.Mushroom => "버섯", MonsterKind.Boar => "멧돼지", _ => "슬라임" };
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
            DrawSolidRect(new Rect(0, 0, Screen.width, Screen.height), new Color(.015f, .02f, .06f, .76f));

            GUIStyle headingStyle = new GUIStyle(titleStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 34,
                normal = { textColor = new Color(.96f, .92f, 1f) }
            };
            GUIStyle subtitleStyle = new GUIStyle(statStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = new Color(.67f, .71f, .86f) }
            };
            GUI.Label(new Rect(0, 74f, Screen.width, 46f), "LEVEL UP", headingStyle);
            GUI.Label(new Rect(0, 118f, Screen.width, 26f), "CHOOSE ONE TEMPORARY BOON", subtitleStyle);

            bool compactLayout = Screen.width < 820;
            float gap = compactLayout ? 10f : 14f;
            float cardWidth = compactLayout
                ? Mathf.Min(460f, Screen.width - 40f)
                : Mathf.Min(236f, (Screen.width - 92f) / TemporaryRunUpgrades.Length);
            float cardHeight = compactLayout
                ? Mathf.Min(178f, Mathf.Max(108f, (Screen.height - 208f - gap * 2f) / TemporaryRunUpgrades.Length))
                : cardWidth * 1.5f;
            float groupHeight = compactLayout
                ? cardHeight * TemporaryRunUpgrades.Length + gap * (TemporaryRunUpgrades.Length - 1)
                : cardHeight;
            float startX = compactLayout
                ? (Screen.width - cardWidth) * .5f
                : (Screen.width - (cardWidth * TemporaryRunUpgrades.Length + gap * (TemporaryRunUpgrades.Length - 1))) * .5f;
            float startY = Mathf.Max(164f, (Screen.height - groupHeight) * .5f + 34f);

            for (int i = 0; i < TemporaryRunUpgrades.Length; i++)
            {
                Rect targetCardRect = compactLayout
                    ? new Rect(startX, startY + i * (cardHeight + gap), cardWidth, cardHeight)
                    : new Rect(startX + i * (cardWidth + gap), startY, cardWidth, cardHeight);
                float cardProgress = Mathf.Clamp01((Time.unscaledTime - upgradeSelectionOpenedAt - i * .075f) / .28f);
                float arrival = EaseOutBack(cardProgress);
                float belowScreenY = Screen.height + cardHeight + 36f;
                Rect animatedCardRect = targetCardRect;
                animatedCardRect.y = Mathf.LerpUnclamped(belowScreenY, targetCardRect.y, arrival);
                DrawRunUpgradeCard(animatedCardRect, TemporaryRunUpgrades[i], i, compactLayout, cardProgress >= .96f);
            }
        }

        private void DrawRunUpgradeCard(Rect cardRect, RunUpgradePresentation presentation, int index, bool compactLayout, bool isInteractive)
        {
            bool hovered = isInteractive && cardRect.Contains(Event.current.mousePosition);
            UpdateUpgradeCardHover(index, hovered);
            float tilt = upgradeCardHoverTilts[index];
            Matrix4x4 previousGuiMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(tilt, cardRect.center);
            GUIUtility.ScaleAroundPivot(Vector2.one * (hovered ? upgradeCardHoverScale : 1f), cardRect.center);
            DrawSolidRect(cardRect, new Color(.055f, .065f, .14f, .98f));
            DrawUpgradeCardFrame(cardRect, presentation.accent);

            bool longTitle = presentation.title.Length > 12;
            GUIStyle nameStyle = new GUIStyle(titleStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = compactLayout ? 16 : (longTitle ? 18 : 22),
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(.93f, .84f, .7f) }
            };
            GUIStyle iconStyle = new GUIStyle(titleStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = compactLayout ? 30 : 60,
                normal = { textColor = presentation.accent }
            };
            GUIStyle descriptionStyle = new GUIStyle(statStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = compactLayout ? 13 : 14,
                wordWrap = true,
                normal = { textColor = new Color(.88f, .88f, .88f) }
            };
            GUIStyle footerStyle = new GUIStyle(statStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(.68f, .68f, .68f) }
            };

            float horizontalPadding = compactLayout ? 30f : 34f;
            float titleY = compactLayout ? 10f : 27f;
            float titleHeight = compactLayout ? 22f : 52f;
            float iconY = compactLayout ? 33f : 91f;
            float iconHeight = compactLayout ? 30f : 82f;
            float upperDividerY = compactLayout ? 69f : 186f;
            float lowerDividerY = cardRect.height - (compactLayout ? 31f : 52f);
            GUI.Label(new Rect(cardRect.x + horizontalPadding, cardRect.y + titleY, cardRect.width - horizontalPadding * 2f, titleHeight), presentation.title, nameStyle);
            GUI.Label(new Rect(cardRect.x + horizontalPadding, cardRect.y + iconY, cardRect.width - horizontalPadding * 2f, iconHeight), presentation.icon, iconStyle);
            DrawSolidRect(new Rect(cardRect.x + horizontalPadding, cardRect.y + upperDividerY, cardRect.width - horizontalPadding * 2f, 1f), new Color(presentation.accent.r, presentation.accent.g, presentation.accent.b, .45f));
            GUI.Label(new Rect(cardRect.x + horizontalPadding, cardRect.y + upperDividerY + 12f, cardRect.width - horizontalPadding * 2f, lowerDividerY - upperDividerY - 18f), presentation.description, descriptionStyle);
            DrawSolidRect(new Rect(cardRect.x + horizontalPadding, cardRect.y + lowerDividerY, cardRect.width - horizontalPadding * 2f, 1f), new Color(presentation.accent.r, presentation.accent.g, presentation.accent.b, .35f));
            GUI.Label(new Rect(cardRect.x + horizontalPadding, cardRect.y + lowerDividerY + 7f, cardRect.width - horizontalPadding * 2f, 18f), $"{presentation.category}  ·  TEMP  [{index + 1}]", footerStyle);
            GUI.matrix = previousGuiMatrix;

            if (isInteractive && GUI.Button(cardRect, GUIContent.none, GUIStyle.none))
            {
                ChooseRunUpgrade(presentation.upgrade);
            }
        }

        private void UpdateUpgradeCardHover(int index, bool hovered)
        {
            if (hovered && !upgradeCardWasHovered[index])
            {
                PlayUpgradeCardHoverSound();
            }

            upgradeCardWasHovered[index] = hovered;
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            float targetTilt = hovered ? upgradeCardHoverLeftTiltAngle : 0f;
            upgradeCardHoverTilts[index] = Mathf.MoveTowards(
                upgradeCardHoverTilts[index],
                targetTilt,
                upgradeCardHoverTiltSpeed * Time.unscaledDeltaTime);
        }

        private void PlayUpgradeCardHoverSound()
        {
            // Audio hook: assign and play the future hover clip here. Intentionally silent for UI debugging.
        }

        private static float EaseOutBack(float progress)
        {
            const float overshoot = 1.45f;
            float offset = progress - 1f;
            return 1f + (overshoot + 1f) * offset * offset * offset + overshoot * offset * offset;
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void DrawUpgradeCardFrame(Rect rect, Color accent)
        {
            if (upgradeCardFrameTexture != null)
            {
                Color previousColor = GUI.color;
                GUI.color = accent;
                GUI.DrawTexture(rect, upgradeCardFrameTexture, ScaleMode.StretchToFill, true);
                GUI.color = previousColor;
                return;
            }

            // A plain fallback preserves the choice UI if the optional frame asset is unavailable.
            DrawOutline(rect, accent, 2f);
            DrawOutline(new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f), new Color(accent.r, accent.g, accent.b, .58f), 1f);
        }

        private static void DrawOutline(Rect rect, Color color, float thickness)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void DrawMetaTree()
        {
            // The tree owns the entire screen instead of appearing as an in-game popup.
            DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(.075f, .038f, .014f, 1f));
            Rect treeCanvas = new Rect(0f, 0f, Screen.width, Screen.height);
            HandleMetaTreeInput(treeCanvas);
            DrawMetaTreeCanvas(treeCanvas);
            // This is a navigation button, not a popup/header; the graph still owns the whole screen.
            Rect navigation = new Rect(Screen.width - 202f, Screen.height - 66f, 180f, 44f);
            if (DrawResultButton(navigation, runEnded ? "플레이 시작" : "플레이로 복귀", true))
            {
                if (runEnded) BeginRun();
                else ToggleMetaTree();
            }
        }

        private void HandleMetaTreeInput(Rect canvas)
        {
            Event currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            if (currentEvent.type == EventType.ScrollWheel && canvas.Contains(currentEvent.mousePosition))
            {
                float previousZoom = metaTreeZoom;
                metaTreeZoom = Mathf.Clamp(metaTreeZoom - currentEvent.delta.y * .06f, .62f, 1.45f);
                Vector2 canvasCenter = canvas.size * .5f;
                Vector2 pointer = currentEvent.mousePosition - canvas.position;
                metaTreePan += (pointer - canvasCenter - metaTreePan) * (1f - metaTreeZoom / previousZoom);
                currentEvent.Use();
            }

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 2 && canvas.Contains(currentEvent.mousePosition))
            {
                isPanningMetaTree = true;
                metaTreeLastPointer = currentEvent.mousePosition;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDrag && isPanningMetaTree)
            {
                metaTreePan += currentEvent.mousePosition - metaTreeLastPointer;
                metaTreeLastPointer = currentEvent.mousePosition;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 2)
            {
                isPanningMetaTree = false;
                currentEvent.Use();
            }
        }

        private void DrawMetaTreeCanvas(Rect canvas)
        {
            GUI.BeginGroup(canvas);
            Vector2 canvasSize = canvas.size;
            hoveredMetaTreeNode = -1;
            Event currentEvent = Event.current;
            Vector2 localPointer = currentEvent != null ? currentEvent.mousePosition - canvas.position : new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < TemporaryMetaTreeNodes.Length; i++)
            {
                if (GetMetaTreeNodeHitRect(TemporaryMetaTreeNodes[i], canvasSize).Contains(localPointer))
                {
                    hoveredMetaTreeNode = i;
                }
            }

            for (int i = 0; i < TemporaryMetaTreeNodes.Length; i++)
            {
                MetaTreeNodePresentation node = TemporaryMetaTreeNodes[i];
                if (node.parentIndex < 0)
                {
                    continue;
                }

                MetaTreeNodePresentation parent = TemporaryMetaTreeNodes[node.parentIndex];
                DrawMetaTreeConnector(GetMetaTreeNodeCenter(parent, canvasSize), GetMetaTreeNodeCenter(node, canvasSize), node.state == MetaTreeNodeState.Locked
                    ? new Color(.28f, .18f, .11f, .85f)
                    : new Color(.97f, .66f, .16f, .92f));
            }

            for (int i = 0; i < TemporaryMetaTreeNodes.Length; i++)
            {
                DrawMetaTreeNode(i, TemporaryMetaTreeNodes[i], canvasSize);
            }

            GUI.EndGroup();
            DrawMetaTreeHoverDescription();
        }

        private Vector2 GetMetaTreeNodeCenter(MetaTreeNodePresentation node, Vector2 canvasSize)
        {
            return canvasSize * .5f + metaTreePan + node.position * metaTreeZoom;
        }

        private Rect GetMetaTreeNodeRect(MetaTreeNodePresentation node, Vector2 canvasSize)
        {
            Vector2 nodeSize = node.parentIndex < 0 ? new Vector2(82f, 82f) : new Vector2(66f, 66f);
            nodeSize *= Mathf.Lerp(.9f, 1f, metaTreeZoom);
            return new Rect(GetMetaTreeNodeCenter(node, canvasSize) - nodeSize * .5f, nodeSize);
        }

        private static Rect GetMetaTreeNodeLabelRect(Rect nodeRect)
        {
            return new Rect(nodeRect.x - 44f, nodeRect.yMax + 5f, nodeRect.width + 88f, 20f);
        }

        private Rect GetMetaTreeNodeHitRect(MetaTreeNodePresentation node, Vector2 canvasSize)
        {
            Rect nodeRect = GetMetaTreeNodeRect(node, canvasSize);
            Rect labelRect = GetMetaTreeNodeLabelRect(nodeRect);
            return Rect.MinMaxRect(
                Mathf.Min(nodeRect.x, labelRect.x),
                Mathf.Min(nodeRect.y, labelRect.y),
                Mathf.Max(nodeRect.xMax, labelRect.xMax),
                Mathf.Max(nodeRect.yMax, labelRect.yMax));
        }

        private static void DrawMetaTreeConnector(Vector2 from, Vector2 to, Color color)
        {
            Vector2 direction = to - from;
            float length = direction.magnitude;
            if (length < .01f)
            {
                return;
            }

            Matrix4x4 previousMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, from);
            DrawSolidRect(new Rect(from.x, from.y - 1.5f, length, 3f), color);
            GUI.matrix = previousMatrix;
        }

        private void DrawMetaTreeNode(int index, MetaTreeNodePresentation node, Vector2 canvasSize)
        {
            Rect nodeRect = GetMetaTreeNodeRect(node, canvasSize);
            Rect labelRect = GetMetaTreeNodeLabelRect(nodeRect);
            Rect hitRect = GetMetaTreeNodeHitRect(node, canvasSize);
            bool isSelected = selectedMetaTreeNode == index;
            bool isHovered = hoveredMetaTreeNode == index;
            Color fill = node.state == MetaTreeNodeState.Locked
                ? new Color(.105f, .064f, .035f, .99f)
                : new Color(.16f, .083f, .027f, .99f);
            Color border = node.state == MetaTreeNodeState.Locked
                ? new Color(.36f, .27f, .18f, .9f)
                : new Color(.96f, .66f, .19f);

            if (isSelected)
            {
                fill = Color.Lerp(fill, node.accent, .15f);
            }
            if (isHovered)
            {
                fill = Color.Lerp(fill, new Color(1f, .73f, .23f), .2f);
            }

            DrawSolidRect(nodeRect, fill);
            DrawOutline(nodeRect, border, isSelected || isHovered ? 2.5f : 1.25f);

            GUIStyle icon = new GUIStyle(titleStyle)
            {
                fontSize = Mathf.RoundToInt((node.parentIndex < 0 ? 31f : 25f) * Mathf.Lerp(.9f, 1f, metaTreeZoom)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = node.state == MetaTreeNodeState.Locked ? new Color(.42f, .34f, .26f) : new Color(.99f, .77f, .31f) }
            };
            GUIStyle title = new GUIStyle(statStyle)
            {
                fontSize = Mathf.RoundToInt(12f * Mathf.Lerp(.9f, 1f, metaTreeZoom)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = node.state == MetaTreeNodeState.Locked ? new Color(.46f, .37f, .29f) : new Color(.9f, .74f, .49f) }
            };
            GUI.Label(nodeRect, MetaTreeNodeIcon(node.id), icon);
            GUI.Label(labelRect, node.title, title);

            if (GUI.Button(hitRect, GUIContent.none, GUIStyle.none))
            {
                selectedMetaTreeNode = index;
            }
        }

        private void DrawMetaTreeHoverDescription()
        {
            if (hoveredMetaTreeNode < 0 || Event.current == null)
            {
                return;
            }

            MetaTreeNodePresentation node = TemporaryMetaTreeNodes[hoveredMetaTreeNode];
            const float width = 300f;
            const float height = 102f;
            Vector2 pointer = Event.current.mousePosition;
            float tooltipX = pointer.x + 22f;
            float tooltipY = pointer.y + 20f;
            if (tooltipX + width > Screen.width - 18f)
            {
                tooltipX = pointer.x - width - 22f;
            }
            if (tooltipY + height > Screen.height - 18f)
            {
                tooltipY = pointer.y - height - 20f;
            }

            Rect rect = new Rect(Mathf.Clamp(tooltipX, 18f, Screen.width - width - 18f), Mathf.Clamp(tooltipY, 18f, Screen.height - height - 18f), width, height);
            DrawSolidRect(rect, new Color(.075f, .038f, .014f, .99f));
            DrawOutline(rect, node.state == MetaTreeNodeState.Locked ? new Color(.42f, .29f, .17f, .95f) : new Color(.98f, .66f, .17f, .98f), 1.5f);

            GUIStyle section = new GUIStyle(statStyle)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(.98f, .67f, .19f) }
            };
            GUIStyle nodeName = new GUIStyle(titleStyle)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(.96f, .82f, .61f) }
            };
            GUIStyle body = new GUIStyle(statStyle)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(.72f, .58f, .43f) }
            };

            float x = rect.x + 22f;
            GUI.Label(new Rect(x, rect.y + 10f, 120f, 20f), MetaTreeNodeStatus(node.state), section);
            GUI.Label(new Rect(x, rect.y + 28f, rect.width - 44f, 26f), node.title, nodeName);
            GUI.Label(new Rect(x, rect.y + 59f, rect.width - 44f, 32f), $"{node.subtitle}\n효과와 비용은 테이블 연결 후 표시됩니다.", body);
        }

        private static string MetaTreeNodeIcon(string id)
        {
            return id switch
            {
                "ROOT" => "◆",
                "COMBAT" => "✦",
                "SURVIVAL" => "+",
                "GROWTH" => "$",
                "ARCANE" => "✧",
                "PRECISION" => "◈",
                "VITALITY" => "♥",
                "WARD" => "◇",
                "FORTUNE" => "?",
                _ => "○"
            };
        }

        private static string MetaTreeNodeStatus(MetaTreeNodeState state)
        {
            return state switch
            {
                MetaTreeNodeState.Completed => "해금 완료",
                MetaTreeNodeState.Reachable => "연결 가능",
                _ => "데이터 대기"
            };
        }

        private readonly struct RunUpgradePresentation
        {
            public readonly RunUpgrade upgrade;
            public readonly string icon;
            public readonly string category;
            public readonly string title;
            public readonly string description;
            public readonly Color accent;

            public RunUpgradePresentation(RunUpgrade upgrade, string icon, string category, string title, string description, Color accent)
            {
                this.upgrade = upgrade;
                this.icon = icon;
                this.category = category;
                this.title = title;
                this.description = description;
                this.accent = accent;
            }
        }

        private readonly struct MetaTreeNodePresentation
        {
            public readonly string id;
            public readonly string title;
            public readonly string subtitle;
            public readonly Vector2 position;
            public readonly int parentIndex;
            public readonly MetaTreeNodeState state;
            public readonly Color accent;

            public MetaTreeNodePresentation(string id, string title, string subtitle, Vector2 position, int parentIndex, MetaTreeNodeState state, Color accent)
            {
                this.id = id;
                this.title = title;
                this.subtitle = subtitle;
                this.position = position;
                this.parentIndex = parentIndex;
                this.state = state;
                this.accent = accent;
            }
        }

        private enum RunUpgrade { Damage, Haste, Fortune }
        private enum MetaUpgrade { Damage, Haste, Fortune }
        private enum MetaTreeNodeState { Completed, Reachable, Locked }
    }
}
