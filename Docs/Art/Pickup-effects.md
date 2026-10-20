# 드랍템의 잔잔한 상승 입자

2026-10-20 요청: 리소스에 어울리는 색의 작은 입자/빛이 아래에서 위로 잔잔하게 올라오는 연출.

경험치 5종, 재화 5종, 상자, 마나의 총 12개 프리팹에 `Visual/RisingMotes` 자식을 붙입니다. Unity 기본 `ParticleSystem`과 `PickupAmbientVfx`를 사용합니다. 바닥 가까이에서 소량 방출되어 위로 천천히 올라가며 나타났다 사라집니다. 경험치는 파랑/청록/보라/초록/금색, 재화는 동색/은색/금색/에메랄드/자수정색, 마나는 차분한 파랑, 상자는 옅은 금색입니다.

기본값은 초당 2개, 수명 약 1.4~2초, 최대 6개, 최대 알파 0.28입니다. 실제 동시 입자는 보통 2~4개입니다. 이미 줄여둔 이미지 발광은 유지하며 실시간 광원이나 가산 합성 없이 부드러운 알파 합성을 사용합니다. 새 래스터 이미지 없이 작은 원형 입자를 `PickupMote.shader`로 그립니다. 모든 프리팹이 하나의 머티리얼을 공유합니다.

## 조절

프리팹의 `Visual/RisingMotes`에서 `PickupAmbientVfx`의 한글 항목을 조절합니다.

- 리소스에 맞춘 입자 색상, 최대 불투명도
- 초당 입자 수, 상승 속도, 수명, 최대 동시 입자 수

속도·크기·발생 위치는 스프라이트 높이를 기준으로 맞추고 픽업 크기 배율도 따릅니다. 로컬 공간에서 시뮬레이션하므로 부유·분수·자석 이동을 따라가며 잔상을 바닥에 남기지 않습니다. 스케일된 시간을 사용해 레벨업·성장 지도·결과창의 일시정지를 따릅니다. 풀 반환 시 모든 입자를 비우고 다시 사용할 때 새로 시작합니다. 자체 난수 시드로 전투 난수는 소비하지 않습니다. 화면 밖에서는 네이티브 파티클 시뮬레이션을 멈춥니다.

`Project DM/Install Quiet Pickup Effects`는 12개 프리팹을 재설정합니다. 기존 프리팹 생성 도구에서도 호출해 내용 재설치 후 효과가 빠지지 않게 합니다. 원본 스프라이트/애니메이션과 획득/보상 계산을 유지합니다.

## 리소스와 확인

- `Assets/Scripts/Systems/Pickups/PickupAmbientVfx.cs`
- `Assets/GameContent/Art/Skills/PickupMote.shader` 및 `PickupMote.mat`
- `Assets/Editor/ProjectDMPickupEffectsSetup.cs`
- `Assets/Editor/QA/PickupEffectsRegression.cs`

`ProjectDM.Editor.ProjectDMPickupEffectsSetup.RunBatch`는 설치 후 실제 12종 프리팹의 상승 방향, 입자 상한·색상·셰이더, 스케일된 시간, 풀 정리/재사용, 전투 난수 불변을 확인합니다. 마나 수집과 기존 재화/경험치 검증도 실행합니다. `Logs/PickupEffects-preview.png`는 실제 프리팹과 ParticleSystem을 격리 카메라로 렌더한 예시입니다. 실제 Main 플레이는 기존 `PrototypePlaySmoke`로 별도 확인합니다.

2026-10-20 실행 결과: 상승 입자 169개, 마나 드랍 17개, 재화 등급 371개, 시스템 구성 6,485개 검증 통과. Main 플레이 스모크에서도 실제 Addressables 마나 프리팹의 입자 재생과 수집 시 입자 정리를 확인했습니다. 이미지에서 사용하는 하단 피벗을 기준으로 입자 시작점을 잡으며, 상자는 Animator가 시작하기 전에도 닫힌 첫 프레임으로 크기를 계산합니다.
