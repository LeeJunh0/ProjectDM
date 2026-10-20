# 전체 캐릭터·몬스터·스킬·드랍 아트 톤 (2026-10-20)

사용자 요청: "각 리소스들 전부 이런식으로 수정해서 반영해줘 빛 약하게 아웃라인도 연하게 캐릭터 몬스터 스킬 드랍템 전부".

내장 `image_gen` 이미지 편집으로 전체 16개 PNG를 같은 방향으로 맞춥니다. 앞서 확정한 마나·빙결 2개를 유지하고 나머지 14개 시트/단일 이미지를 수정합니다. 투명 배경과 픽셀 형태를 유지하며, 각 재질에 맞는 연하고 채도가 낮은 외곽선을 사용합니다. 보라색 의상은 연한 보라, 피부는 피부 그림자색, 나무·금화는 갈색/황토색, 돌·은화는 회색, 녹색 몸체는 올리브색으로 처리합니다. 눈·입 등 식별 정보는 읽히도록 유지합니다.

빛은 흰 중심광·반짝임·후광을 낮춥니다. 테두리만 밝아져 표본의 평균 밝기가 올라가는 경우도 있으므로 전체 평균 휘도 감소를 일괄 목표로 삼지 않습니다. 최종 발광은 실제 Unity SpriteRenderer의 알파 합성으로 확인합니다.

## 저장 경로

모든 경로는 프로젝트 루트 기준입니다. 기존 파일을 교체하고 `.meta`를 유지하여 스프라이트 GUID·슬라이스·피벗·PPU·애니메이션/프리팹 참조를 보존합니다.

| 구분 | PNG 경로 | 원본 크기 |
| --- | --- | --- |
| 캐릭터 기본 | `Assets/GameContent/Art/ProjectDM_Player_16Bit_v5.png` | 1536×1024 |
| 캐릭터 대기 | `Assets/GameContent/Art/ProjectDM_Player_Idle_4Frame_v1.png` | 1278×1230 |
| 캐릭터 보행 | `Assets/GameContent/Art/ProjectDM_Player_Walk_4Frame_v6.png` | 1278×1230 |
| 캐릭터 측면 보행 | `Assets/GameContent/Art/ProjectDM_Player_Walk_SideRefined_v7.png` | 1278×1230 |
| 슬라임·스켈레톤 | `Assets/GameContent/Art/ProjectDM_Monsters_16Bit_v5.png` | 1402×1122 |
| 고블린·버섯·멧돼지 | `Assets/GameContent/Art/ProjectDM_ExtraMonsters_16Bit_v1.png` | 1024×1536 |
| 초원 몬스터·보스 | `Assets/GameContent/Art/ProjectDM_MeadowMonsters_2Frame_v1.png` | 724×2172 |
| 던전 몬스터·보스 | `Assets/GameContent/Art/ProjectDM_DungeonMonsters_2Frame_v1.png` | 724×2172 |
| 공용 투사체·보조 리소스 | `Assets/GameContent/Art/ProjectDM_Sprites_TopDown_v2.png` | 1774×887 |
| 경험치·재화 5종 | `Assets/GameContent/Art/ProjectDM_ExperienceCurrency_5Types_4Frame_v1.png` | 1402×1122 |
| 경험치·금화·상자 | `Assets/GameContent/Art/ProjectDM_Pickups_4Frame_v1.png` | 1448×1086 |
| 마나 (앞선 확정본) | `Assets/GameContent/Art/ProjectDM_ManaPickup_v1.png` | 1254×1254 |
| 번개 | `Assets/GameContent/Art/Skills/ProjectDM_ChainLightning_v1.png` | 1536×1024 |
| 스플래쉬 | `Assets/GameContent/Art/Skills/ProjectDM_Splash_v1.png` | 1284×1225 |
| 운석 | `Assets/GameContent/Art/Skills/ProjectDM_Meteor_v1.png` | 1536×1024 |
| 빙결 (앞선 확정본) | `Assets/GameContent/Art/Skills/ProjectDM_FrostShard_v1.png` | 1254×1254 |

## 실제 스킬 표시 강도

이미지 수정과 함께 `SkillVisuals.asset`의 표시 알파를 번개 0.9→0.65, 스플래쉬 0.75→0.6, 운석 충돌 0.8→0.65, 운석 바닥 예고 0.12→0.09로 낮춥니다. 임시 화염 부채꼴 광선도 0.65→0.5로 낮춥니다. 범위·앵커·기간·피해·마나 비용은 같은 값을 사용합니다. 범위 이펙트 검증은 완전히 펼쳐졌을 때 설정한 은은한 알파가 유지되는지 검사합니다.

## 프롬프트와 검증 기록

선택한 편집 프롬프트 원문은 `SoftTone-prompts.json`, 원본 크기·메타데이터 SHA-256·적용 이미지 해시는 `SoftTone-assets.json`에 기록합니다. 마나·빙결의 앞선 프롬프트는 [FrostMana-assets.md](FrostMana-assets.md)에 있습니다. 편집은 모두 내장 도구를 사용합니다.

원본과 후보 PNG의 크기·알파 형태를 비교한 뒤 같은 경로에 복사합니다. 캔버스 크기가 달라진 후보는 적용하지 않고 원본 크기로 다시 편집합니다. 검증 원본/후보 보고서는 `Logs/ArtTone-20261020`에 있습니다.

`ProjectDM.Editor.ArtToneRegression.RunBatch`는 16개 PNG의 정확한 캔버스 크기, `.meta` 해시 불변, 투명/Point 임포트와 실제 스프라이트 슬라이스를 검사합니다. 기존 스프라이트 정리/두 스테이지/스킬/마나 드랍/캐릭터/드랍 등급 검증도 함께 실행합니다. 실제 Main 플레이는 `PrototypePlaySmoke.RunBatch`로, 스테이지별 실제 렌더는 `StageContentRegression.RenderBatch`로 확인합니다. 원본 씬을 저장하거나 실제 진행 데이터를 쓰지 않습니다.

적용 결과: 16개 중 이번 수정 14개, 앞선 확정본 유지 2개. PNG 16개의 원본 크기와 `.meta` SHA-256이 모두 일치합니다. 전체 아트 240개·스프라이트 참조 159개·스테이지 181개·스킬 153개·마나 드랍 17개·캐릭터 3,531개·드랍 등급 371개 검증을 통과했습니다. 초원/던전의 실제 SpriteRenderer 렌더를 `Logs/Stage1-render.png`, `Logs/Stage2-render.png`로 확인했습니다.

최종 Main 플레이 스모크도 통과했습니다. 두 스테이지/네 캐릭터, 실제 Addressables·프리팹 로딩과 캐릭터 마법, 빙결 공전, 마나 수집·고갈·재시작을 확인했습니다. `Logs/FrostMana-ingame.png`는 실제 Main 월드 카메라의 수정 후 렌더입니다. HUD/수동 마우스 조작까지 포함한 플레이 실측은 아닙니다.
