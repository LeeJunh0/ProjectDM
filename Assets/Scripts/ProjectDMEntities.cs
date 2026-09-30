using UnityEngine;

namespace ProjectDM
{
    // Runtime entity state is intentionally separate from the game coordinator.
    internal sealed class Enemy
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Sprite baseSprite;
        public Sprite alternateSprite;
        public float animationTime;
        public bool showAlternate;
        public int hitPoints;
        public float speed;
    }

    internal sealed class Projectile
    {
        public Transform transform;
        public Vector2 direction;
        public int damage;
        public float lifetime;
    }

    internal sealed class Pickup
    {
        public Transform transform;
        public PickupKind kind;
        public int amount;
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
}
