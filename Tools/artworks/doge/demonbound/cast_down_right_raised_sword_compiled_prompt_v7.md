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
- pose_guide: Tools/artworks/doge/demonbound/cast_down_right_raised_sword_pose_guide_v7.png @ fcbf488f48c10c42eb830372a180cc5f5e52ca668105342dd52c26e02cf7d945

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
      105,
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
        104,
        136,
        116
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
      104,
      136,
      116
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Transparent-background 2D game character sprite, 256x256 master composition, down-right three-quarter view. First reference is the exact formal Demonbound Idle DR identity and ancestral sword design anchor. Second reference is only the upright Cast pose layout.

Preserve the v4 body pose exactly: same compact rigid equal-width capsule body, same down-right three-quarter asymmetry, dark coat with half-body alternate coat, one alternate-colour ear, small flat gray-white forehead blaze, four attached paws, no arms or legs. Serious head-raised expression with pupils deliberately converging inward and upward at the sword tip.

Two front paws hold the compact one-handed ancestral sword at chest centre. Keep v4's compact guard-to-tip length and upright centreline position exactly; do NOT lengthen the sword. But make the BLADE visibly thicker and more substantial, matching the formal Idle DR and Melee DR sword blade width and ornate metallic weight. It must not read as a thin needle. Keep the blade narrow enough to cover only the face center strip and avoid both eyes; compact guard below mouth and above collar.

Exactly one tiny restrained red gemstone only at guard centre. No gems, pale ornaments, coloured fittings, or decorations on blade, tip, pommel, or grip end. No scabbard and no glow/light/energy/orb/beam/fire/particles/aura/slash/crescent/magic effect. Uniform #00ff00 chroma background only, no text/frame/ground/shadow.
