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

            DrawSection("프로토타입 테이블");
            DrawProperty("prototypeBalanceTable", "기본·몬스터·스폰 테이블", "비워두면 Resources/ProjectDM/Data/PrototypeBalance.json을 사용합니다. 수정한 테이블은 플레이 재시작 시 적용됩니다.");
            DrawProperty("prototypeSkillTable", "런 스킬 테이블", "비워두면 PrototypeSkills.json을 사용합니다. 레벨업 선택 조건과 실제 공격 효과 수치입니다.");
            DrawProperty("prototypeGrowthTable", "영구 성장 그래프 테이블", "비워두면 PrototypeGrowth.json을 사용합니다. 가격, 최대 단계, 해금 조건, 그래프 위치를 지정합니다.");
            DrawProperty("useTableRunDuration", "테이블 시작 시간 사용", "켜면 기본 40초 + 영구 시간 성장입니다. 끄면 아래 진행 제한 시간 + 영구 시간 성장을 사용합니다.");
            DrawProperty("useTableSpawnDistances", "테이블 스폰 거리 사용", "켜면 초기 3~5, 리필 5~8 거리입니다. 끄면 기존 씬의 스폰 영역 설정을 사용합니다.");
            DrawProperty("enablePrototypeDebug", "런 디버그 버튼 표시", "레벨업, 재화 지급, 임시 스킬 획득, 발동 100%, 스폰 단계, 보스 도달 테스트 도구입니다.");
            DrawProperty("prototypeSandbox", "실험 모드 (저장하지 않음)", "플레이 시작 전에 켜세요. 기존 진행을 읽지만 실험 중 얻은 재화·처치·구매는 실제 저장하지 않습니다. 플레이 도중 변경은 다음 플레이 시작에 적용됩니다.");

            DrawSection("런 진행 시간");
            DrawProperty("runDuration", "시작 시간 수동 설정 (초)", "테이블 시작 시간 사용을 끈 경우의 기본 시간입니다. 영구 시간 성장이 더해집니다. 변경한 값은 다음 회차에 적용됩니다.");

            DrawSection("전투 데미지 숫자");
            DrawProperty("showCombatDamage", "데미지 숫자 표시", "실제 적중 지점에 받은 피해량을 표시합니다.");
            DrawProperty("combatDamageDuration", "숫자 표시 시간 (초)", "숫자가 떠오른 뒤 사라지는 시간입니다. 일시정지 중에는 멈춥니다.");
            DrawProperty("combatDamageFontSize", "기본 숫자 크기", "1920×1080 기준 글꼴 크기입니다.");
            DrawProperty("combatDamageStartScale", "시작 크기 배율", "기본 숫자 크기에 곱하는 등장 크기입니다. 기본 0.55배로 작게 시작합니다.");
            DrawProperty("combatDamagePeakScale", "최대 크기 배율", "커지는 연출의 목표 크기입니다. 기본 1.3배입니다.");
            DrawProperty("combatDamageEndScale", "마지막 크기 배율", "작아진 뒤 사라질 때까지 유지하는 크기입니다. 기본 0.55배입니다.");
            DrawProperty("combatDamageGrowDuration", "커지는 시간 (초)", "작게 등장한 숫자가 최대 크기에 도달하는 시간입니다. 짧을수록 빠릅니다. 0이면 즉시 커집니다.");
            DrawProperty("combatDamagePeakHoldDuration", "최대 크기 유지 시간 (초)", "최대 크기에서 잠깐 멈추는 시간입니다. 0이면 바로 작아집니다.");
            DrawProperty("combatDamageShrinkDuration", "작아지는 시간 (초)", "최대 크기에서 마지막 크기까지 줄어드는 시간입니다. 0이면 즉시 작아집니다.");
            DrawProperty("combatDamageScaleSpeed", "크기 연출 속도 배율", "1은 기본 속도, 2는 두 배 속도입니다. 커짐·유지·줄어듦에 적용하며 숫자 표시 시간과 이동 속도는 바꾸지 않습니다. 연출 합계가 표시 시간보다 길면 전체 구간을 비례 축소합니다.");
            DrawProperty("combatDamageHeight", "등장 높이", "적중 지점보다 위에 나타나는 월드 높이입니다.");
            DrawProperty("combatDamageRise", "떠오르는 거리", "숫자가 사라질 때까지 위로 이동하는 거리입니다.");
            DrawProperty("combatDamageScatter", "좌우 분산 거리", "겹치는 숫자를 좌우로 흩어 표시합니다. 전투 확률 판정에는 영향을 주지 않습니다.");
            DrawProperty("combatCriticalScale", "치명타 크기 배율", "치명타 표시의 크기 배율입니다. 실제 치명타 판정은 별도 전투 규칙입니다.");
            DrawProperty("combatDamageMaximum", "동시 표시 최대 수", "숫자 표시만 제한하며 실제 피해나 스킬 발동은 제한하지 않습니다.");
            DrawProperty("combatAttackColor", "공격 피해 숫자 색상", "몬스터에게 가한 피해의 숫자 색상입니다.");
            DrawProperty("combatReceivedColor", "플레이어 피격 숫자 색상", "플레이어가 받은 실제 피해의 숫자 색상입니다.");
            DrawProperty("combatCriticalColor", "치명타 숫자 색상", "실제로 치명타가 발생한 적중의 숫자 색상입니다.");
            DrawProperty("enableCriticalHits", "기본 공격 치명타 사용", "치명타 확률과 배율은 기본 테이블의 criticalChance/criticalMultiplier입니다. 다중 공격의 각 적중도 따로 판정합니다.");
            DrawProperty("enablePlayerContactDamage", "플레이어 접촉 피해 사용", "체력·접촉 판정 거리·피격 무적 시간과 몬스터 접촉 피해량은 기본 테이블에서 설정합니다.");

            DrawSection("결과 통계 연출");
            DrawProperty("resultCountUpDuration", "숫자 증가 시간 (초)", "각 통계 숫자가 0에서 실제 값까지 올라가는 시간입니다. 0이면 즉시 표시합니다.");
            DrawProperty("resultCountUpStagger", "숫자 시작 간격 (초)", "재화부터 몬스터별 처치 수까지 차례로 증가를 시작하는 간격입니다. 0이면 동시에 시작합니다.");
            DrawProperty("resultGlowIntensity", "주황빛 세기", "결과창 위·아래 그라데이션의 불투명도입니다. 0이면 빛을 숨깁니다.");
            DrawProperty("resultGlowPulseDuration", "빛 밝기 반복 시간 (초)", "빛이 천천히 밝아졌다 어두워지는 주기입니다. 0이면 밝기를 고정합니다.");

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
