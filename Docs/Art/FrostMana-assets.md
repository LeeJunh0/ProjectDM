# 빙결·마나 리소스 (2026-10-20)

내장 `image_gen`으로 투명 PNG 2개를 생성하고 프로젝트에 복사했습니다. 후속 요청으로 같은 도구에서 발광과 아웃라인 대비를 낮추는 이미지 편집을 적용했습니다. 원본 크기는 각각 1254×1254이며 실제 임포트는 최대 512 / Single Sprite / Full Rect / Point / 무압축 / 밉맵·읽기 비활성입니다. 원본의 알파 채널을 유지합니다.

| 리소스 | 저장 경로 | 연결 |
| --- | --- | --- |
| 빙결 공전 조각 | `Assets/GameContent/Art/Skills/ProjectDM_FrostShard_v1.png` | `Resources/ProjectDM/Skills/SkillVisuals.asset`의 `frostShard` 직접 참조, PPU 256, 여백 지름 비율 0.94 |
| 마나 방울 | `Assets/GameContent/Art/ProjectDM_ManaPickup_v1.png` | `GameContent/Prefabs/ManaPickup.prefab`의 `Visual`, PPU 512, 카탈로그 `manaPickupPrefab`, Addressables `prefab/pickup-mana` |

`Project DM/Install Frost and Mana Content` 메뉴로 임포트·참조·프리팹·카탈로그를 재설정할 수 있습니다. 씬은 열거나 저장하지 않습니다. 마나 크기는 GameManager 한글 인스펙터의 **마나 픽업 크기 배율**로 조절합니다. 부유 곡선은 경험치의 설정을 공유합니다.

마나의 기본 드랍 확률 8%, 회복량 +1 MP와 `extension` 영구 성장의 확률 보정을 유지합니다. 모든 실제 처치에서 독립 판정하며 마나가 가득 차 있어도 바닥에 떨어집니다. MP 회복과 숫자는 실제 수집 시에만 발생합니다. 최대값을 넘는 양은 버리고 실제 회복량만 누적합니다. 0 MP/종료된 런을 되살리지 않으며 재화·경험치·보스 경과 시간에 지급하지 않습니다.

후속 요청의 종류별 이펙트는 경험치·재화·상자·마나 총 12개 프리팹에 리소스 색을 따른 잔잔한 상승 입자로 적용했습니다. 설정과 검증은 [드랍템 상승 입자](Pickup-effects.md)에 기록합니다.

## 최종 생성 프롬프트

초기 생성 프롬프트입니다. 실제 적용 리소스는 아래 밝기·아웃라인 수정본입니다.

빙결:

```text
Use case: stylized-concept. Asset type: single transparent PNG sprite for a top-down 2D pixel-art fantasy action game in Unity. Primary request: one dedicated frost orbit spell ice shard. A compact symmetrical sharp ice crystal cluster, long spear point oriented straight up, angular side facets, icy cyan and pale blue with white specular core, darker cobalt edge for readability on grass. One centered isolated sprite occupying about 82 percent of the square canvas. Crisp chunky 16-bit pixel art, stepped edges and deliberate pixel clusters, limited palette, sharp silhouette readable at 48 pixels, no painterly blur. Tiny ice flecks close to the crystal only. Genuine transparent background, no ground, no cast shadow, no panel, no text, no watermark, no UI, no other items. The artwork will rotate around a player as a damaging shard, so center the visual mass and keep padding symmetrical.
```

마나:

```text
Use case: stylized-concept. Asset type: single transparent PNG mana pickup sprite for a top-down 2D 16-bit pixel-art fantasy action game in Unity. Primary request: one small floating magical mana droplet, distinct from a faceted experience gem and a gold coin. Teardrop silhouette with curled pointed tip, round lower body, deep royal-blue rim, saturated azure liquid energy and a pale cyan-white spiral core. A few tiny square glints close to silhouette. Crisp chunky pixel art with deliberate pixel clusters and stepped edges, readable at 40 pixels, restrained shading, strong silhouette. Centered isolated single item occupying about 80 percent of square canvas with symmetrical padding. Genuine transparent background, no bottle or container, no ground shadow, no scene, no panel, no UI, no lettering or watermark. This is the base pickup art; separate particle effects will be added later.
```

## 밝기 수정 (2026-10-20)

사용자 요청: "빛은 좀 더 약하게 하자 특히 마나방울". 내장 `image_gen` 편집으로 마나는 중심 소용돌이의 흰 광량과 주변 반짝임을 크게 줄이고, 빙결은 넓은 흰 면을 옅은 청색으로 낮춥니다. 경로·Unity GUID·투명 배경·크기·게임 동작을 유지하고 같은 PNG를 교체합니다. 마나의 불투명 픽셀 표본 평균 휘도는 114.12에서 72.22로 낮아졌고, RGB 모두 220 이상인 흰 픽셀 비율은 7.04%에서 0.20%로 줄었습니다.

빙결의 같은 표본 평균 휘도는 164.52에서 140.52로, 흰 픽셀 비율은 17.88%에서 5.06%로 낮아졌습니다. 두 편집본 모두 1254×1254와 투명 배경을 유지합니다. 위 수치는 알파 128 이상을 4픽셀 간격으로 읽은 밝기 표본이며 게임 후처리의 발광 강도를 직접 측정한 값은 아닙니다.

마나 최종 편집 프롬프트:

```text
Use case: lighting-weather. Edit target: the supplied transparent pixel-art mana droplet game sprite. Change only luminosity/glow. Make the mana droplet substantially less luminous, about 50 percent less perceived emitted light: replace the large nearly white spiral core with subdued medium cyan/teal blue, soften the cyan highlights, remove bloom and the luminous aura, greatly reduce the brightness of the few satellite glints. It should look like softly shaded blue magical liquid, with a small modest highlight and readable internal spiral, rather than a bright glowing light source. Preserve the exact curled teardrop silhouette, internal spiral shape, item size, position, transparent padding, square framing, crisp chunky pixel-art style, dark blue outline and existing alpha transparency. Do not add any objects, new particles, ground, text or background. Keep enough contrast to read on grass. Return the same single sprite on genuine transparent background.
```

빙결 최종 편집 프롬프트:

```text
Use case: lighting-weather. Edit target: the supplied transparent pixel-art frost shard game sprite. Change only luminosity/glow. Make this icy crystal modestly less luminous, about 25 percent less perceived emitted light. Replace broad pure white inner facets with soft pale blue and muted icy cyan, retaining a few small bright specular facets for readability. Reduce brightness of satellite ice flecks. No bloom or outward glowing aura. Preserve the exact tall pointed crystal cluster silhouette, facet layout, smaller side shards, original size and center, symmetrical transparent padding, square framing, crisp chunky pixel-art style and dark blue edging. Do not redesign or add/remove objects, no text, no ground, no background. Preserve alpha transparency and return one isolated transparent sprite.
```

## 아웃라인 수정 (2026-10-20)

사용자 요청: "아웃라인도 좀 더 연하게 하자". 내장 `image_gen` 편집으로 검게 보이는 남색 외곽선을 연하고 채도가 낮은 파란색으로 낮춥니다. 마나의 물방울·작은 방울, 빙결 본체·얼음 조각 모두에 같은 방향을 적용합니다. 이전 단계에서 낮춘 발광과 내부 음영은 유지하며 기존 PNG 경로에 적용합니다.

수정본 2개를 기존 경로에 교체했습니다. 각 1254×1254와 투명 배경, 기존 프리팹/설정의 스프라이트 GUID 연결을 확인했습니다. 아래 전체 전투 검증 결과는 리소스 최초 적용·밝기 수정 단계의 결과입니다.

마나 최종 편집 프롬프트:

```text
Use case: precise-object-edit. Edit target: the supplied subdued pixel-art mana droplet sprite. Change ONLY its outline colors. The current nearly black/navy hard outer contour is too dark and forceful. Recolor the dark perimeter of the droplet and its small satellite droplets to a softer, lighter, desaturated medium blue (approximately #426b9a), blending the rim gently into the adjacent blue shading. Keep a readable crisp pixel silhouette, but make the outline feel substantially less heavy and less contrasty. Preserve the current dim cyan internal spiral, already reduced glow, all interior shading and highlights, exact curled teardrop shape, pixel cluster style, object dimensions, positions, transparent padding and square framing. Do not brighten the body or increase glow. No bloom, no blur, no new particles, no added objects, no text, no background. Preserve genuine alpha transparency. Return the same single sprite with only softer/lighter outlines.
```

빙결 최종 편집 프롬프트:

```text
Use case: precise-object-edit. Edit target: the supplied subdued pixel-art frost crystal cluster sprite. Change ONLY its outline colors. Replace the harsh very dark navy perimeter around the main crystal and the floating ice fragments with softer, lighter, slightly desaturated icy medium blue (approximately #6294b1), integrating the contour into adjacent crystal facets. Keep crisp stepped pixel edges and a readable silhouette, but remove the heavy dark-stroke appearance. Preserve the current softened pale-blue facets, existing restrained highlights and reduced glow, exact tall spear-like ice cluster shape, facet layout, all fragments, object size and alignment, symmetric transparent padding and square framing. Do not brighten interior facets or add any glow. No bloom, no blur, no extra particles or objects, no text, no ground, no background. Preserve genuine alpha transparency. Return the same single sprite with only softer/lighter outlines.
```

## 검증

`ProjectDM.Editor.ProjectDMFrostManaSetup.RunBatch`는 설치 후 `ManaPickupRegression`, `SkillVisualRegression`, `ManaRegression`, `CharacterRegression`, `PrototypeRegression`을 실행합니다. 드랍과 수집의 분리, 분수·자석·정지·풀 재사용, 실제 회복량·만땅·MP 고갈·결과창·확률 0, 직접/효과 처치의 중복 보상 방지와 빙결 전용 참조/크기를 검사합니다.

`PrototypePlaySmoke`는 실제 Main 씬의 Addressables 로딩, 빙결 전용 이미지와 마나 수집을 검사합니다. 그래픽 장치가 있으면 `Logs/FrostMana-ingame.png`에 테스트 중 월드 카메라를 렌더합니다. HUD/마우스 조작까지 포함한 수동 플레이 검증은 아닙니다. 검증에는 메모리/실험 저장소를 사용합니다.

실행 결과: 마나 드랍 17개, 스킬 이미지 153개, 마나 런 50개, 캐릭터 3,531개, 기존 전투·성장 255개 검증 통과. 실제 Main 플레이 스모크도 통과했습니다. PowerShell 시스템 구성 6,483개·재화 45,888개·등급 2,587개 검증 통과. 격리된 비활성 오브젝트를 사용하는 기존 테스트의 Animator 경고는 기록되며, 게임 코드 예외/컴파일 오류는 없었습니다.
