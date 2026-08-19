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
- pose_guide: Tools/artworks/doge/demonbound/cast_down_right_raised_sword_pose_guide_v9.png @ 1034e5e90e8612da4a2732a95d52911ea9d9a298dc84d690d902992ff51544c4

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
    "metalFinish": "silver_gray",
    "scabbard": "absent",
    "staticEffects": "absent"
  },
  "eyeOcclusion": {
    "bladeOverlapsBothInnerEyes": true,
    "maxEyeCenterGapPx": 28
  },
  "footCenter": [
    127,
    236
  ],
  "forbiddenRegions": [
    {
      "name": "left_outer_eye",
      "rect": [
        100,
        126,
        119,
        148
      ]
    },
    {
      "name": "right_outer_eye",
      "rect": [
        136,
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
      106,
      134,
      165
    ],
    "bladeLengthRangePx": [
      55,
      65
    ],
    "exitWindow": [
      121,
      105,
      134,
      166
    ],
    "forbiddenGemRegions": [
      [
        119,
        105,
        136,
        118
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
      105,
      136,
      118
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Transparent-background 2D game sprite, 256x256 master, down-right three-quarter view. First reference is the exact formal Demonbound Idle DR identity and sword design anchor. Second reference sets only the upright Cast layout.

Preserve v4 body pose: compact equal-width capsule body, clear down-right asymmetry, dark coat with half-body alternate coat, one alternate-colour ear, small flat gray-white forehead blaze, four attached paws, no arms or legs. Serious concentrated face, pupils converge inward/upward toward the tip.

Two front paws hold the one-handed ancestral sword at chest centre. Keep v5's substantial blade thickness but v4's compact length: the plain silver-gray tip reaches approximately the ear-tip height, not far above it. The narrow vertical blade must partially cover the INNER EDGE of BOTH eyes; keep the eyes close together behind the blade, not wide apart on opposite sides. It must not cover the outer eye areas. Guard remains below mouth and above collar.

All sword METAL—blade, guard, and pommel—is the same cool silver-gray metal as Idle DR, never gold. Exactly one tiny restrained red gemstone only at guard centre. No pale ornament, gem, coloured fitting, or decoration on blade, tip, pommel, grip end, forehead, ears or body. No scabbard, glow, energy, orb, beam, fire, particles, aura, slash or crescent. Uniform #00ff00 chroma background only; no text, frame, ground or shadow.
