# 투명 배경 적용

내장 image_gen으로 각 시안의 바깥 빈 공간을 투명화했다. 초원의 흙 테두리와 석재 내부 틈은 유지한다. 결과는 meadow-transparent.png, dungeon-transparent.png에 저장하고 게임 원본 경로에 연결했다. 원본 GUID와 스프라이트 ID를 유지했다. 석재도 타일 경계 4픽셀을 제외한 410픽셀 영역을 사용하고 기존 가장자리 커스텀 규칙의 출력만 새 GroundRule 타일로 연결했다.

## 초원

Use case: background-extraction. Edit this grass terrain tile atlas. Remove ONLY the empty brown background OUTSIDE the grass-and-soil terrain silhouette and make that exterior space truly transparent with alpha=0, including all exterior-connected small holes between edge grass and soil protrusions. Preserve the actual narrow brown earth rim attached to the grass; that rim is terrain, not background. Preserve every interior grass pixel, the nine tile arrangement, colors, chunky pixel-art edges, grass shapes, canvas size and framing. Do not redraw, shift, add or change the terrain. No new border, no shadow, no checkerboard painted into the image. Transparent exterior background.

## 석재

Use case: background-extraction. Edit this stone dungeon terrain tile atlas. Remove ONLY empty background OUTSIDE the perimeter rock-and-moss terrain silhouette and make all that exterior-connected negative space truly transparent alpha=0, including the small exterior-connected gaps around peripheral rocks. Keep the attached peripheral stones and moss intact. Preserve the dark cracks and central connected stone floor INSIDE the terrain; internal cracks are part of terrain and should not be erased. Preserve all actual stone and moss pixels, exact nine tile arrangement, chunky pixel edges, palette, canvas size and framing. Do not redraw, move, add or alter terrain shapes. No new border, no shadow, no checkerboard painted into the image. Transparent exterior background.
