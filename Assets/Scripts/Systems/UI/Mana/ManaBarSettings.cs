using UnityEngine;

namespace ProjectDM
{
    public enum ManaVisualState
    {
        [InspectorName("기본 · 청색")] Normal,
        [InspectorName("각성 · 보라색")] Awakened,
        [InspectorName("분노 · 빨간색")] Rage,
        [InspectorName("과열 · 주황색")] Overheated
    }

    [CreateAssetMenu(menuName = "Project DM/액체 마나 바 설정", fileName = "ManaBarSettings")]
    public sealed class ManaBarSettings : ScriptableObject
    {
        [Header("리소스·상태 미리보기")]
        [InspectorName("액체 셰이더")] public Shader liquidShader;
        [InspectorName("미리보기 상태 (게임 판정과 무관)")] public ManaVisualState previewState;
        [Header("상태별 액체 색상")]
        [InspectorName("기본 색상")] public Color normalColor = new(.1f, .48f, 1);
        [InspectorName("각성 색상")] public Color awakenedColor = new(.62f, .2f, 1);
        [InspectorName("분노 색상")] public Color rageColor = new(1, .12f, .15f);
        [InspectorName("과열 색상")] public Color overheatedColor = new(1, .42f, .06f);
        [InspectorName("색상 전환 시간"), Min(0)] public float colorTransitionDuration = .35f;
        [Header("액체·빛 연출")]
        [InspectorName("흐름 움직임 속도"), Min(0)] public float waveSpeed = 1.4f;
        [InspectorName("흐름 강조 강도"), Range(0, .3f)] public float waveAmplitude = .07f;
        [InspectorName("흐름 빈도"), Range(1, 12)] public float waveFrequency = 3;
        [InspectorName("내부 흐름 속도"), Min(0)] public float flowSpeed = .65f;
        [InspectorName("발광 세기"), Range(0, 2)] public float glowStrength = .4f;
        [InspectorName("마나 변동 시 흐름 반응 강도"), Range(0, 3)] public float reactionStrength = 1;
        [InspectorName("흐름 반응 진정 시간"), Min(.01f)] public float reactionDuration = .55f;
        [InspectorName("부족 경고 기준"), Range(0, 1)] public float lowManaThreshold = .25f;
        [InspectorName("부족 시 빛 점멸 강도"), Range(0, 1)] public float lowManaPulse = .2f;

        internal Color StateColor(ManaVisualState state) => state switch
        {
            ManaVisualState.Awakened => awakenedColor,
            ManaVisualState.Rage => rageColor,
            ManaVisualState.Overheated => overheatedColor,
            _ => normalColor
        };
    }
}
