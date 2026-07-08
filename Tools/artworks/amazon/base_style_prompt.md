# Amazon Base Style Prompt

## Base Prompt

```text
Create a single production-ready 2D character sprite for a Unity game.

This must look like a real low-resolution game sprite, not a detailed pixel illustration.

Style contract:
strict low-resolution pixel art, coarse pixel density, very clean pixel clusters, no anti-aliasing, no soft shading, no painterly rendering, no glossy highlights, no smooth lighting, no decorative texture detail.
Use a limited clean color palette with hard shadow shapes.
Prioritize silhouette clarity, readability, and stable sprite production over decoration.

Reference usage contract:
Use the reference image only for:
- 2-head-tall chibi body proportion
- oversized head taking about half of the body height
- simple block-like face
- large solid dark eyes
- compact torso
- very short simplified limbs
- chunky oversized feet
- coarse low-resolution sprite feeling
- clear readable silhouette

Ignore:
- background shadows
- background shapes
- body tilt in the reference
- scene lighting
- presentation-style posing
- any non-target motion from the reference

Sprite asset requirements:
isolated character sprite only
single animation frame sprite
full body visible from head to toe
centered in frame
pure white or transparent background
no cast shadow
no floor
no environment
no UI
no grid lines
no text
no effects
fixed canvas and framing
same scale across all frames
same camera angle
same baseline

Quality constraints:
one character only
no duplicate limbs
no extra fingers
no anatomy distortion
no motion blur
no glow
no painterly details
no high-detail rendering
keep the result coarse, simple, sturdy, and game-readable
```

## Usage Notes

- Reuse this block unchanged across `idle`, `walk`, and `attack`.
- Put this block before the character and action-specific sections.
- If the model drifts toward polished illustration, strengthen `coarse pixel density` and `not a detailed pixel illustration`.
- For a `60fps` game, keep authored frame counts low and control smoothness with playback cadence instead of demanding many extra original frames.
