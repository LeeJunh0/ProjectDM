# 연금 가마 전용 금화 생성 기록

- 생성: 내장 imagegen, 실제 투명 배경. 원본은 `forge-coin-generated-v1.png`.
- 런타임: nearest-neighbor로 32×32 축소한 `Assets/Resources/ProjectDM/UI/RelicForge/forge-coin-v1.png`. Point·비압축·밉맵 없음.
- 적용: `RelicForgeMotion`의 투입 동전만 사용한다. HUD와 필드의 금화는 기존 리소스를 유지한다.

## 사용한 프롬프트

Create a production game sprite: exactly ONE isolated gold coin for a pixel-art alchemy cauldron animation. Transparent background, no ground, no drop shadow, no surrounding sparkles or particles. Front face seen slightly at a three-quarter angle, nearly circular slightly vertical oval, a small visible dark bronze thickness on lower right. Deliberate coarse hand-placed pixel art with roughly 20x24 logical pixels (large clearly stepped square pixel clusters, no antialiasing, no fine texture). Simple warm ochre gold face (#c48229), two small unequal pale yellow highlight clusters confined to upper left (#f2bd58 and very limited #fff0b2), dark muted brown thin broken outline (#5b3116). Coin center has a very simple shallow embossed tiny diamond, spare detail, large quiet flat color areas. Irregular worn rim, avoid a uniform bright ring, no glossy gradients, no 3D rendering, no ornate filigree, no lettering, no currency sign, no UI frame. Keep whole coin centered with transparent margin around it. This is used for many overlapping coins being poured into a coarse pixel-art dark iron kettle full of molten gold, so clear silhouette and restrained warm gold palette matter more than detail.
