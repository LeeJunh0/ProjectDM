using UnityEngine;

namespace ProjectDM
{
    // Runtime entity state is intentionally separate from the game coordinator.
    internal sealed class Enemy
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Animator animator;
        public Sprite baseSprite;
        public Sprite alternateSprite;
        public float animationTime;
        public Sprite[] animationFrames;
        public float frameSeconds = .16f;
        public int animationFrame;
        public bool mirrorToMovement = true;

        internal void AdvanceAnimation(float elapsed)
        {
            if (elapsed <= 0 || animationFrames == null || animationFrames.Length < 2 || renderer == null) return;
            // Keep the remainder and catch up every elapsed frame, including long update steps.
            animationTime += elapsed;
            int steps = Mathf.FloorToInt((animationTime + .000001f) / frameSeconds);
            if (steps == 0) return;
            animationTime = Mathf.Max(0, animationTime - steps * frameSeconds);
            animationFrame = (animationFrame + steps % animationFrames.Length) % animationFrames.Length;
            renderer.sprite = animationFrames[animationFrame];
        }
        public float hitPoints;
        public bool dead;
        public float speed;
        public MonsterKind kind;
        public EnemyRank rank;
        public float rewardMultiplier = 1;
        public float hitRadiusMultiplier = 1;
        public int chillStacks;
        public int burnStacks;
        public float chillRemaining, freezeRemaining, chillSlow;
    }

    internal sealed class Projectile
    {
        public Transform transform;
        public Vector2 direction;
        public float damage;
        public float lifetime;
        public int pierces;
        public readonly System.Collections.Generic.HashSet<Enemy> hits = new();
    }

    internal sealed class Pickup
    {
        public Transform transform;
        public PickupKind kind;
        public int grade;
        public float amount;
        public bool isInteracting;
        public float interactionRemaining;
        public bool isLaunching;
        public bool vacuum;
        public bool removed;
        public bool batched;
        public int serial;
        public Sprite batchSprite;
        public float floatPhase, visualScale;
        public float bornAt;
        private Vector3 position;
        internal Vector3 Position { get => transform!=null?transform.position:position; set {position=value;if(transform!=null)transform.position=value;} }
        internal float VisualScale {get=>visualScale;set{visualScale=value;if(transform!=null)transform.localScale=Vector3.one*value;}}
        public Vector3 launchOrigin;
        public Vector3 launchDestination;
        public float launchElapsed;
        public float launchDuration;
        public float launchArcHeight;
        public float baseScale;
    }

    internal enum PickupKind { Experience, Currency, Chest, Mana, Magnet }
    internal enum MonsterKind { Slime, Skeleton, Goblin, Mushroom, Boar }
    internal enum EnemyRank { Normal, MiniBoss, Boss }
}
