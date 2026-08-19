# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- no arms or legs
- four attached paws
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color
- no scabbard anywhere

## Reference responsibilities
- identity_anchor: Tools/artworks/doge/calibrated/doge_capsule_demonbound_idle_dr_v01.png @ 21559ca7aedd3e146feb2b93c5a6633affc6ce544122628c960972a982b0b5e7
- pose_guide: Tools/artworks/doge/demonbound/cast_down_right_raised_sword_pose_guide_v8.png @ e69c817482dfe60afa7604b606a65d3909fc74501ebcc1cfeea63730a0b3a326

## Composition
```json
{
  "canvas": [
    256,
    256
  ],
  "coreAxis": {
    "bottom": [
      127,
      230
    ],
    "tiltDegrees": [
      -2.0,
      2.0
    ],
    "top": [
      127,
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
  "forbiddenRegions": [
    {
      "name": "left_eye",
      "rect": [
        100,
        126,
        120,
        148
      ]
    },
    {
      "name": "right_eye",
      "rect": [
        135,
        126,
        155,
        148
      ]
    }
  ],
  "requiredHiddenLabels": [],
  "visibleAreaCaps": {
    "equipment": 0.22
  },
  "weapon": {
    "bladeCenterline": [
      121,
      94,
      134,
      165
    ],
    "bladeLengthRangePx": [
      68,
      78
    ],
    "exitWindow": [
      121,
      94,
      134,
      166
    ],
    "forbiddenGemRegions": [
      [
        119,
        91,
        136,
        104
      ],
      [
        122,
        184,
        132,
        198
      ]
    ],
    "gemWindow": [
      122,
      168,
      132,
      178
    ],
    "guardWindow": [
      111,
      163,
      145,
      184
    ],
    "hiddenGrip": [
      127,
      177
    ],
    "maxBladeWidthPx": 13,
    "maxGemAreaPx": 36,
    "minBladeWidthPx": 10,
    "tipRegion": [
      119,
      91,
      136,
      104
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Transparent-background 2D game character sprite, 256x256 master composition, down-right three-quarter view. First reference is the exact formal Demonbound Idle DR identity and ancestral sword style anchor. Second reference is only the upright Cast pose layout.

Preserve the accepted v4 body pose and v5 blade thickness: compact rigid equal-width capsule body, down-right three-quarter asymmetry, dark coat with half-body alternate coat, one alternate-colour ear, small flat gray-white forehead blaze, four attached paws, no arms or legs. Serious head-raised expression with inward-and-upward focused pupils.

Two front paws hold the compact one-handed ancestral sword at chest centre. Keep v5's substantial blade thickness, but lengthen the blade to a compact MEDIUM length between the earlier v4 and v5: longer than v5, shorter than v3, with a tip modestly above the ears and safely below the upper third of the canvas. Do not turn it into a needle or a long pole. Keep compact guard below mouth and above collar, with blade only covering the narrow face centre strip and not either eye.

Exactly one tiny red gemstone only at guard centre. No gems, pale ornaments, coloured fittings, decorations, or extra shapes on blade, tip, pommel, grip end, forehead, ears, or body. Keep the guard and pommel compact rather than gold oversized redesigns. No scabbard and no glow/light/energy/orb/beam/fire/particles/aura/slash/crescent/magic effect. Uniform #00ff00 chroma background only; no text/frame/ground/shadow.
