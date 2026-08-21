# Deterministic ImageGen Task Packet
## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color
- no scabbard anywhere

## Reference responsibilities
- action_reference: Tools/artworks/pure_run/enemies/approved/tomb_maw_bat_wind_blade_attack_dr_v03.png @ 3d178d9af2a9a093c3792f0e22d760cb21c1e45669b50b81c2e5bd8381a2d9f2
- mother_anchor: Tools/artworks/pure_run/enemies/approved/tomb_maw_bat_ranged_color_ul_v01.png @ d8c0037bd029f6f59049d846a4698e0fed63d1d139fb6fde0642cc944c12facb
- pose_guide: Tools/artworks/pure_run/enemies/reviews/tomb_maw_bat_wind_blade_ul_pose_guide_v01.png @ 0ff1f88bd5e4c07e38696d0b1a990fe254084b86bd19ca69735221831ae32aab

## Composition
```json
{
  "canvas": [
    256,
    256
  ],
  "coreAxis": {
    "bottom": [
      128,
      174
    ],
    "tiltDegrees": [
      -8.0,
      8.0
    ],
    "top": [
      128,
      108
    ]
  },
  "equipmentState": {
    "scabbard": "absent",
    "staticEffects": "absent"
  },
  "footCenter": [
    128,
    236
  ],
  "forbiddenRegions": [],
  "weapon": {
    "exitWindow": [
      28,
      96,
      228,
      184
    ],
    "hiddenGrip": [
      128,
      142
    ],
    "tipRegion": [
      18,
      92,
      238,
      190
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Create one native up-left, rear three-quarter isometric action Sprite for the Tomb Maw Bat at the exact wind-blade release moment.

Input responsibilities:
- The approved up-left Idle Sprite is the only identity, rear-view anatomy, color, outline, spherical core size, core center, and hovering-height mother image.
- The approved down-right wind-blade action Sprite controls only the frozen action intensity: both wings perform the same near-horizontal power sweep and the spherical core counter-rotates only slightly.
- The pose guide controls only screen-space placement. It does not define identity, anatomy, color, or rendering style.

World and camera:
- The bat faces up-left in world space, viewed by the fixed isometric camera as a true rear three-quarter view.
- This is a strong wing-driven wind-blade release, not Idle flight, an upward flap, a falling pose, or a hit reaction.

Screen-space result:
- Preserve the approved UL rear head and ear relationship; show only facial features anatomically visible from that rear three-quarter angle.
- Keep the small central body as a near-round spherical core at the same size, x center, and hovering height as approved UL Idle. Do not scale or center by wingspan.
- Both wings sweep powerfully along one near-horizontal attack stage. They must read as the same action moment as the approved DR attack, reprojected natively into UL space rather than mirrored.
- Rebuild near/far wing overlap for the UL camera. Both wings remain attached with natural membrane continuity; no extra wing, detached wing, body penetration, or reversed depth.
- Keep the virtual ground point at (128,236), vertically aligned with the spherical core and Tile center.
- Keep all wing tips inside the 256x256 canvas with safe padding.

Rendering:
- Match the approved mother image's chunky chibi isometric game-Sprite style, dark outline, flat burgundy/purple palette, and restrained shading.
- Output one complete opaque character on a perfectly flat solid #00ff00 chroma-key background.
- The background must have no gradient, texture, floor, shadow, reflection, or lighting variation.

Do not draw a wind blade, airflow, speed line, projectile, ground shadow, blood, text, watermark, or additional effects.
