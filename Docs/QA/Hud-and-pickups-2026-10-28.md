# 2026-10-28 HUD와 획득 연출 작업

- 우측 상단에 배경 없는 보유 코인 HUD를 배치했다.
- 재화·마나·경험치 드롭은 서로 다른 곡선과 반짝이는 궤적으로 해당 HUD에 날아간다. 마지막 구간에서 투명해지며 비행 크기는 유지한다.
- 실제 보상은 즉시 반영하고 HUD 표시는 비행 도착 후 0.28초간 부드럽게 반영한다. 화면 이동 시 남은 표시 지연은 해제한다.
- 필드 마나 드롭 크기를 비행 그림 크기에 맞추고, 결과창 전환의 페이드를 제거했다.
- 경험치바는 바닥 전체 폭의 높이 30픽셀 바로 표시하며 레벨을 안쪽 중앙에 배치했다.
- 타입별 초상화 4종을 연결했다. 최종 초상화는 176×176이며 경험치바 바로 위에 붙고 마나바는 오른쪽에 배치된다.
- 기본 마법사 64×64 캐릭터 시안을 Design/Characters/Mage64-v1에 저장했다. 게임 캐릭터는 교체하지 않았다. 초상화 원본과 생성 프롬프트도 Design/Portraits/v1에 보존했다.

## 검증

- 격리 Unity 6000.3.18f1에서 변경된 런타임 C# 소스의 해시 일치를 확인하고 컴파일 및 PickupFlightRegression 4,742개 검사 통과.
- PowerShell 회귀 검사 10종 통과: CombatDamageScale, CompactNumber, CurrencyDrop, LootGrade, ObjectiveLine, PermanentKillResults, PrototypeProgression, RunResultCountUp, SystemComposition, UpgradeGrades. 각 검사는 중복 C# 타입 로딩을 피하도록 별도 프로세스에서 실행했다.
- CurrentBalanceParity는 입력인 Temp/Balance-current.json이 없어 실행하지 않았다. 이번 작업에서 밸런스 테이블은 변경하지 않았다.
- 획득 연출과 HUD는 격리 렌더 장면에서 확인했다. Portrait-mana-hud-2026-10-28.png는 초상화 크기 88픽셀·경험치와 간격 10픽셀이던 중간 시점의 캡처다. 최종 176픽셀·간격 0 배치는 회귀 검사로 검증했다.
- 실제 메인 장면 플레이 리뷰와 배포용 플레이어 빌드는 수행하지 않았다.
