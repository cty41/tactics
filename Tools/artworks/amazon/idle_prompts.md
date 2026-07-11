# Amazon Idle Prompts

## Single-Frame Template

```text
[Paste base_style_prompt.md]

[Paste character_prompt.md]

Animation contract:
idle combat stance for a 2D isometric battle sprite.
Facing down-right.
Body upright, planted, and ready for combat.
No attack anticipation, throw anticipation, running lean, or showcase posing.

Frame contract:
This is frame [X] of [N].

Pose details:
[head state]
[torso state]
[left arm state]
[right arm state]
[left leg state]
[right leg state]
[weapon state]
[shield state]
[hair state]

Final frame requirements:
one sprite only
no duplicate limbs, weapon, or shield
no cast shadow, floor, effects, UI, grid line, or text
```

## Down-Right Eight-Frame Sequence-Sheet Test Prompt

Use this prompt as one image-generation request. The current `idle.png` is a style reference only; preserve its chibi proportion, coarse pixel clusters, red tunic, blonde high ponytail, one long javelin, and one round shield.

```text
Use case: stylized-concept
Asset type: Unity 2D isometric sprite-animation production sheet
Input image: the provided amazon idle image is a style and character reference only, not a layout reference.

Create one square production source image containing a 3 by 3 grid of equal square sprite cells. There are no visible grid lines, borders, labels, arrows, text, or panel decorations. Keep the entire image on one perfectly flat solid #00ff00 chroma-key background, with no shadow, gradient, texture, floor, or reflection.

Cells are read left-to-right, then top-to-bottom. Cells 1 through 8 each contain exactly one full-body animation frame of the SAME amazon female warrior: down-right 2D isometric 45-degree view, 2-head-tall chibi proportion, large blocky dark eyes, blonde high ponytail, short red tunic with simple gold trim, one long javelin, and one round shield. Cell 9 is completely empty #00ff00 background.

Style: strict low-resolution pixel art, coarse sturdy pixel clusters, limited palette, hard shadow blocks, no anti-aliasing, no painterly rendering, no smooth lighting, no glossy highlights, not a polished pixel illustration.

Cell framing contract: each used cell is the same square camera canvas. Keep canvas center x=128 and foot baseline y=232 after normalizing each cell to 256 by 256. The feet, pelvis, spear shaft, shield outer rim, character scale, camera angle, and all equipment dimensions are visually locked across all eight cells. Do not crop any hair, spear, shield, head, or feet. Never allow any element to cross into another cell.

Animation contract: restrained planted combat breathing only. The legs stay planted. The shield and spear remain locked in screen position. Only the upper torso and head move, with at most a tiny ponytail-tip delay. Do not redraw the face, hair mass, body proportions, tunic shape, shield diameter, spear length, or weapon angle between cells.

Exact cell motion:
1 neutral; 2 torso and head up 1 pixel; 3 up 2 pixels; 4 up 1 pixel; 5 neutral; 6 down 1 pixel; 7 down 2 pixels; 8 down 1 pixel and ready to loop back to cell 1.

Avoid: white background, transparent checkerboard, background objects, cast shadows, extra weapons, duplicate limbs, attack poses, movement blur, cell borders, text, UI, different scales, different face designs, different equipment, per-cell camera changes, or decorative variations.
```

## Usage Notes

- Generate the sequence sheet once; do not issue eight independent frame prompts for this test.
- Clean the chroma key on the entire sheet, then split it into equal cells before nearest-neighbor normalization.
- Reject the whole sheet if any used cell changes character identity, equipment silhouette, scale, camera, baseline, or cell framing.
- The normal single-frame template remains valid for other actions and directions.
