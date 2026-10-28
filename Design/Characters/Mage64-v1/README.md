# 기본 마법사 64×64 시안

내장 image_gen으로 기존 기본 마법사 스프라이트 시트를 참조하여 한 컷을 생성했다. 게임에는 적용하지 않았다.

- mage-generated.png: 생성 원본
- mage-64.png: 실제 64×64 RGBA PNG, 생성 원본을 nearest-neighbor로 크기 변환한 시안. 수작업 픽셀 정렬/팔레트 정리는 하지 않았다.
- comparison.png: 기존 32×32와 신규 64×64를 같은 화면 크기로 확대한 비교
- 참조: Assets/GameContent/Art/Characters/ProjectDM_Missile_Hooded_Walk_v1.png

## 생성 프롬프트

Use case: stylized-concept. Asset: ONE full-body 64x64 logical-pixel gameplay character sprite, NOT a portrait, NOT a sprite sheet. Reference image is the existing game's purple hooded default mage sprite sheet; preserve its exact recognizable design, chibi body proportions and front-facing idle stance from the first frame. ONE centered standing character: large purple hood with warm gold trim, short lavender hair, calm neutral confident face with simple dark eyes, navy tunic, purple gold-edged short cape, brown boots, wooden staff in the hand on viewer's left topped by a small turquoise orb. Whole body and staff fit inside the square with 3 logical pixels clear margin on every side. Camera matches the front-facing slightly top-down RPG reference. Redraw at a STRICT 64x64 logical pixel grid, rendered as clean nearest-neighbor enlarged pixels; deliberate chunky single-pixel clusters, tiny selective highlights and varied restrained dark purple outlines. Use the extra resolution over the original 32px to clarify hood folds, face and boots, while keeping the original silhouette and simple readable forms. Approximately 24-32 colors. No smooth anti-aliasing, no painterly brushwork, no illustration-level fine detail, no tiny noisy texture. Hand-placed irregular pixel clusters, asymmetrical cloth folds, directional shading, turquoise light confined to orb with only 2 tiny uneven flecks. Transparent background, no floor, no cast shadow, no labels, no frame, no other objects, no extra poses. Output one sprite occupying a 64 by 64 logical-pixel square; keep pixel scale uniform everywhere.
