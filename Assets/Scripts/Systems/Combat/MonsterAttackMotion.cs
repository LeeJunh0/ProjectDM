using UnityEngine;

namespace ProjectDM
{
    public enum MonsterAttackKind { None, Spore, ArmorSweep, ChainStrike, Charge }

    // Body transforms only. Telegraphs, projectiles and hit tests belong to EnemySpecialAttacks.
    internal static class MonsterAttackMotion
    {
        internal static Vector2 Scale(MonsterAttackKind kind, float age)
        {
            float p = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .6f));
            if (age < .6f) return kind == MonsterAttackKind.Spore ? new Vector2(1 + .13f * p, 1 + .1f * p) :
                kind == MonsterAttackKind.ArmorSweep ? new Vector2(1 + .05f * p, 1 - .07f * p) : Vector2.one;
            float k = Mathf.Exp(-(age - .6f) * 11);
            return kind == MonsterAttackKind.Spore ? new Vector2(1 + .08f * k, 1 - .1f * k) :
                kind == MonsterAttackKind.ArmorSweep ? new Vector2(1 + .065f * k, 1 - .04f * k) : Vector2.one;
        }

        internal static float Shake(MonsterAttackKind kind, float age)
        {
            if (kind != MonsterAttackKind.Spore && kind != MonsterAttackKind.ArmorSweep) return 0;
            if (age < .4f) return 0;
            return age < .6f ? Mathf.Sin(age * 65) * .008f : Mathf.Sin((age - .6f) * 45) * Mathf.Exp(-(age - .6f) * 11) * .025f;
        }
    }
}
