using UnityEngine;

namespace ProjectDM
{
    /// <summary>Configuration only, never runtime state. Refreshed from existing serialized GameManager fields.</summary>
    internal sealed class GameSettings
    {
        internal float startingMana = 40f;
        internal int initialStage = 1;
        internal float resultCountUpDuration = 1.1f;
        internal float resultCountUpStagger = .07f;
        internal float resultGlowIntensity = .16f;
        internal float resultGlowPulseDuration = 3f;
        internal Camera gameplayCamera;
        internal GameFieldBounds fieldBounds;
        internal Transform playerSpawnPoint;
        internal float pickupCollectionRadius = .28f;
        internal MonsterSpawnAreaPreview monsterSpawnArea;
        internal PlayerMovementAreaPreview playerMovementArea;
        internal DungeonFloorTilemap dungeonFloor;
        internal float cameraFollowSpeed = 8f;
        internal Vector2 fieldSize = new(25f, 17f);
        internal Vector2 playerMovementBounds = new(8.4f, 4.8f);
        internal float monsterSpawnMinimumDistance = 10f;
        internal float monsterSpawnMaximumDistance = 12f;
        internal float experiencePickupScaleMultiplier = 3f;
        internal float currencyPickupScaleMultiplier = 3f;
        internal float chestPickupScaleMultiplier = 1f;
        internal float manaPickupScaleMultiplier = 3f;
        internal float collectibleFloatYAmplitude = .06f;
        internal float collectibleFloatLoopDuration = 1.2f;
        internal AnimationCurve experiencePickupFloatYCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(.25f, 1f), new Keyframe(.5f, 0f), new Keyframe(.75f, -.67f), new Keyframe(1f, 0f));
        internal AnimationCurve currencyPickupFloatYCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(.25f, 1f), new Keyframe(.5f, 0f), new Keyframe(.75f, -.67f), new Keyframe(1f, 0f));
        internal float chestInteractionDuration = .8f;
        internal AnimationCurve chestInteractionYCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(.35f, .18f), new Keyframe(.7f, .06f), new Keyframe(1f, 0f));
        internal AnimationCurve chestInteractionScaleCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(.28f, 1.15f), new Keyframe(.65f, .95f), new Keyframe(1f, 1f));
        internal float pickupFountainDuration = .28f;
        internal float pickupFountainDistance = .62f;
        internal float pickupFountainArcHeight = .34f;
        internal float upgradeCardHoverLeftTiltAngle = -4f;
        internal float upgradeCardHoverTiltSpeed = 180f;
        internal float upgradeCardHoverScale = 1.035f;
        internal TextAsset prototypeBalanceTable;
        internal TextAsset prototypeSkillTable;
        internal TextAsset prototypeGrowthTable;
        internal bool useTableStartingMana = true;
        internal bool useTableSpawnDistances = true;
        internal bool enablePrototypeDebug = true;
        internal bool prototypeSandbox;
        internal bool showCombatDamage = true;
        internal float combatDamageDuration = .7f;
        internal float combatDamageHeight = .35f;
        internal float combatDamageRise = .65f;
        internal float combatDamageScatter = .22f;
        internal int combatDamageFontSize = 24;
        internal float combatDamageStartScale = .55f;
        internal float combatDamagePeakScale = 1.3f;
        internal float combatDamageEndScale = .55f;
        internal float combatDamageGrowDuration = .1f;
        internal float combatDamagePeakHoldDuration = .04f;
        internal float combatDamageShrinkDuration = .3f;
        internal float combatDamageScaleSpeed = 1f;
        internal float combatCriticalScale = 1.4f;
        internal int combatDamageMaximum = 128;
        internal Color combatAttackColor = new Color(.96f, .94f, .86f);
        internal Color combatReceivedColor = new Color(1, .32f, .28f);
        internal Color combatCriticalColor = new Color(1, .74f, .26f);
        internal bool enableCriticalHits = true;
        internal bool enablePlayerContactDamage = true;
    }
}
