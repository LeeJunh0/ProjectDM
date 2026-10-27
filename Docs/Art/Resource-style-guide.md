# Project DM 리소스 스타일 기준

2026-10-27에 선택한 [스킬 아이콘 빛·입자 시안](../../Design/GrowthIcons/OrganicPreview/index.html)을 이후 새 리소스를 만들 때의 공통 기준으로 사용한다. 기존 리소스를 자동으로 다시 만들거나 이 기준만으로 원본을 덮어쓰지는 않는다. 새 리소스를 만들 때는 먼저 **실제 게임에서 사용 중인 해당 대상의 색·실루엣·특징**을 확인하고, 그 특징이 알아보이도록 별도 그림을 생성한다.

## 공통 원칙

- **유기적인 형태:** 대칭과 균일한 굵기보다는 방향에 맞는 비대칭 실루엣, 크기가 다른 조각, 손으로 놓은 듯한 불규칙한 픽셀 덩어리를 사용한다. 무작정 지저분하게 만들지 않고 작은 크기에서도 주요 형태가 읽혀야 한다.
- **의도적인 여백:** 주제 주변과 요소 사이에 빈 곳을 남긴다. 빈 부분을 보석·연기·빛무리·장식으로 채우지 않는다. 구멍과 실루엣의 빠진 부분도 표현의 일부다.
- **빛의 강약과 끝 처리:** 가장 밝은 지점은 제한적으로 두고 방향에 따라 밝기·색·굵기를 다르게 한다. 끝으로 갈수록 가늘어지거나 어두워지고, 끊기거나 작은 도트로 흩어지게 한다. 모든 외곽을 같은 밝기의 네온선으로 두르지 않는다.
- **입자:** 파티클을 없애지 않는다. 크기·개수·거리·밝기가 서로 다른 입자를 흐름을 따라 비대칭으로 배치하고, 입자가 없는 방향도 남긴다. 입자로 실루엣과 배경을 빽빽하게 채우지 않는다.
- **외곽선과 색:** 속성에 어울리는 어두운 색의 얇고 변화 있는 선을 사용한다. 필요하면 일부 선을 생략하거나 밝은 면과 어두운 면의 강도를 다르게 한다. 강한 흰색·빛은 핵심 부위에만 쓴다.
- **도트 표현:** 흐린 벡터 테두리나 일률적인 광원보다 정돈된 픽셀 계단과 색 덩어리를 우선한다. 생성물의 도트가 실제 사용 크기에서 뭉개지지 않는지 확인한다.

## 리소스별 적용

- **스킬·이펙트:** 각 스킬의 움직임 방향을 읽을 수 있게 불꽃·전기·마법 궤적의 중심과 끝을 다르게 표현한다. 스킬의 실제 색과 모양을 참고한다.
- **성장 노드:** 스킬 기본 그림과 오른쪽 위의 성장 속성 보조 그림을 구분 가능하게 유지한다. 축소 상태에서도 인식하고, 보조 그림과 주요 실루엣이 서로 가리지 않도록 한다.
- **환경·아이템·캐릭터:** 스킬의 강한 발광을 그대로 복사하지 않고, 재질에 맞는 방향성 있는 명암·불규칙한 형태·여백으로 같은 원칙을 적용한다. 스테이지 바닥이나 기존 캐릭터와 나란히 비교해 과하게 튀지 않게 한다.

## 실제 선택된 생성 프롬프트

아래는 마지막 비교에서 선택한 스킬 시트와 번개 구체를 생성할 때 사용한 원문이다. 새 작업에서는 대상·색·실루엣과 리소스 용도에 맞춰 변경한다. 이미지를 참고로 제공할 때는 원본과 기능이 식별 가능하도록 유지한다.

### 스킬 시트

> Edit the provided existing 12 pixel-art skill icon sheet in place, maintaining exact identity and 3x4 placement. Keep the reference image's transparent negative space EMPTY: do not introduce background shapes, cloudy masses, filled circles, or dark-colored plates behind any icon. Target is a subtle artistic polish, not more detail. For each icon: make the outer tips thin and uneven, fading through darker or desaturated colored pixels or splitting into tiny intermittent fragments; vary brightness by direction so bright accents are confined to a few focal spots, with other edges dimmer. Add only 2-4 very small scattered pixel particles per icon, irregularly spaced and unequal in size/brightness, leaving much of the surrounding transparency empty. Avoid symmetrical duplicated ornaments, equal thickness and evenly distributed highlights. Keep rough hand-placed pixel-clusters and the same game skill silhouettes/colors. Real transparent background, no labels, no grid, do not let neighboring icons touch.

스킬 시트의 입력은 `Design/GrowthIcons/NegativeSpacePreview/skill-negative-space.png`, 선택 결과는 `Design/GrowthIcons/OrganicPreview/skill-organic.png`이다. 시안의 정확한 2–4개 입자는 이 시트에 사용한 요청이며, 다른 리소스에서도 입자 개수를 고정한다는 뜻은 아니다.

### 번개 구체

> Redraw this dark purple lightning orb skill icon as deliberate hand-made pixel art. Keep the gold bolt and purple orb identifiable. Give the bolt one bright focal part, then taper it toward its tip and break it into a few dimmer gold pixels. An irregular purple arc emerges strongest near the orb and grows thinner, dimmer, and intermittently broken toward the end, leaving asymmetric transparent gaps. Restore a few small asymmetric purple and gold particle flecks with unequal spacing, sizes and intensities, mainly on one side; leave some sides completely empty. Vary light and dark around the rim; no uniform luminous ring, no same-brightness outline, no forced symmetry, no glow blob, no frame, no text. Crisp pixel-art clusters, real transparent background.

번개 구체는 앞선 비대칭 중간 생성물을 입력으로 했으며, 선택 결과는 `Design/GrowthIcons/OrganicPreview/orb-organic.png`이다. 선택 시트는 아이콘에 따라 실루엣과 빛이 여전히 균일하게 느껴지는 부분이 있으므로, 위 원칙을 기준으로 사용하되 생성 결과를 그대로 완벽한 정답으로 간주하지 않는다.
