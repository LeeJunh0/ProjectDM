# 2026-11-05 커밋 전 검증

## 변경 범위

- 기존 마력탄을 참고한 형태·잔광과 크기 조절, 편집한 이펙트 설정 보존.
- 성장지도 기본 배율·축소 한계, 빈 곳 왼쪽 드래그, 디버그 접기, 홀딩 배경 간격 개선.
- 마나·경험치 표시 즉시 반영, 해당 획득 비행 연출 단축.
- 결과창 전용 연금 가마, 런 수입 참가비와 최고 수입 80% 기준, 독립 50% 판정과 1→2→5 조각 보상.
- 전체 화면 작업실, 금액별 금화 투입·금빛 용해, 전용 32×32 금화, 연출 상단 여백 조정.

## 통과한 검사

| 검사 | 결과 |
|---|---|
| SystemCompositionRegression.ps1 | 6,011 assertions PASS |
| GrowthHoldInteractionRegression.cs | 112 assertions PASS |
| GrowthNavigationRegression.cs | 19 assertions PASS |
| PickupFlightRegression.cs | 4,904 checks PASS |
| PixelFxRegression.cs | 247 assertions PASS, 2,000개 이펙트 단일 아틀라스 배치 |
| RelicForgeRegression.cs | 426 assertions PASS |
| PixelFxPreviewRegression.cjs | 15개 클립·60프레임·런타임 아틀라스 일치 PASS |
| PixelFxTuningRegression.mjs | 사용자 편집 설정·잘림·미리보기·잘못된 입력 거부 PASS |
| RelicForgePreviewRegression.mjs | 비동기 로딩·재생·성공/실패·투입량 PASS |
| pixelfx relic-forge | 5클립 PASS, 경고 0 |
| RelicForgeRuntimeSmoke.cs | 실제 Main에서 결과→제련→성공→추가 결제 없는 재응축 실패 PASS |

Unity 검사는 격리 프로젝트와 실험 모드에서 실행해 사용자 저장을 변경하지 않았다. 제련 리소스의 2D·Point 임포트와 금화 PNG의 32×32 RGBA·투명 여백도 확인했다. 속도를 편집한 클립의 반복 주기를 고정 0.5초로 가정하던 검사와 임시 폴더가 없으면 실패하던 테스트 준비 코드를 수정한 뒤 재검사했다.

## 화면과 남은 확인

[금화 투입](2026-11-05-alchemy-fit-coins.png) · [성공과 상단 여백](2026-11-05-alchemy-fit-success.png).

커밋 전 코드 검토에서 추가 기능 수정이 필요한 문제는 발견하지 않았다. 장시간 플레이 밸런스와 다른 화면 비율의 수동 확인은 이번 검증에 포함하지 않았다. 유물 장착·능력치 효과는 요청대로 아직 구현 범위에 포함하지 않는다.
