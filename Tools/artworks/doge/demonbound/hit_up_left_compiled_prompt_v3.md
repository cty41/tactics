# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color
- no scabbard anywhere

## Reference responsibilities
- edit_target: Tools/artworks/doge/candidates/doge_capsule_demonbound_hit_ul_wip_v02.png @ 710e2a655ff8905ba3bdfc3d02289a02e49b726e9dd2e18c8ee244b70bebcfc1
- equipment: Tools/artworks/doge/calibrated/doge_capsule_demonbound_melee_ul_v01.png @ 4519d643cecce40130d92f00e4bbef9c3cd628f6b34953763a7cbbdda4d0e82b
- identity: Tools/artworks/doge/calibrated/doge_capsule_demonbound_idle_ul_v01.png @ ef9fd7521afafc4f46ef8fd355fc90337b493e268de61eaebfcd8d2ea3e529f4

## Composition
```json
{
  "bodyLayer": true,
  "canvas": [
    256,
    256
  ],
  "coreAxis": {
    "bottom": [
      127,
      236
    ],
    "tiltDegrees": [
      -11.0,
      -6.0
    ],
    "top": [
      111,
      121
    ]
  },
  "equipmentState": {
    "scabbard": "absent",
    "staticEffects": "absent"
  },
  "footCenter": [
    127,
    236
  ],
  "forbiddenRegions": [],
  "requiredHiddenLabels": [],
  "visibleAreaCaps": {},
  "weapon": {
    "bladeCenterline": [
      54,
      139,
      92,
      165
    ],
    "exitWindow": [
      50,
      132,
      101,
      178
    ],
    "guardWindow": [
      84,
      155,
      101,
      178
    ],
    "hiddenGrip": [
      91,
      169
    ],
    "maxBladeWidthPx": 9,
    "maxGemAreaPx": 24,
    "tipRegion": [
      45,
      128,
      66,
      151
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
# Demonbound Hit Up-Left v3

Edit the accepted Hit UL v2 candidate by adding only the approved ancestral sword to the character's anatomical right hand, which is the screen-left hand in this native up-left back-facing projection.

- Preserve every body pixel relationship from Hit UL v2: body recoil axis, both ears trailing screen-right, one visible eye, one tear, expression, coat, collar, hands and feet behind the body.
- Use approved Demonbound Melee UL only as the exact sword-design and right-hand equipment reference: the same compact narrow silver-gray blade, restrained guard, brown-red grip and single tiny red gem.
- The right hand remains attached to the screen-left body edge and grips the sword. The blade extends compactly toward screen-left and slightly upward, moving with the recoiling body.
- Do not add a second sword, scabbard, arm, leg, magic, impact flash or extra paw.
- Do not widen, lengthen or redesign the blade. Do not move or redraw the body, ears, face, collar, hands or feet.
- Keep one character on a perfectly flat solid `#00ff00` chroma-key background without shadow, gradient, texture, floor plane, reflection, text or watermark.
