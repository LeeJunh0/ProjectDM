using UnityEngine;

namespace ProjectDM
{
    internal enum CombatDamageElement { None, Flame, Lightning, Frost }

    [CreateAssetMenu(menuName = "Project DM/데미지 숫자 외형")]
    public sealed class CombatDamageAppearance : ScriptableObject
    {
        [InspectorName("숫자 전용 픽셀 글꼴")] public Font font;
        [InspectorName("얇은 외곽선 색상")] public Color outlineColor = new(.26f, .24f, .23f, .8f);
        [InspectorName("외곽선 두께 (1080p 기준)"), Range(.5f, 2)] public float outlineWidth = 1;

        [Header("속성별 일반 피해 / 치명타")]
        [InspectorName("화염 일반 색상")] public Color flameNormal = new(.96f, .43f, .22f);
        [InspectorName("화염 치명타 색상")] public Color flameCritical = new(1, .78f, .43f);
        [InspectorName("번개 일반 색상")] public Color lightningNormal = new(.68f, .65f, .97f);
        [InspectorName("번개 치명타 색상")] public Color lightningCritical = new(.88f, .82f, 1);
        [InspectorName("빙결 일반 색상")] public Color frostNormal = new(.43f, .78f, .91f);
        [InspectorName("빙결 치명타 색상")] public Color frostCritical = new(.76f, .94f, 1);

        internal Color ResolveColor(CombatDamageElement element, CombatDamageKind kind, Color attack, Color received, Color critical)
        {
            if (kind == CombatDamageKind.Received) return received;
            bool crit = kind == CombatDamageKind.Critical;
            return element switch
            {
                CombatDamageElement.Flame => crit ? flameCritical : flameNormal,
                CombatDamageElement.Lightning => crit ? lightningCritical : lightningNormal,
                CombatDamageElement.Frost => crit ? frostCritical : frostNormal,
                _ => crit ? critical : attack
            };
        }
    }
}
