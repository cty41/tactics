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
- pose_guide: Tools/artworks/doge/demonbound/cast_down_right_raised_sword_pose_guide_v6.png @ a07f94dfe518c907e673086c5fda6154b5abdb82a0e45dd0542751eb5fe9d0eb

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
      123,
      105,
      132,
      165
    ],
    "bladeLengthRangePx": [
      55,
      65
    ],
    "exitWindow": [
      123,
      105,
      132,
      166
    ],
    "forbiddenGemRegions": [
      [
        120,
        104,
        135,
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
      112,
      163,
      144,
      184
    ],
    "hiddenGrip": [
      127,
      177
    ],
    "maxBladeWidthPx": 9,
    "maxGemAreaPx": 36,
    "tipRegion": [
      120,
      104,
      135,
      116
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Transparent-background 2D game character sprite, 256x256 master composition, down-right three-quarter view. Use the supplied formal Idle DR as the exact identity AND compact sword design anchor. Use the supplied pose guide only for the vertical cast layout.

Depict exactly the same Demonbound Doberman as Idle DR: clear down-right three-quarter asymmetry, rigid equal-width capsule body, dark coat with deliberate half-body alternate coat colour, exactly one alternate-colour ear, and small flat gray-white blaze between the ears. No human hair. Exactly four small attached paws; no arms or legs.

Serious focused cast: head slightly raised; both pupils intentionally converge inward and upward at the sword tip. Two attached front paws clasp the sword grip at chest centre, one per side.

The sword must be the SAME compact, narrow, ornate ancestral one-handed sword from Idle DR, merely rotated upright. Do not lengthen, thicken, scale up, or redesign it. From guard to tip it is short and compact—about the same screen length as Idle DR—and the plain metal tip rises only slightly above the ears, never into the upper third of the canvas. Guard remains below mouth and above collar; blade covers only a narrow central facial strip and never covers either eye.

Exactly ONE tiny red gemstone sits only at the center of the guard. There are no gems, ornaments, pale fittings, or coloured dots on blade, tip, pommel, or grip end. No scabbard. No glow, light, energy, orb, beam, particles, aura, slash, crescent or other magic effect. Uniform #00ff00 chroma background only; no text, frame, ground or shadow.
