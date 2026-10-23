using System;
using UnityEngine;

namespace ProjectDM
{
    /// <summary>Biome artwork. Monster slots retain their existing progression/reward identities.</summary>
    [CreateAssetMenu(menuName = "Project DM/스테이지", fileName = "Stage")]
    public sealed class StageDefinition : ScriptableObject
    {
        [InspectorName("스테이지 번호"), Min(1)] public int stageId;
        [InspectorName("스테이지 이름")] public string displayName;
        [InspectorName("바닥 룰타일")] public DungeonGroundRuleTile ground;
        [InspectorName("배경 색상")] public Color background;
        [InspectorName("몬스터 구성 (기존 성장 슬롯 순서)")] public MonsterVisual[] monsters;
        [InspectorName("보스 이름")] public string bossName;
        [InspectorName("보스 미리보기 프레임")] public Sprite[] bossFrames;
        [InspectorName("보스 미리보기 크기"), Min(.01f)] public float bossScale = 1.8f;

        [Serializable]
        public sealed class MonsterVisual
        {
            [InspectorName("표시 이름")] public string displayName;
            [InspectorName("걷기 프레임")] public Sprite[] frames;
            [InspectorName("프레임 간격 (초)"), Min(.02f)] public float frameSeconds = .16f;
            [InspectorName("그림 크기 보정"), Min(.01f)] public float visualScale = 1f;
            [InspectorName("이동 방향으로 좌우 반전")] public bool mirrorToMovement = true;
            [InspectorName("애니메이터 (비어 있으면 프레임 자동 재생)")] public RuntimeAnimatorController animatorController;
        }

        public void ValidateContent()
        {
            if (stageId < 1 || string.IsNullOrWhiteSpace(displayName) || ground == null || !ground.HasRuleTilePalette)
                throw new InvalidOperationException($"스테이지 {stageId}: 이름 또는 3×3 바닥 룰타일이 누락되었습니다.");
            if (monsters == null || monsters.Length != 5)
                throw new InvalidOperationException($"스테이지 {stageId}: 몬스터 슬롯은 5개여야 합니다.");
            foreach (MonsterVisual monster in monsters)
                if (monster == null || string.IsNullOrWhiteSpace(monster.displayName) || !HasFrames(monster.frames) ||
                    !Positive(monster.frameSeconds) || !Positive(monster.visualScale))
                    throw new InvalidOperationException($"스테이지 {stageId}: 몬스터 이름/프레임이 누락되었습니다.");
            if (string.IsNullOrWhiteSpace(bossName) || !HasFrames(bossFrames) || bossScale <= 0 || float.IsNaN(bossScale) || float.IsInfinity(bossScale))
                throw new InvalidOperationException($"스테이지 {stageId}: 보스 리소스가 누락되었습니다.");
        }

        private static bool Positive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool HasFrames(Sprite[] frames) => frames != null && frames.Length >= 2 && Array.TrueForAll(frames, frame => frame != null);
    }
}
