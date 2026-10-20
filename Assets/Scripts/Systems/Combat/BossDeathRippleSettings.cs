using UnityEngine;

namespace ProjectDM
{
    [CreateAssetMenu(menuName = "Project DM/보스 처치 파동 설정")]
    public sealed class BossDeathRippleSettings : ScriptableObject
    {
        [InspectorName("공간 파동 머티리얼")] public Material material;
        [InspectorName("중간보스 파동 반경"), Min(.1f)] public float miniBossRadius = 3.2f;
        [InspectorName("보스 파동 반경"), Min(.1f)] public float bossRadius = 5;
        [InspectorName("파동 지속 시간"), Min(.1f)] public float duration = 1.25f;
        [InspectorName("파동 색상")] public Color tint = new(.48f, .64f, .7f, .14f);
        [InspectorName("공간 굴절 강도 (화면 픽셀)"), Range(0, 6)] public float distortion = 2;
        [InspectorName("최대 동시 파동"), Range(1, 8)] public int maximumActive = 4;
    }
}
