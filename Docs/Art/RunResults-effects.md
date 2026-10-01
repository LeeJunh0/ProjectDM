# Run-result effects

Created October 1, 2026. Orange glow generated with the built-in image generation tool (imagegen skill).

## Asset

`Assets/Resources/ProjectDM/UI/ProjectDM_ResultGlowOrange_v1.png`

The original PNG and alpha are preserved. Texture2D, alpha transparency, no mipmaps, no compression, clamp wrap, bilinear filtering, no NPOT rescaling. Drawn in two soft 860 x 170 reference rectangles centered on the panel's top and bottom edges, beneath all runtime text and buttons. Default opacity .16, .35-second fade-in, subtle 3-second brightness breathing. No gameplay post-processing or changes to icon/text colors.

## Count-up

Currency, total kills and five monster counts use a display-only cubic ease-out from zero. Default duration 1.1 seconds per number, successive starts .07 seconds apart. Values stay nonnegative, monotonically increase and end at the exact statistics. EndRun captures an unscaled-time timestamp, so pausing the game does not freeze the effects. Displaying results again via the graph does not award rewards again or reset the animation. Each new completed run starts a fresh presentation.

The currency group reserves the final amount's text width to avoid layout jitter during the animation. GameManager Inspector exposes Korean labels for duration, stagger, glow strength and pulse duration. Zero duration displays numbers immediately; zero glow strength hides glow; zero pulse duration fixes the brightness.

Regression checks: `Tests/RunResultCountUpRegression.ps1` and `Tests/CoinSliceRegression.ps1`.

## Final generation prompt

Use case: stylized-concept. Asset type: production transparent 2D game UI glow overlay texture, not a mockup. Generate exactly ONE diffuse horizontal amber-orange light gradient on a genuinely transparent canvas. A long softly glowing flattened oval cloud, orange warmth concentrated smoothly at its center, smoothly fading in opacity to fully transparent toward all four edges. Broad soft even gradient only, not a sharp beam: width approximately 85% of a landscape canvas, height approximately 45%, centered. Warm orange #F28A34 at the core and darker orange falloff, NO white or yellow-white center, NO streak, lens flare, thin light line, sparks, particles, fire, smoke patterns, noise, border, panel, objects, text or background. Intended to be overlaid at low opacity at the top and bottom edges of a dark navy result panel. Very soft clean gaussian-like falloff, subtle calm light without decorative features. Smooth continuous alpha feathering, corners fully transparent. One usable standalone texture with alpha, not a glow painted on opaque black.
