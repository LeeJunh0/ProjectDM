# Timer HUD generated assets

Generated using the built-in image generation tool (imagegen skill), October 1, 2026.

## Files

- `Assets/Resources/ProjectDM/UI/ProjectDM_TimerIcon_v1.png`: stopwatch icon.
- `Assets/Resources/ProjectDM/UI/ProjectDM_TimerTrack_v1.png`: bronze frame and dark empty track.
- `Assets/Resources/ProjectDM/UI/ProjectDM_TimerFill_v1.png`: gold fill texture.

The original generated PNGs and alpha are preserved. GameManager uses UV rectangles to omit transparent margins and isolated low-alpha artifacts. The track uses three horizontal slices to preserve its end caps. The fill is cropped by the remaining-time ratio; no slider handle or pointer interaction is enabled. At ten seconds or less the icon and fill receive a warm red tint.

Import settings: alpha transparency, no mipmaps, no texture compression, bilinear filter, clamp wrap, no NPOT rescaling.

## Final prompts

### Icon

Use case: stylized-concept. Asset type: production 2D game HUD timer icon, transparent PNG. Generate ONE simple stopwatch icon for a dark incremental dungeon game, matching restrained bronze/gold outlines and matte near-black UI. Straight-on flat 2D graphic: circular warm gold outline, tiny top button and short stem, two clear clock hands, four short tick marks. Clean bold silhouette legible at 28 pixels. Center the icon, occupy about 80 percent of a square canvas. Muted warm ochre gold, simple flat fill, minimal shading. Actual transparent alpha background including all empty parts of the clock face. No numerals, letters, text, watermark, drop shadow, ornaments, gems, wings, filigree, fantasy decorations, glow, lighting scene or perspective. Deliver a usable isolated game asset, not a screenshot or presentation.

### Track generation

Use case: stylized-concept. Asset type: production 2D game UI horizontal timer slider track, transparent PNG. Generate ONE plain long horizontal empty progress bar, aspect ratio approximately 12:1, centered on a landscape transparent canvas. Straight-on 2D, parallel horizontal edges, restrained simple rectangular shape with very slightly rounded corners, a thin matte bronze/gold border and opaque dark charcoal indigo empty interior. The border thickness is about 8 percent of the bar height. Image intended for a 360 x 24 pixel HUD in a dark incremental dungeon game. A single consistent subtle highlight across the top edge, otherwise flat colors. Completely empty bar, no filled colored portion. Transparent alpha outside the rectangle. No slider handle, knob, clock, text, numbers, segments, arrows, labels, plates, ornaments, gems, filigree, vignette, glow, shadows, perspective or surrounding mockup. Occupy most canvas width with a little transparent padding. Deliver the isolated track asset.

### Track cleanup (final edit)

Use case: background-extraction. Edit target: attached empty horizontal slider track asset. Preserve the exact simple thin bronze-gold border, chamfered corners and dark navy EMPTY interior of the bar, all in the same straight-on position and proportions. REMOVE ALL the brown/gold glow, fog, drop shadow and colored backdrop outside the bar. Every pixel outside the outer bar border must be genuinely fully transparent, alpha zero. No aura, no vignette or halo, no background of any kind. Keep the entire horizontal bar including both endpoints intact. No additional objects, no handle, no text, no filled gauge. Output a clean isolated production game UI asset with transparent alpha.

### Fill

Use case: stylized-concept. Asset type: production 2D game UI horizontal timer slider fill texture, transparent PNG. Generate ONE solid unsegmented long horizontal progress bar fill strip about 16:1 aspect ratio, centered on landscape transparent canvas, for the inside of a slim 360 x 18 pixel game HUD. Flat warm ochre gold with a very subtle lighter top highlight, same appearance all along the horizontal axis so it can be stretched or cropped as time decreases. Straight-on 2D graphic, square ends, perfectly parallel straight edges. No border or surrounding frame, no empty track, no handle, no knob, no icon, no text, no numbers, no separators, no gloss streaks or decoration, no glow, no shadow, no perspective. Genuine alpha transparency around the single filled rectangle. Occupy most canvas width. Deliver a standalone usable isolated texture.
