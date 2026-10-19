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
        public bool showAlternate;
        public float hitPoints;
        public bool dead;
        public float speed;
        public MonsterKind kind;
        public int chillStacks;
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
        public Vector3 launchOrigin;
        public Vector3 launchDestination;
        public float launchElapsed;
        public float launchDuration;
        public float launchArcHeight;
        public float baseScale;
    }

    internal enum PickupKind { Experience, Currency, Chest }
    internal enum MonsterKind { Slime, Skeleton, Goblin, Mushroom, Boar }
}
