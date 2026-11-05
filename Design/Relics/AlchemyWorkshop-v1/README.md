# 마법사의 귀환 작업실 · 낡은 연금 가마

원정 금화에 남은 마력을 가마에서 추출하고 유물 조각으로 응축한다. 금화는 녹아서 소모되고, 실패한 미확정 조각은 연기로 흩어진다. 가마와 이미 보관한 조각은 유지된다.

- 게임 진입: 런 결과창 → **귀환 작업실 · 연금 가마**.
- 선택: **금화 넣기 → 조각 꺼내기 / 더 응축하기**.
- 첫 동작에만 금화를 넣는다. 추가 응축은 가마 안의 마력만 사용한다.
- 1.4초 동작, 0.72초에 결과 공개. 숫자와 버튼이 먼저 결과를 드러내지 않는다.
- 성공: 가마 안에서 조각이 자라고 위로 떠오른다. 실패: 조각이 부서지고 희미한 연기가 올라오며 마력이 가라앉는다.

`index.html`은 이미지 내장 비교 화면이며 실제 저장을 사용하지 않는다. `comparison.gif`는 왼쪽 성공, 오른쪽 실패다. 게임과 같은 대기 레이어를 합성해 가마와 작업대가 연출 뒤에도 남는다.

`gold-amounts.gif`는 왼쪽부터 10 / 1,000 / 100,000금화 투입 비교다. 금액이 커질수록 표시 금화가 늘어나며, 표면에서 납작해지고 녹아 금빛 파문을 남긴다. 액체와 응축 광원도 금색이다. 표시량은 1~128닢으로 압축하며 실제 참가비를 바꾸지 않는다. 추가 응축에는 금화를 다시 넣지 않는다.

런타임 시트에는 금화를 굽지 않는다. Unity에서 전용 `forge-coin-v1.png`를 참가비에 맞게 그리며, 비교 화면은 같은 개수 계산·낙하·용해 시간을 사용한다.

[런타임 PNG](forge-effects.png) · [런타임 JSON](forge-effects.json)

pixelfx 소스는 `work/pixelfx/src/fx/relic-forge`, Unity 표시는 `RelicForgeMotion.cs`다. 아트 기준은 `Docs/Art/Resource-style-guide.md`를 사용하고 둥근 철제 가마, 닳은 나무 작업대, 짧고 불균일한 금빛·보라색 반짝임으로 맞췄다. round1 → round2 → final을 렌더해 비교했으며 효과 검사 5개를 통과했다. MIT 라이선스는 `work/pixelfx/LICENSE`와 런타임 폴더에 보존한다.

`work/pixelfx`에서 재생성한다.

```powershell
node tools/render.mjs relic-forge --sheet --gif --no-actor --scale 3 --out ../../Design/Relics/AlchemyWorkshop-v1/final
node tools/check.mjs relic-forge
node tools/relic-forge-export.mjs
node tools/relic-forge-preview.mjs
```

`gold-pour-large.gif`는 게임과 같은 상단 여백을 확보한 구도로 1,000금화를 촘촘하게 쏟는 모습이다. 금화는 좁은 줄기로 겹쳐 떨어지고 액면에서 잠깐 모였다가 녹는다.

최신 투입 금화는 imagegen으로 별도 생성했다. [리소스·생성 프롬프트](forge-coin-prompt-v1.md) · [확대 미리보기](forge-coin-preview-v1.png). 기존 비교 GIF의 동전은 절차적 도형으로, 전용 리소스 적용 모습은 실제 게임 캡처를 기준으로 한다.
