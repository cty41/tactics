# Amazon Output Spec

> **Deprecated:** This specification belongs to the retired human/pixel Amazon exploration and must not guide current Pure Run production. See [`DEPRECATED.md`](DEPRECATED.md).

## Summary

This spec targets a `60fps` 2D isometric tilemap game, while keeping character animation authored as low-frame sprite animation.

Recommended rule:

- game runtime: `60fps`
- sprite animation source: low-frame original drawings
- motion smoothness: controlled by playback cadence, not by generating 60 unique hand-drawn frames

This character is treated as a `small isometric battle unit`, not a showcase-size hero illustration.

## Master Output Format

- Output unit: `single-frame PNG`
- Color: `RGBA`
- Background: `transparent`
- Master canvas: `256 x 256`
- Framing: `full body visible`, `centered`, `same scale across all frames`
- Camera: `2D isometric 45-degree angle`
- Character orientation set:
  - `down-right`
  - `down-left`
  - `up-left`
  - `up-right`

## Framing And Safety Rules

- Lock one shared foot baseline for every frame of the same direction and action
- Keep the main body mass stable inside the middle of the canvas
- Recommended visual safe area:
  - left/right padding: `16-24 px`
  - top padding: `16-24 px`
  - bottom baseline margin: `24-32 px`
- Weapon tips, shield edge, and ponytail must remain inside the canvas safe area
- Do not crop the spear, shield, head, or feet

## Character Readability Target

Recommended visual size target on the `256 x 256` master canvas:

- body height without weapon emphasis: about `72-96 px`
- full readable height including hair and equipment silhouette: about `96-128 px`
- foot contact width on the ground plane: about `28-40 px`

The goal is not maximum detail. The goal is stable silhouette readability after later downscaling.

## Action Frame Counts

Default authored frame counts:

- `idle`: `8` frames for the down-right stability-test sequence
- `walk`: `8` frames
- `attack`: `6` frames
- `hurt`: `4-5` frames
- `death`: `8-10` frames

Do not increase frame counts just because the game runs at `60fps`.

## Recommended Playback Cadence

Recommended in-game playback targets:

- `idle`: play authored `8` frames at `8fps`
  - practical cadence in a `60fps` game: `7.5` ticks per frame; schedule frame changes from elapsed time, not integer tick counting
- `walk`: play authored `8` frames at `12fps`
  - practical cadence in a `60fps` game: `5` ticks per frame
- `attack`: play authored `6` frames at `12fps`
  - practical cadence in a `60fps` game: `5` ticks per frame
- `hurt`: `12-15fps`
- `death`: `10-12fps`

If a motion feels off, adjust playback cadence first. Do not immediately add more original frames.

## Direction Policy

This amazon uses asymmetric equipment:

- one long spear
- one round shield

Because of that, the default policy is:

- author all `4` isometric directions natively
- do not rely on mirror-flipping as the main production path

Mirroring can invert weapon-hand and shield-hand logic, and can also break body weight readability.

## Naming Convention

Recommended folder structure:

```text
Tools/artworks/amazon/
  imgs/
    idle/
      dr/
      dl/
      ul/
      ur/
    walk/
      dr/
      dl/
      ul/
      ur/
    attack/
      dr/
      dl/
      ul/
      ur/
```

Recommended file naming:

- `idle_f01.png`
- `idle_f02.png`
- `walk_f01.png`
- `attack_f03.png`

If a full relative example is needed:

- `imgs/idle/dr/idle_f01.png`
- `imgs/walk/ul/walk_f04.png`
- `imgs/attack/dl/attack_f02.png`

## Prompt-Level Enforcement

Every generation prompt for this character should reinforce:

- `single animation frame sprite`
- `fixed canvas and framing`
- `same scale across all frames`
- `same camera angle`
- `same baseline`
- `one spear only`
- `one shield only`
- `production-ready game sprite`
- `not a polished pixel illustration`
- `coarse low-resolution pixel clusters`

For `idle`, only allow:

- slight breathing
- tiny weight shift
- minimal hair motion
- minimal weapon angle drift

Do not allow idle to drift into:

- attack anticipation
- throw anticipation
- run lean
- showcase posing

## Down-Right Idle Stability-Test Sheet

For the current `amazon / idle / dr` test asset, generate one square `3 x 3` sequence sheet before producing individual PNG frames.

- Cells `1-8`, read left-to-right then top-to-bottom, are `idle_f01` through `idle_f08`.
- Cell `9` must be completely empty: only the chroma-key background, no sprite fragment, no label, no decoration.
- The sheet is a temporary production source and must live under `imgs/idle/dr/source/`, outside the preview-frame directory.
- Remove the connected chroma-key background once on the full sheet, then split the first eight equal cells.
- Resize every split cell to `256 x 256` with nearest-neighbor sampling.
- Never trim, recenter, scale, or clean backgrounds independently per frame.

### Locked Anchors

- Final canvas: `256 x 256 RGBA`.
- Foot baseline: `y = 232` in every frame.
- Canvas center: `x = 128`.
- The spear shaft, shield outer rim, feet, and pelvis stay in the same screen-space position.
- Head, upper torso, and only the ponytail tip may change.
- No part may cross the 16px side or top safe margin, or the 24px bottom baseline margin.

### Eight-Frame Breathing Loop

| Frame | Motion state |
| --- | --- |
| `f01` | neutral |
| `f02` | upper torso and head rise by 1px |
| `f03` | upper torso and head rise by 2px |
| `f04` | upper torso and head rise by 1px |
| `f05` | neutral |
| `f06` | upper torso and head lower by 1px |
| `f07` | upper torso and head lower by 2px |
| `f08` | upper torso and head lower by 1px, then loop to `f01` |
