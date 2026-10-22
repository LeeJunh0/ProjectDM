using UnityEngine;

namespace ProjectDM
{
    [CreateAssetMenu(menuName = "Project DM/캐릭터 성장 트리 색상")]
    public sealed class GrowthGraphTheme : ScriptableObject
    {
        [InspectorName("마법미사일 트리 강조색")] public Color missile = new(.9f, .82f, .64f);
        [InspectorName("화염 트리 강조색")] public Color flame = new(.94f, .44f, .24f);
        [InspectorName("번개 트리 강조색")] public Color lightning = new(.71f, .64f, .95f);
        [InspectorName("빙결 트리 강조색")] public Color frost = new(.46f, .8f, .93f);

        internal Color Accent(string characterId) => characterId switch
        {
            "projectile" => missile, "flame" => flame, "lightning" => lightning, "frost" => frost,
            _ => new Color(.96f, .66f, .19f)
        };

        internal Color Tint(Color original, string characterId)
        {
            if (characterId != "projectile" && characterId != "flame" && characterId != "lightning" && characterId != "frost") return original;
            Color.RGBToHSV(original, out _, out float saturation, out float brightness);
            Color.RGBToHSV(Accent(characterId), out float hue, out float themeSaturation, out _);
            // Preserve each state's brightness and opacity, including hidden/locked silhouettes.
            Color result = Color.HSVToRGB(hue, Mathf.Clamp01(saturation * themeSaturation / .8f), brightness);
            result.a = original.a;
            return result;
        }
    }
}
