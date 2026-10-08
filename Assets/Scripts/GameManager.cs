using System;
using System.Collections;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Unity composition root: serialized configuration, explicit injection and frame/lifecycle routing only.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField, Min(1f), Tooltip("한 회차의 플레이 제한 시간(초)입니다. 변경한 값은 다음 회차부터 적용됩니다.")]
        private float runDuration = 40f;

        [SerializeField, Range(1, 2), Tooltip("기본 시작 스테이지입니다. 1은 초원, 2는 기존 어두운 석재 맵입니다. 플레이 중 변경하면 다음 런에 적용됩니다.")]
        private int initialStage = 1;

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

        [SerializeField, Range(-25f, 0f), Tooltip("음수 값일수록 호버한 레벨업 카드가 왼쪽으로 더 기울어집니다.")]
        private float upgradeCardHoverLeftTiltAngle = -4f;

        [SerializeField, Min(1f), Tooltip("레벨업 카드가 호버 기울기까지 도달하는 속도입니다.")]
        private float upgradeCardHoverTiltSpeed = 180f;

        [SerializeField, Range(1f, 1.2f), Tooltip("레벨업 카드에 마우스를 올렸을 때 적용되는 확대 배율입니다.")]
        private float upgradeCardHoverScale = 1.035f;

        [SerializeField] private TextAsset prototypeBalanceTable;

        [SerializeField] private TextAsset prototypeSkillTable;

        [SerializeField] private TextAsset prototypeGrowthTable;

        [SerializeField] private bool useTableRunDuration = true;

        [SerializeField] private bool useTableSpawnDistances = true;

        [SerializeField] private bool enablePrototypeDebug = true;

        [SerializeField, Tooltip("플레이 시작 시의 값을 사용합니다. 켜면 재화/처치/성장을 실제 저장하지 않습니다.")]
        private bool prototypeSandbox;

        [SerializeField] private bool showCombatDamage = true;

        [SerializeField, Min(.05f)] private float combatDamageDuration = .7f;

        [SerializeField, Min(0)] private float combatDamageHeight = .35f;

        [SerializeField, Min(0)] private float combatDamageRise = .65f;

        [SerializeField, Min(0)] private float combatDamageScatter = .22f;

        [SerializeField, Range(10, 64)] private int combatDamageFontSize = 24;

        [SerializeField, Min(.05f)] private float combatDamageStartScale = .55f;

        [SerializeField, Min(.05f)] private float combatDamagePeakScale = 1.3f;

        [SerializeField, Min(.05f)] private float combatDamageEndScale = .55f;

        [SerializeField, Min(0)] private float combatDamageGrowDuration = .1f;

        [SerializeField, Min(0)] private float combatDamagePeakHoldDuration = .04f;

        [SerializeField, Min(0)] private float combatDamageShrinkDuration = .3f;

        [SerializeField, Min(.01f)] private float combatDamageScaleSpeed = 1f;

        [SerializeField, Range(1, 2.5f)] private float combatCriticalScale = 1.4f;

        [SerializeField, Range(16, 512)] private int combatDamageMaximum = 128;

        [SerializeField] private Color combatAttackColor = new Color(.95f, .96f, 1);

        [SerializeField] private Color combatReceivedColor = new Color(1, .32f, .28f);

        [SerializeField] private Color combatCriticalColor = new Color(1, .78f, .22f);

        [SerializeField] private bool enableCriticalHits = true;

        [SerializeField] private bool enablePlayerContactDamage = true;

        private GameSettings settings;
        private SaveService save;
        private GameTables tables;
        private ProgressionService progression;
        private WorldArtAssets art;
        private PlayerWorld playerWorld;
        private GameplayCamera cameraSystem;
        private RunController run;
        private EnemySystem enemiesSystem;
        private CombatSystem combat;
        private SkillSystem skills;
        private CombatStats stats;
        private PlayerStats playerStats;
        private PickupSystem pickupsSystem;
        private LevelUpController selection;
        private BossMilestone boss;
        private GameNotifications notifications;
        private DamagePresentation presentation;
        private PlayerCombatSystem playerCombat;
        private UiGraphics graphics;
        private RunHudView hud;
        private RunResultView results;
        private LevelUpView levelUpView;
        private GrowthGraphView growthView;
        private DebugView debugView;
        private BossArrivalView bossView;
        private ProjectDMAssetLoader assetLoader;
        private GameObjectPool objectPool;
        private bool isInitialized;
        private string prototypeError;
        private Action onBossDue;
        private StageSession stages;

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

        private void SynchronizeSettings()
        {
            settings.runDuration = runDuration;
            settings.initialStage = initialStage;
            settings.resultCountUpDuration = resultCountUpDuration;
            settings.resultCountUpStagger = resultCountUpStagger;
            settings.resultGlowIntensity = resultGlowIntensity;
            settings.resultGlowPulseDuration = resultGlowPulseDuration;
            settings.gameplayCamera = gameplayCamera;
            settings.fieldBounds = fieldBounds;
            settings.playerSpawnPoint = playerSpawnPoint;
            settings.monsterSpawnArea = monsterSpawnArea;
            settings.playerMovementArea = playerMovementArea;
            settings.dungeonFloor = dungeonFloor;
            settings.cameraFollowSpeed = cameraFollowSpeed;
            settings.fieldSize = fieldSize;
            settings.playerMovementBounds = playerMovementBounds;
            settings.monsterSpawnMinimumDistance = monsterSpawnMinimumDistance;
            settings.monsterSpawnMaximumDistance = monsterSpawnMaximumDistance;
            settings.experiencePickupScaleMultiplier = experiencePickupScaleMultiplier;
            settings.currencyPickupScaleMultiplier = currencyPickupScaleMultiplier;
            settings.chestPickupScaleMultiplier = chestPickupScaleMultiplier;
            settings.collectibleFloatYAmplitude = collectibleFloatYAmplitude;
            settings.collectibleFloatLoopDuration = collectibleFloatLoopDuration;
            settings.experiencePickupFloatYCurve = experiencePickupFloatYCurve;
            settings.currencyPickupFloatYCurve = currencyPickupFloatYCurve;
            settings.chestInteractionDuration = chestInteractionDuration;
            settings.chestInteractionYCurve = chestInteractionYCurve;
            settings.chestInteractionScaleCurve = chestInteractionScaleCurve;
            settings.pickupFountainDuration = pickupFountainDuration;
            settings.pickupFountainDistance = pickupFountainDistance;
            settings.pickupFountainArcHeight = pickupFountainArcHeight;
            settings.upgradeCardHoverLeftTiltAngle = upgradeCardHoverLeftTiltAngle;
            settings.upgradeCardHoverTiltSpeed = upgradeCardHoverTiltSpeed;
            settings.upgradeCardHoverScale = upgradeCardHoverScale;
            settings.prototypeBalanceTable = prototypeBalanceTable;
            settings.prototypeSkillTable = prototypeSkillTable;
            settings.prototypeGrowthTable = prototypeGrowthTable;
            settings.useTableRunDuration = useTableRunDuration;
            settings.useTableSpawnDistances = useTableSpawnDistances;
            settings.enablePrototypeDebug = enablePrototypeDebug;
            settings.prototypeSandbox = prototypeSandbox;
            settings.showCombatDamage = showCombatDamage;
            settings.combatDamageDuration = combatDamageDuration;
            settings.combatDamageHeight = combatDamageHeight;
            settings.combatDamageRise = combatDamageRise;
            settings.combatDamageScatter = combatDamageScatter;
            settings.combatDamageFontSize = combatDamageFontSize;
            settings.combatDamageStartScale = combatDamageStartScale;
            settings.combatDamagePeakScale = combatDamagePeakScale;
            settings.combatDamageEndScale = combatDamageEndScale;
            settings.combatDamageGrowDuration = combatDamageGrowDuration;
            settings.combatDamagePeakHoldDuration = combatDamagePeakHoldDuration;
            settings.combatDamageShrinkDuration = combatDamageShrinkDuration;
            settings.combatDamageScaleSpeed = combatDamageScaleSpeed;
            settings.combatCriticalScale = combatCriticalScale;
            settings.combatDamageMaximum = combatDamageMaximum;
            settings.combatAttackColor = combatAttackColor;
            settings.combatReceivedColor = combatReceivedColor;
            settings.combatCriticalColor = combatCriticalColor;
            settings.enableCriticalHits = enableCriticalHits;
            settings.enablePlayerContactDamage = enablePlayerContactDamage;
        }

        private void Awake()
        {
            Time.timeScale = 1f;
            Application.targetFrameRate = 60;
            settings = new GameSettings();
            SynchronizeSettings();
            art = new WorldArtAssets();
            graphics = new UiGraphics();
            try
            {
                tables = GameTables.LoadResources(settings);
                prototypeBalanceTable = settings.prototypeBalanceTable;
                prototypeSkillTable = settings.prototypeSkillTable;
                prototypeGrowthTable = settings.prototypeGrowthTable;
                save = new SaveService(tables, new PlayerPrefsProgressStore(), prototypeSandbox);
                save.Load();
                objectPool = gameObject.AddComponent<GameObjectPool>();
                PrototypeSkillVisuals effects = gameObject.AddComponent<PrototypeSkillVisuals>();
                ComposeServices(tables, save, objectPool, effects);
                cameraSystem.SetupCamera();
                gameplayCamera = settings.gameplayCamera;
                assetLoader = gameObject.AddComponent<ProjectDMAssetLoader>();
                StartCoroutine(InitializeGame());
            }
            catch (Exception exception)
            {
                prototypeError = "프로토타입 초기화 실패: " + exception.Message;
                if (save != null) save.progress = null; // Never flush an unrecognized/corrupt save.
            }
        }

        // Tests inject in-memory persistence and isolated scene objects through the same factory.
        internal void ComposeServices(GameTables tables, SaveService save, GameObjectPool pool, PrototypeSkillVisuals effects)
        {
            if (settings == null) { settings = new GameSettings(); SynchronizeSettings(); }
            this.tables = tables;
            this.save = save;
            objectPool = pool;
            art ??= new WorldArtAssets();
            graphics ??= new UiGraphics();
            progression = new ProgressionService(save, tables);
            playerStats = new PlayerStats(tables, progression);
            playerWorld = new PlayerWorld(settings, tables, art, playerStats);
            cameraSystem = new GameplayCamera(settings, playerWorld);
            notifications = new GameNotifications();
            stages = new StageSession(settings, art);
            presentation = new DamagePresentation(settings);
            run = new RunController(settings, save, tables, progression, playerWorld, ResetWorld, () => enemiesSystem.SpawnPrototypeInitialEnemies(), () => cameraSystem.SnapCameraToPlayer(), () => growthView?.ResetNavigation(), () => { if (selection != null) selection.choosingUpgrade = false; }, () => selection != null && selection.choosingUpgrade, stages);
            boss = new BossMilestone(save, art, playerWorld, run);
            pickupsSystem = new PickupSystem(settings, save, tables, progression, art, playerWorld, run, pool, amount => selection.AddExperience(amount), playerStats);
            enemiesSystem = new EnemySystem(settings, save, tables, progression, art, playerWorld, run, pickupsSystem, boss, notifications, pool, effects, presentation.ShowCombatDamage);
            skills = new SkillSystem(tables, progression, art, run, enemiesSystem, effects, () => combat.RollCriticalHit(), () => stats.MainDamage, playerStats);
            stats = new CombatStats(tables, progression, skills);
            combat = new CombatSystem(settings, tables, progression, art, playerWorld, enemiesSystem, skills, stats, pool, playerStats);
            playerCombat = new PlayerCombatSystem(settings, tables, playerWorld, run, enemiesSystem, combat, presentation, playerStats);
            selection = new LevelUpController(tables, progression, run, skills);
            hud = new RunHudView(settings, save, tables, run, enemiesSystem, stats, selection, playerCombat, graphics);
            results = new RunResultView(settings, tables, art, run, boss, playerCombat, graphics);
            levelUpView = new LevelUpView(settings, art, selection, graphics);
            growthView = new GrowthGraphView(settings, save, tables, progression, art, run, notifications, graphics);
            debugView = new DebugView(save, tables, run, enemiesSystem, combat, skills, boss, notifications, playerCombat, graphics);
            bossView = new BossArrivalView(tables, run, boss, graphics);
            selection.onOpened = levelUpView.ResetHover;
            onBossDue = () => boss.ArriveAtPrototypeBoss();
        }

        private IEnumerator InitializeGame()
        {
            yield return assetLoader.LoadAsync();
            if (!assetLoader.IsReady)
            {
                Debug.LogError(assetLoader.Error ?? "Project DM Addressables bootstrap failed.");
                yield break;
            }
            art.runtimeAssets = assetLoader.Assets;
            art.LoadSprites(art.runtimeAssets);
            try { stages.LoadContent(); }
            catch (Exception exception) { prototypeError = exception.Message; yield break; }
            playerWorld.CreatePlayer();
            cameraSystem.SnapCameraToPlayer();
            run.BeginRun();
            isInitialized = true;
        }

        private void ResetWorld()
        {
            enemiesSystem.Reset();
            combat.Reset();
            pickupsSystem.Reset();
            progression.ResetSnapshot();
            skills.Reset();
            presentation.Reset();
            playerCombat.ResetPlayerCombat();
            selection.Reset();
            boss.Reset();
            stages.ApplySelected();
            dungeonFloor = settings.dungeonFloor;
            run.timeRewardsThisRun = 0;
        }

        private void Update()
        {
            if (!isInitialized) return;
            SynchronizeSettings();
            save.FlushPrototypeProgressIfDue();
            if (selection.choosingUpgrade) { selection.HandleRunUpgradeShortcuts(); return; }
            if (Input.GetKeyDown(KeyCode.Tab)) run.ToggleMetaTree();
            if (run.showMetaTree)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { run.showMetaTree = false; run.RefreshPauseState(); }
                return;
            }
            if (run.runEnded) return;
            float dt = run.TickClock(Time.deltaTime);
            playerWorld.MovePlayer(dt);
            cameraSystem.FollowPlayerWithCamera(dt);
            if (monsterSpawnArea != null) monsterSpawnArea.transform.position = playerWorld.player.position;
            enemiesSystem.SpawnEnemies(run.elapsed);
            combat.FireAtNearestEnemy(run.elapsed);
            enemiesSystem.UpdateEnemies(dt);
            combat.UpdateProjectiles(dt);
            presentation.Tick(dt);
            skills.UpdatePrototypeEffects(dt);
            playerCombat.UpdatePlayerContactDamage(dt);
            if (run.runEnded) return;
            pickupsSystem.UpdatePickups(dt);
            run.CompleteIfDue(onBossDue);
        }

        private void OnGUI()
        {
            graphics?.SetupGuiStyles();
            if (!isInitialized)
            {
                string message = assetLoader != null && !string.IsNullOrEmpty(assetLoader.Error)
                    ? "ASSET LOAD FAILED — check the Console"
                    : (!string.IsNullOrEmpty(prototypeError) ? prototypeError : "LOADING ADDRESSABLE ASSETS...");
                graphics.DrawLoadingMessage(message);
                return;
            }

            if (run.showMetaTree)
            {
                growthView.DrawMetaTree();
                return;
            }

            if (run.runEnded)
            {
                hud.DrawRunTimer();
                if (boss.bossArrived && !boss.showBossStatistics) bossView.DrawPrototypeBossArrival();
                else results.DrawRunResults();
                return;
            }

            hud.DrawRunHudInformation();
            hud.DrawRunTimer();
            presentation.DrawCombatDamage();
            hud.DrawPlayerHealth();

            hud.DrawNavigationControls();
            if (enablePrototypeDebug && !selection.choosingUpgrade) debugView.DrawPrototypeDebugTools();
            debugView.DrawPrototypeNotices();
            if (selection.choosingUpgrade)
            {
                levelUpView.DrawRunUpgradeSelection();
            }
        }

        private void OnApplicationPause(bool paused) { if (paused) save?.FlushPrototypeProgress(); }
        private void OnApplicationQuit() { save?.FlushPrototypeProgress(); }
        private void OnDisable() { save?.FlushPrototypeProgress(); Time.timeScale = 1f; }
    }
}
