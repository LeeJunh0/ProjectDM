using UnityEngine;

namespace ProjectDM
{
    /// <summary>Approved artwork and presentation-only settings. Never changes combat tables.</summary>
    [CreateAssetMenu(menuName = "Project DM/스킬 이펙트 설정", fileName = "SkillVisuals")]
    public sealed class SkillVisualAssets : ScriptableObject
    {
        [Header("사용 리소스")]
        [InspectorName("연쇄 번개 스프라이트")] public Sprite chain;
        [InspectorName("스플래쉬 스프라이트")] public Sprite splash;
        [InspectorName("운석 스프라이트")] public Sprite meteor;
        [InspectorName("빙결 공전 조각 스프라이트")] public Sprite frostShard;
        [InspectorName("운석 충돌 색상 변환 머티리얼")] public Material impactMaterial;

        [Header("연쇄 번개")]
        [InspectorName("번개 시작 위치 (이미지 비율)")] public Vector2 chainStart = new(.14f, .11f);
        [InspectorName("번개 끝 위치 (이미지 비율)")] public Vector2 chainEnd = new(.9f, .88f);
        [InspectorName("번개 표시 시간"), Min(.01f)] public float chainDuration = .18f;
        [InspectorName("번개 투명도"), Range(0, 1)] public float chainOpacity = .65f;
        [InspectorName("번개 굵기·퍼짐 배율"), Min(.1f), Tooltip("적 사이 연결 길이는 유지하고, 연결 축에 수직인 방향만 확대합니다.")]
        public float chainWidthMultiplier = 2.5f;

        [Header("스플래쉬·충돌 확산")]
        [InspectorName("스플래쉬 표시 시간"), Min(.01f)] public float splashDuration = .3f;
        [InspectorName("스플래쉬 투명도"), Range(0, 1)] public float splashOpacity = .6f;
        [InspectorName("확산 시작 크기"), Range(.01f, 1)] public float burstStartScale = .18f;
        [InspectorName("타격 범위까지 펼쳐지는 시간"), Min(.001f), Tooltip("이 시간 안에 실제 타격 지름까지 펼친 후, 최대 크기를 유지하며 사라집니다.")]
        public float burstExpansionDuration = .06f;
        [InspectorName("이미지 속 원형 영역의 지름 비율"), Range(.1f, 1)] public float burstArtworkDiameter = .9f;

        [Header("운석")]
        [InspectorName("운석 낙하 높이"), Min(0)] public float meteorFallHeight = 3;
        [InspectorName("운석 이미지 너비"), Min(.01f)] public float meteorWidth = 1.45f;
        [InspectorName("운석 낙하 회전 각도")] public float meteorRotation = 55;
        [InspectorName("운석 타격점 (이미지 비율)")] public Vector2 meteorAnchor = new(.35f, .3f);
        [InspectorName("운석 예고 투명도"), Range(0, 1)] public float warningOpacity = .09f;
        [InspectorName("운석 충돌 표시 시간"), Min(.01f)] public float impactDuration = .4f;
        [InspectorName("운석 충돌 투명도"), Range(0, 1)] public float impactOpacity = .65f;
        [InspectorName("운석 충돌 색상")] public Color impactColor = new(1, .4f, .08f, 1);

        [Header("빙결")]
        [InspectorName("얼음 조각의 이미지 내 지름 비율"), Range(.1f, 1)] public float frostArtworkDiameter = .94f;

        [Header("성능")]
        [InspectorName("최대 동시 스프라이트 수"), Min(1)] public int maxActiveEffects = 128;
    }
}
