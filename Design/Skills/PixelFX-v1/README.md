# Project DM PixelFX

기본 공격 4종과 고유 스킬 8종, 공통 명중·충돌·획득 반짝임을 픽셀 애니메이션으로 구성했다. `index.html`은 외부 이미지 요청 없이 초원·던전 배경에서 15종을 반복 재생한다. `beam-game.gif`, `cone-game.gif`는 자바스크립트 없이 보는 광선·부채꼴 미리보기다.

## 스타일

- 게임의 보라색 마력탄·광선·위성과 보라색 번개 구체를 유지한다. 연쇄·낙뢰는 기존 청백색, 화염은 짙은 적갈색에서 주황·노랑, 냉기는 청회색에서 옅은 청백색으로 이어진다.
- 선명한 중심과 어두운 가장자리, 흐름을 따라가는 짧은 밝은 맥동을 사용한다. 화염 부채꼴은 길게 늘어지는 줄기를 없애고, 불꽃 덩어리가 전방으로 퍼지며 식어 사라지도록 했다.
- 긴 균일한 네온 외곽이나 화면을 채우는 빛무리 대신 작은 파편·짧은 잔광을 사용한다. 초원과 던전의 실제 스크린샷 위에서 비교했다.
- 색·분포·시간을 검토한 round1~round2와 최종 재수정은 별도로 보관한다. 최종 장면 렌더는 `unity-meadow.png`, `unity-dungeon.png`다.

## 소스와 재생성

`work/pixelfx`는 설치된 pixelfx 스킬의 프로젝트 사본이다. 설치 원본은 수정하지 않았다. 프로젝트 전용 이펙트는 `src/fx/projectdm`, 생성기는 `tools/projectdm*.mjs`다. 아래 명령은 `work/pixelfx`에서 실행한다.

```powershell
node tools/projectdm.mjs 5
node tools/render.mjs projectdm --no-actor --sheet --out ../../Design/Skills/PixelFX-v1/final
node tools/check.mjs projectdm
node tools/projectdm-export.mjs
node tools/projectdm-preview.mjs
```

## Unity에서 직접 조절하기

상단 메뉴 **Project DM → 픽셀 이펙트 편집기**를 연다. 플레이 모드를 켜지 않아도 재생된다.

1. 효과를 선택하고 크기·움직임 속도·잔광 밝기·입자량·색상을 조절한다. 광선 굵기, 화염 퍼짐, 마력탄 잔광 길이, 빙결 폭풍의 큰 별 거리도 해당 효과에서 조절할 수 있다.
2. 자동 미리보기는 수치를 변경한 뒤 잠깐 기다리면 갱신된다. 초원·던전 배경, 확대, 일시정지, 프레임 확인으로 비교한다. 미리보기 배속은 게임 설정을 바꾸지 않는다.
3. **설정 저장**은 `tuning.json`만 저장한다. **게임에 적용**은 설정을 저장하고 전체 런타임 시트와 브라우저 미리보기를 재생성한다. 플레이를 중지한 상태에서 적용하고 다시 플레이하면 확인할 수 있다.
4. **선택 효과를 초기값으로**는 이번에 만든 기본 설정으로 돌아간다. 형태를 코드로 수정하려면 **효과 모양 코드 열기**를 누른다. `_shared.js` 저장도 자동 미리보기에 반영된다.

`tuning.json`과 `tuning-defaults.json`을 프로젝트와 함께 보관한다. 미리보기 작업 파일은 `Temp/PixelFXEditor`에 만들어진다. 프레임 밖으로 확대되는 경우 적용을 막고 안내한다. Node와 FFmpeg가 PATH에 필요하며 현재 작업 PC에서 검증했다. Node를 못 찾으면 창에서 실행 파일을 지정할 수 있다.

명령으로 같은 설정을 적용하려면 `work/pixelfx`에서 다음을 실행한다.

```powershell
node tools/projectdm-edit.mjs apply cone ../../Design/Skills/PixelFX-v1/tuning.json ../../Temp/PixelFXEditor/manual
```

마력탄은 각진 작은 마법 결정과 짧고 곧은 잔광으로 바꿨고, 유성 충돌 파편은 11개에서 15개로 늘렸다. 빙결 폭풍의 큰 별은 바깥으로 옮겼다. 나머지 승인된 11개 효과는 기본 설정에서 기존 픽셀 결과를 유지한다.

도구의 코어·셰이프 라이브러리는 pixelfx의 MIT 라이선스다. `work/pixelfx/LICENSE`와 런타임 폴더의 `LICENSE.txt`에 저작권과 허가문을 보존했다. 기존 라이브러리의 arcane-orb, fire-breath, snowflake-burst, chain-lightning을 렌더해 비교한 뒤 프로젝트 전용 형태를 작성했다.

## Unity 연결

런타임 리소스: `Assets/Resources/ProjectDM/Skills/PixelFX/skill-effects.png`와 `.json`. 15클립, 183프레임, 24fps, 1200×1600 단일 텍스처다. Point, 압축 없음, 밉맵 없음, 원래 크기를 유지한다.

`PixelFxLibrary`가 프레임을 공유하고 `PrototypeSkillVisuals`는 기존 SpriteQuadBatch로 애니메이션을 일괄 그린다. 마력탄과 공전 얼음은 실제 이동·회전 위치에 프레임만 교체한다. 명중 파편은 속성별 색을 입히고, 재화·마나·경험치 궤적과 노드 구매 반짝임에는 같은 흰색 픽셀 반짝임을 색칠해서 쓴다.

피해·공격 범위·충돌·마나·난수 확률·생성 개수·스킬 충전은 변경하지 않는다. 전역 히트스톱이나 화면 흔들림을 추가하지 않는다. 누락된 클립은 기존 리소스로 돌아갈 수 있다. 스킬 아이콘과 몬스터 원본 그림은 교체하지 않는다.
