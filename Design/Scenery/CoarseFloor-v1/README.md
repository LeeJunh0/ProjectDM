# 석재 바닥 굵은 도트 시안

기존 ProjectDM_GroundRuleTiles_3x3_v1.png를 참고해 내장 image_gen으로 생성한 스타일 비교용 시안. 게임에는 적용하지 않았다. 실제 96×96 해상도나 타일 연결 정확성을 보증하는 배포용 아틀라스가 아니다.

파일: dungeon-coarse-preview.png

## 생성 프롬프트

Use case: style-transfer. Edit target: the attached existing game stone floor tile atlas. Produce ONE new version of this exact square 3x3 dungeon ground rule tile atlas, keeping the same top-down orthographic composition, nine equal tile regions, corner/edge arrangement, dark desaturated blue-purple stone and sparse muted green moss. Main change: much coarser hand-crafted pixel art to match a 32x32 chibi character, approximately 32 logical pixels per tile (96x96 logical pixels across the entire atlas) enlarged cleanly with nearest-neighbor appearance. Every visible pixel cluster must be chunky and intentional, stepped angular edges. Simplify the dense diagonal surface grain drastically: broad calm flat stone color areas with just 2-3 sparse broken irregular shade clusters per tile. No fine speckling, no dense dithering, no smooth gradients or painterly texture. Preserve the original large connected central stone floor and peripheral rounded irregular broken rocks, deep narrow cracks, restrained asymmetric tiny moss tufts. Do NOT replace the floor with a regular brick checkerboard. Avoid uniform black outlines and bright contrast. Stone faces use 3-4 restrained shade bands with low contrast; edges have selectively interrupted dark purple outlines and sparse soft directional highlights. Keep the center quiet and dark for gameplay readability, character remains the focal point when placed on it. Image filled edge to edge as a tile atlas; no text, no grid lines, no characters, no props, no frame, no drop shadow, no perspective. This is a visual style preview, preserve recognizable source layout.

## 최종 적용 상태 (2026-11-03)
사용자 요청으로 돌판 바닥 실험을 중단하고 첨부한 연속 암반 그림을 ProjectDM_DungeonInterior_Continuous_v1.png에 원본 그대로 복원했다. 내부 16칸 패턴 코드와 연결은 제거하고 기존 중앙 타일 한 장으로 연결했다. 외곽은 초기 굵은 도트·투명 배경 석재로 복원했다. 석재 던전/초원 진입 디버그 버튼은 유지했다. 중간 돌판 리소스는 RetiredRuntimePaving에 보존했다. Unity 216칸·크기·바닥 참조·디버그 진입 검사와 구조 회귀 검사 5,994개 통과. 실제 플레이 리뷰는 미실행.
