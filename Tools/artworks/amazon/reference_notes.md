# Amazon Reference Notes

> **Deprecated:** This human/pixel Amazon exploration is audit-only. Current Pure Run Amazon references are the approved red-shiba hunter assets listed in [`DEPRECATED.md`](DEPRECATED.md).

## Input

- Character name: `amazon`
- Reference image: existing in-folder result review based on the provided Amazon warrior reference
- Short brief: a female amazon warrior holding one javelin and one round shield

## Keep

- `2-head-tall chibi proportion`
- `oversized head` taking roughly half of the full body height
- `very simple block-like face`
- `large dark eyes` with no visible nose or mouth
- `compact torso`
- `very short simplified limbs`
- `chunky oversized feet`
- `coarse low-resolution pixel density`
- `clear silhouette readability`
- `isometric RPG battle-unit feeling`

## Ignore

- background shadow shapes
- dark scene backdrop
- any floor grounding painted into the reference
- slight body tilt from the source pose
- presentation-style framing
- any implied environment lighting

## Avoid Drift

- drifting into a polished pixel illustration
- adding glossy gold highlights everywhere
- over-detailing hair, armor, or shield surface
- generating more than one weapon
- turning the idle pose into a throw, charge, or attack wind-up
- soft anti-aliased edges
- smooth rendering instead of blocky pixel clusters

## Production Goal

The reference should anchor the sprite's proportion, face simplification, pixel chunkiness, and readability. It should not be treated as a pixel-perfect copy target. The final prompt library should bias toward a real in-game isometric sprite, not a decorative splash-style character image.

## Runtime Context

- target runtime: `60fps` game
- target asset style: low-frame source animation for a small isometric battle unit
- target direction policy: `4` native isometric directions for asymmetric equipment
- target production behavior: preserve readability after later downscaling, not maximum static illustration detail
