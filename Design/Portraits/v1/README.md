# 마법사 초상화 시안 v1

2026-10-28. 내장 image_gen으로 타입별 초상화를 각각 생성했다. 게임에 연결하기 전 외형·표정 검토용이며 기존 스프라이트는 변경하지 않았다. 투명 배경 PNG다.

| 파일 | 외형과 표정 |
| --- | --- |
| mage-basic.png | 기본: 보라 후드·금색 테두리·라벤더 머리·청록 보석 지팡이. 당당하고 웃지 않는 표정. |
| mage-flame.png | 화염: 붉은 후드·금색 테두리·적갈색 머리·주황 스카프. 열정적이고 자신감 있는 웃음. |
| mage-frost.png | 냉기: 푸른 후드·흰 테두리와 털깃·얼음색 머리. 차갑고 시크한 무표정. |
| mage-lightning.png | 번개: 남보라 후드·연보라 테두리·보라 머리. 두 눈을 감고 집중하는 표정. |

## 공통 생성 프롬프트

Use case: stylized-concept. Create ONE square game character portrait, head and shoulders only, face large and immediately readable as a small HUD portrait. Based on the attached sprite sheet as a character identity reference ONLY: draw ONE expressive detailed portrait, NOT a sprite sheet, NOT walking poses. Keep the youthful gender-ambiguous hooded mage identity, short hair, fair skin, rounded approachable fantasy facial proportions. Hand-crafted pixel art with clearly visible deliberate pixel clusters, stepped edges, limited palette, selective dithering and directional highlights. Approximate 128x128 logical pixel art enlarged cleanly, no smooth airbrush or vector rendering. Natural asymmetry, sparse tiny uneven elemental flecks, plenty of empty negative space, no uniform neon rim or busy decoration. Full hood crown and shoulders visible with a little margin, three-quarter view, eyes readable under the hood. Same framing and pixel scale across a set of four. Transparent background, no frame, no lettering, no symbols floating over the face.

## 타입별 추가 프롬프트

기본 (reference: Assets/GameContent/Art/Characters/ProjectDM_Missile_Hooded_Walk_v1.png):

Proud and assured but entirely neutral unsmiling expression: chin subtly raised, steady confident open eyes, relaxed straight mouth, no grin, no angry scowl. Purple hood with warm golden trim and round gold clasp, short lavender hair, dark navy inner clothing. A subtle turquoise staff gem can peek beside one shoulder. Keep gold trim simple.

화염 (reference: Assets/GameContent/Art/Characters/ProjectDM_Flame_Hooded_Walk_v1.png):

Passionate confident mage with a lively warm self-assured smile, slightly open smiling mouth and spirited eyebrows, not an evil grin or rage. Deep red hood with warm gold trim, short reddish auburn hair, orange scarf, navy inner clothing. A tiny warm ember accent beside shoulder; keep face clearly visible.

냉기 (reference: Assets/GameContent/Art/Characters/ProjectDM_Frost_Hooded_Walk_v1.png):

Cold chic reserved mage with expressionless face, narrow cool steady eyes, subtle aloof gaze and closed straight mouth, no smile, not aggressive or angry. Blue hood with white trim and soft white fur collar, short pale icy blue hair, round gold clasp, navy inner clothing. Sparse small ice flecks off one shoulder.

번개 (reference: Assets/GameContent/Art/Characters/ProjectDM_Lightning_Hooded_Walk_v1.png):

Mage concentrating deeply with BOTH EYES FULLY CLOSED, relaxed but focused eyebrows, peaceful composed straight mouth, head slightly lowered. Indigo violet hood with pale lavender trim, short violet hair, round gold clasp, navy inner clothing. A few restrained uneven violet sparks near one shoulder, no huge lightning bolts or glowing eyes.
