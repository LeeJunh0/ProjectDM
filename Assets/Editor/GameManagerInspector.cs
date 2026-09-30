using UnityEditor;
using UnityEngine;

namespace ProjectDM.Editor
{
    /// <summary>Korean-facing Inspector labels for all GameManager tuning properties.</summary>
    [CustomEditor(typeof(GameManager))]
    public sealed class GameManagerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSection("씬 배치 (에디터 미리보기)");
            DrawProperty("gameplayCamera", "게임플레이 카메라", "게임 화면을 표시하는 메인 카메라입니다.");
            DrawProperty("fieldBounds", "필드 범위", "게임 필드와 카메라 제한의 기준이 되는 범위입니다.");
            DrawProperty("playerSpawnPoint", "플레이어 시작 위치", "게임 시작 시 플레이어가 생성되는 위치입니다.");
            DrawProperty("monsterSpawnArea", "몬스터 스폰 영역", "몬스터 생성 영역 미리보기입니다.");
            DrawProperty("playerMovementArea", "플레이어 이동 영역", "플레이어가 이동할 수 있는 영역 미리보기입니다.");
            DrawProperty("dungeonFloor", "던전 바닥 타일맵", "던전 바닥 룰타일을 표시하는 타일맵입니다.");

            DrawSection("카메라 추적");
            DrawProperty("cameraFollowSpeed", "카메라 추적 속도", "초당 카메라 이동 거리입니다. 0이면 카메라가 고정됩니다.");

            DrawSection("픽업 비주얼 크기");
            DrawProperty("experiencePickupScaleMultiplier", "경험치 픽업 크기 배율", "경험치 픽업 비주얼의 크기 배율입니다. 기본값은 원본의 3배입니다.");
            DrawProperty("currencyPickupScaleMultiplier", "재화 픽업 크기 배율", "재화 픽업 비주얼의 크기 배율입니다. 기본값은 원본의 3배입니다.");
            DrawProperty("chestPickupScaleMultiplier", "보물상자 픽업 크기 배율", "보물상자 픽업 비주얼의 크기 배율입니다.");

            DrawSection("경험치·재화 부유 모션");
            DrawProperty("collectibleFloatYAmplitude", "부유 높이", "픽업 비주얼이 부유하는 로컬 Y축 최대 높이입니다.");
            DrawProperty("collectibleFloatLoopDuration", "부유 반복 시간", "부유 모션이 한 번 반복되는 시간(초)입니다.");
            DrawProperty("experiencePickupFloatYCurve", "경험치 부유 곡선", "경험치 픽업 비주얼의 로컬 Y축 부유 곡선입니다.");
            DrawProperty("currencyPickupFloatYCurve", "재화 부유 곡선", "재화 픽업 비주얼의 로컬 Y축 부유 곡선입니다.");

            DrawSection("보물상자 상호작용 모션");
            DrawProperty("chestInteractionDuration", "상호작용 시간", "보물상자 획득 전 상호작용 애니메이션 시간(초)입니다.");
            DrawProperty("chestInteractionYCurve", "상호작용 Y축 곡선", "보물상자 획득 시 적용되는 로컬 Y축 모션입니다.");
            DrawProperty("chestInteractionScaleCurve", "상호작용 크기 곡선", "보물상자 획득 시 적용되는 크기 모션입니다.");

            DrawSection("픽업 분수 등장 연출");
            DrawProperty("pickupFountainDuration", "분수 연출 시간", "경험치·재화 픽업이 처치 지점에서 튀어나오는 시간(초)입니다.");
            DrawProperty("pickupFountainDistance", "흩어지는 거리", "픽업이 분수처럼 흩어져 착지하는 거리입니다.");
            DrawProperty("pickupFountainArcHeight", "포물선 높이", "픽업이 튀어나오는 동안 위로 솟는 포물선 높이입니다.");

            DrawSection("레벨업 카드 호버");
            DrawProperty("upgradeCardHoverLeftTiltAngle", "왼쪽 기울기 각도", "음수 값일수록 호버한 카드가 왼쪽으로 더 기울어집니다.");
            DrawProperty("upgradeCardHoverTiltSpeed", "기울기 전환 속도", "카드가 호버 기울기까지 도달하는 속도입니다.");
            DrawProperty("upgradeCardHoverScale", "호버 확대 배율", "카드에 마우스를 올렸을 때 적용되는 확대 배율입니다.");

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawSection(string title)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void DrawProperty(string propertyName, string label, string tooltip)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
            }
        }
    }
}
