# Run results generated assets

Generated with the built-in image generation tool (imagegen skill), October 1, 2026, after user approval of the full-screen preview.

## Files

- `Assets/Resources/ProjectDM/UI/ProjectDM_ResultPanel_v1.png`: main panel frame and navy interior.
- `Assets/Resources/ProjectDM/UI/ProjectDM_ResultButtonSecondary_v1.png`: dark secondary button background.
- `Assets/Resources/ProjectDM/UI/ProjectDM_ResultButtonPrimary_v1.png`: bronze primary button background.

Source PNG pixels and alpha are preserved. GameManager crops transparent margins via normalized UVs; no text or icons are baked into these assets. Currency and monster icons reuse the existing loaded gameplay sprites. Runtime labels and actual run counters are separate from the art.

Import: Texture2D, alpha transparency, no mipmaps, no NPOT rescaling, no compression, bilinear filtering, clamp wrap.

The result panel uses a 720 x 780 reference layout scaled uniformly from 1920 x 1080, matching the tall approved preview. A transparent dark dim leaves the frozen game visible. Zero-kill entries are subdued. Results do not award currency a second time; collection already saves it.

Navigation: result Return opens the full-screen growth graph with the completed run still paused. Play Start there begins a fresh configured-duration run. Escape/Tab returns from the graph to results without resuming the expired run. During a live run the graph's navigation button resumes that run. Restart clears only the run entities/counters/temporary bonuses, preserving permanent currency and growth.

## Final prompts

### Panel

Use case: ui-mockup. Asset type: production Unity game UI texture with genuine alpha transparency outside the shape. Image 1 is approved visual STYLE reference only, NOT an edit target. Match its simple dark navy fill, slim muted bronze-gold outline and tiny diagonal chamfered corners, restrained flat raster game UI. NO text, numbers, icons, ornaments, glow, drop shadow, stone floor or other UI elements. Draw exactly ONE standalone shape, front-on, axis aligned, symmetrically centered, nearly filling canvas with small equal transparent margins. Clean sharp edges and solid opaque interior. Create only the main rectangular results PANEL background. Portrait-ish rectangle width:height=720:780 matching the tall central panel in reference. Thin simple bronze-gold single-line border about 4px at reference scale; near-black navy interior with extremely subtle texture. No separators or buttons; those are runtime content.

### Secondary button

Use case: ui-mockup. Asset type: production Unity game UI texture with genuine alpha transparency outside the shape. Image 1 is approved visual STYLE reference only, NOT an edit target. Match its simple dark navy fill, slim muted bronze-gold outline and tiny diagonal chamfered corners, restrained flat raster game UI. NO text, numbers, icons, ornaments, glow, drop shadow, stone floor or other UI elements. Draw exactly ONE standalone shape, front-on, axis aligned, symmetrically centered, nearly filling canvas with small equal transparent margins. Clean sharp edges and solid opaque interior. Create only the bottom LEFT secondary BUTTON background from reference, without its label. Wide horizontal rectangle width:height=300:76, dark navy interior, thin muted bronze-gold outline, tiny chamfered corners. Canvas is landscape and shape fills most width.

### Primary button

Use case: ui-mockup. Asset type: production Unity game UI texture with genuine alpha transparency outside the shape. Image 1 is approved visual STYLE reference only, NOT an edit target. Match its simple dark navy fill, slim muted bronze-gold outline and tiny diagonal chamfered corners, restrained flat raster game UI. NO text, numbers, icons, ornaments, glow, drop shadow, stone floor or other UI elements. Draw exactly ONE standalone shape, front-on, axis aligned, symmetrically centered, nearly filling canvas with small equal transparent margins. Clean sharp edges and solid opaque interior. Create only the bottom RIGHT primary BUTTON background from reference, without its label. Wide horizontal rectangle width:height=300:76, muted warm dark bronze-brown interior, thin muted gold outline, tiny chamfered corners. Canvas is landscape and shape fills most width.
