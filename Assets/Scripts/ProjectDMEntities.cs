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
    }

    internal enum PickupKind { Experience, Currency, Chest }
}
