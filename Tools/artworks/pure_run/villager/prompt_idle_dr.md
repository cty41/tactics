# Villager idle_dr Prompt

Use case: stylized-concept
Asset type: one Pure Run 2D capsule NPC villager sprite (256x256 RGBA, capsule center x=128, foot baseline y=236, tile-anchored on 64x32 isometric tile)
Primary request: relaxed standing up, both front paws and both hind paws flat on the baseline, calm expression; one compact upright capsule-dog villager
Identity: ordinary villager dog with a round face, simple eyes and nose, plain farmer clothes (coarse cloth tunic, small scarf or suspenders); warm earth/linen palette; no weapon, no shield, no class features; do not copy any approved unit's breed, colors, or gear
Body rule: equal-width rigid capsule body with rounded top cap; NO arms and NO legs between paws and body; exactly two front paws and two hind paws directly overlapping the capsule edge by multiple pixels and alpha-connected (no floating paws, no thin connection lines)
Face rule: simple small eyes and muzzle; no realistic fur rendering
Pose (this image): casual idle standing pose, body upright
Composition/framing: isolated full body on 256x256 canvas, capsule center x=128, foot baseline y=236 (for death: body AABB centered, no baseline), safe transparent margins
Style/medium: clean flat-color cartoon sprite, thick near-black contour, sparse interior lines, crisp antialiased edges; NOT pixel art, no glossy highlights, no smooth 3D light, no gradient
Background: solid uniform pure #00FF00 chroma key, never used inside the character
Avoid: pixel art, painterly rendering, background scenery, floor, cast shadow, VFX, text, watermark, UI, weapon, cape
`
Generation uses a uniform pure #00FF00 backdrop; chroma key removal happens after ingest. Keep the villager free of that color.
`
