# 유물 제련 연출 시안

이 문서는 초기 공중 마력석 시안의 아카이브다. 현재 게임은 [귀환 작업실 · 연금 가마](../AlchemyWorkshop-v1/README.md) 설정을 사용하며 재생성 명령과 소스는 최신 작업실 버전을 기준으로 한다.

게임에는 적용하지 않은 비교용 시안이다. `index.html`은 이미지가 내장된 단일 파일이며 성공·실패를 반복 재생하고 배속·일시정지·프레임 확인을 사용할 수 있다. 실제 금화와 저장은 읽거나 변경하지 않는다.

## 흐름

- 0~0.28초: 금화 세 조각이 핵으로 들어간다.
- 0.28~0.72초: 보라색 기운이 회전하며 모인다. 끝에서 돌이 작게 떨린다. 결과가 나올 때까지 성공·실패 동작은 같다.
- 성공: 짧은 섬광, 조각이 위로 튀어나오고 금색·보라색 반짝임이 서로 다른 속도로 흩어진다. 미확정 조각만 1개가 되고 기존 보유 조각은 유지한다.
- 실패: 핵이 갈라지고 어두운 파편이 아래로 떨어진다. 짧은 국소 흔들림 후 빛이 꺼진다. 기존 확보 조각은 유지한다.
- 약 1.4초에 결과가 정리된다. 시안은 결과를 잠깐 보여준 뒤 다시 재생한다.

## 리소스

성공: [PNG](final/relic-forge/success/success.png) · [JSON](final/relic-forge/success/success.json) · [GIF](final/relic-forge/success/anim.gif)

실패: [PNG](final/relic-forge/failure/failure.png) · [JSON](final/relic-forge/failure/failure.json) · [GIF](final/relic-forge/failure/anim.gif)

pixelfx 프로젝트 사본의 `src/fx/relic-forge`에 작성했다. 160×144, 24fps, 단발 35프레임이며 PNG는 투명 배경이다. 설치 원본과 런타임 스킬 시트는 변경하지 않았다. MIT 라이선스는 `work/pixelfx/LICENSE`에 보관한다.

`arcane-orb`의 회전 기운과 `ice-shatter`의 갈라지는 파편을 참고하고, round1 → round2 → final을 렌더해 비교했다. 효과 검사와 `Tests/RelicForgePreviewRegression.mjs`에서 로딩·재생·제어·성공/실패 구분을 확인한다.

재생성은 `work/pixelfx`에서 다음을 실행한다.

```powershell
node tools/render.mjs relic-forge --sheet --gif --no-actor --scale 3 --out ../../Design/Relics/ForgeMotion-v1/final
node tools/check.mjs relic-forge
node tools/relic-forge-preview.mjs
```
