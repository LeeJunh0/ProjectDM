# 후드 여성 마법사 리소스

확정한 [캐릭터 시안](Character-Hooded-Reference-v1.png)을 기준으로 네 캐릭터를 실제 플레이에 적용합니다. 내장 `image_gen`으로 투명 보행 시트를 생성했습니다. 사용한 프롬프트는 [Hooded-character-prompts.json](Hooded-character-prompts.json)에 기록했습니다.

| ID | 디자인 | PNG (`Assets/GameContent/Art/Characters/`) |
| --- | --- | --- |
| projectile | 보라 후드·금색 테두리·청록 마력구 | ProjectDM_Missile_Hooded_Walk_v1.png |
| flame | 붉은 후드·호박색 스카프·불꽃 망토 끝·불씨 지팡이 | ProjectDM_Flame_Hooded_Walk_v1.png |
| lightning | 남보라 후드·연보라 테두리·갈라진 망토·번개 지팡이 | ProjectDM_Lightning_Hooded_Walk_v1.png |
| frost | 파란 후드·밝은 털깃·긴 망토·얼음 결정 지팡이 | ProjectDM_Frost_Hooded_Walk_v1.png |

모두 여성 캐릭터이며 후드를 착용합니다. 무기를 포함한 전체 표현을 각진 도트로 맞추고, 외곽선과 빛을 약하게 유지합니다. 시안의 `RETRO 32`는 스타일 이름입니다. 실제 PNG는 확대된 도트 이미지이며 32×32 원본 스프라이트가 아닙니다.

각 시트는 정면·왼쪽·오른쪽·뒷면 순서의 4행, 보행 4프레임의 4열입니다. 도구 출력의 간격 오차 때문에 고정 격자로 자르지 않고, 알파 영역을 검출해 실제 프레임 경계를 나눕니다. 원본 PNG를 다시 그리거나 리샘플링하지 않습니다. Point·무압축·밉맵 없음으로 임포트합니다. 후드 중심과 발 기준으로 피벗을 맞추고, 대표 프레임 높이 2.8 유닛을 기준으로 PPU를 정합니다. 기존 플레이어 루트 배율 0.95는 유지합니다.

`Project DM/Import Hooded Character Artwork` 메뉴가 64개 스프라이트, 8fps 보행 클립, 캐릭터별 Animator Controller, `Assets/Resources/ProjectDM/Characters/CharacterVisuals.asset`을 구성합니다. 다시 실행해도 기존 스프라이트 ID와 클립·컨트롤러 GUID를 재사용합니다. 캐릭터별 리소스가 카탈로그에서 직접 참조되므로 빌드에도 포함됩니다.

성장 UI에서 선택한 캐릭터는 기존 규칙대로 **다음 런 시작 시** 외형과 고유 마법이 함께 적용됩니다. 대기 시에는 정면 첫 프레임과 기존 호흡 동작을 사용합니다. 보행은 네 방향 각각 4프레임을 재생합니다. 과거 플레이어 시트·슬라이스·Addressables 설정을 보존하며, 카탈로그가 없으면 기존 외형을 사용할 수 있습니다.

검증 진입점: `ProjectDM.Editor.ProjectDMCharacterArtSetup.RunBatch`. 투명 스프라이트 임포트, 프레임 경계·크기, 방향별 루프, 실제 Player 프리팹의 캐릭터별 교체·이동 방향·대기 복귀, 기존 고유 마법 회귀를 확인합니다. `PrototypePlaySmoke.RunBatch`는 실제 Main/Addressables 부팅에서 캐릭터 선택이 현재 외형을 즉시 바꾸지 않고 새 런에 적용되는지 확인합니다. 실제 SpriteRenderer 렌더는 `Logs/HoodedCharacters-dark.png`, `Logs/HoodedCharacters-light.png`에 저장합니다.

2026-10-22 적용 결과: 아트·애니메이션·프리팹 검증 230개, 기존 캐릭터 고유 마법 검증 3,548개, 실제 Main 플레이 스모크가 통과했습니다. 두 배경의 실제 렌더와 새 빙결 캐릭터가 표시되는 `Logs/FrostMana-ingame.png`를 확인했습니다. 수동 조작 플레이나 새 실행 파일 빌드 검증은 포함하지 않습니다.
