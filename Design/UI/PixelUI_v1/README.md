# Pixel UI 리소스 후보 v1

2026-10-27. 승인한 4화면 목업의 픽셀 프레임 스타일을 바탕으로 내장 imagegen에서 생성한 개별 PNG 원본입니다. `Assets/Resources/ProjectDM/UI/PixelUI/`에 복사하여 실제 게임 UI에 연결했습니다.

공통 스타일은 짙은 남보라 패널, 얇은 금속색 이중 픽셀 테두리, 계단형 모서리와 절제된 하이라이트입니다. 텍스트·아이콘·수치·상태 연출은 게임에서 별도로 얹는 것을 전제로 합니다. `index.html`에서 투명 배경과 개별 원본을 확인할 수 있습니다.

| 파일 | 용도 |
| --- | --- |
| panel_result.png | 결과창·공통 세로 패널 바탕 |
| panel_tooltip.png | 성장 정보·툴팁 가로 패널 바탕 |
| button_primary.png | 파란 주요 동작 버튼 |
| button_secondary.png | 남보라 보조 동작 버튼 |
| card_bronze.png | 동 등급 카드 바탕·구리색 테두리 |
| card_silver.png | 은 등급 카드 바탕·은색 테두리 |
| card_gold.png | 금 등급 카드 바탕·금색 테두리 |
| mana_frame.png | 액체를 별도로 그릴 수 있는 빈 마나 게이지 프레임 |
| growth_node.png | 속성색으로 틴트할 수 있는 중립 성장노드 바탕 |
| tab.png | 캐릭터·성장 종류 선택 탭 바탕 |
| currency_badge.png | 재화 표시 바탕 |
| divider.png | 패널·카드 구분선 |

PNG의 투명 영역을 보존했습니다. 원본 캔버스 크기와 여백은 생성 결과마다 다릅니다. 게임에서는 `PixelUiArt`의 UV 영역으로 여백을 제외하고 아홉 영역으로 나누어 모서리를 보존합니다. Point 필터·mipmap 없음·무압축으로 가져옵니다. 선택지 카드는 등급색의 테두리 빛이 3.8초 주기로 은은하게 변화하고, hover 시 조금 더 밝아집니다. 기존 마나 액체·피격·포션 연출과 글꼴은 그대로 사용합니다.

등급 카드 위의 빈 등급 탭을 제거하고 상단 테두리를 이어주었습니다. 수정 전 카드는 `originals/with-grade-tab/`에 보관했습니다.

`generation-manifest.json`에는 각 파일의 전체 프롬프트·생성 도구·참조 목업·원본 경로·이미지 정보와 수정 이력을 기록합니다.
