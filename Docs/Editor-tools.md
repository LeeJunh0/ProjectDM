# 현재 Unity 에디터 도구

`Project DM` 메뉴에는 다음 기능만 남아 있습니다.

| 메뉴 | 용도 |
| --- | --- |
| Export All Gameplay Artwork To Native 32px | 기존 원본 슬라이스를 32×32 PNG로 다시 내보내고 게임 참조 연결 |
| Rebuild Open Floor Tilemap | 열려 있는 씬의 바닥 타일맵 다시 그리기 |
| 리소스 애니메이션 미리보기 | 리소스 검색·프레임 재생·속도·격자·방향 비교 |
| 성장트리 편집기 | 엑셀/CSV 기반 노드 생성·배치·부모 연결·해금 조건 편집 및 게임 저장 |

GameManager 설정용 인스펙터와 바닥 룰타일의 규칙 편집 인스펙터도 유지합니다.

2026-10-26에 일회성 검증, 예전 아트 임포트/재구성, 씬·프리팹·Addressables 초기 생성, 이펙트·UI 초기 설치 도구를 소스와 메타 파일까지 삭제했습니다. 이전에 에디터를 열 때 실행되던 자동 아트/스테이지 임포트도 제거했습니다. 삭제된 임포터의 경로 상수는 `GameplayArtPaths`로 옮겨 현재 도구의 의존성을 분리했습니다. 폐기한 코인 임포터 소스만 검사하던 `Tests/CoinSliceRegression.ps1`도 삭제했습니다.

게임 코드, 제작된 프리팹·씬·Addressables 설정·리소스·슬라이스·런타임 검증은 유지합니다. `Tests/`의 현재 게임 규칙 검사는 이 메뉴 정리의 대상이 아닙니다. 과거 제작/QA 문서에 나오는 삭제된 메뉴·배치 명령은 이력으로만 남아 있습니다.

[성장트리 편집기 사용법](Growth-tree-editor.md) · [애니메이션 미리보기 사용법](Art/Resource-animation-preview.md) · [32픽셀 출력](Art/Native32-art.md)
