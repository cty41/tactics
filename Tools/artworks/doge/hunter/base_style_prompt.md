# 赤柴猎人母风格提示词

```text
Use case: stylized-concept
Asset type: production-ready Unity 2D single-frame character sprite
Primary request: preserve the approved Pure Run capsule-character style exactly
Style/medium: clean flat-color cartoon sprite, thick dark-brown outer contour, sparse controlled interior lines, subtle cel-shaded color blocks, crisp antialiased edges
Composition/framing: isolated full body on a 256x256 canvas, body center x=128, feet baseline y=236, generous transparent safety margin
Lighting/mood: neutral readable game lighting, no dramatic rim light
Constraints: match the approved hunter mother image's capsule core, face, ears, paws, round shield, spear design, palette and line weight; one character only; body and carried equipment only
Avoid: pixel art, painterly rendering, glossy highlights, smooth 3D lighting, background, floor, cast shadow, VFX, projectile, motion trail, text, watermark, UI
```

生成阶段使用均匀纯 `#00ff00` 色幕，去幕后保存 RGBA；色幕不得出现在角色内部。
