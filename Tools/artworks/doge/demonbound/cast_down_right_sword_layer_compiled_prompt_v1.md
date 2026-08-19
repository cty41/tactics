# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color
- no scabbard anywhere

## Reference responsibilities
- idle_sword_reference: Tools/artworks/doge/calibrated/doge_capsule_demonbound_idle_dr_v01.png @ 21559ca7aedd3e146feb2b93c5a6633affc6ce544122628c960972a982b0b5e7
- selected_body_composite_reference: Tools/artworks/pipeline/artifacts/job-d3e64d0255927458/job-d3e64d0255927458-a001/calibrated.png @ 47a0b5061b68bd9e5a3da0b93f1b83ca9f4c7a86c10372219a2b5ac806c22808

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
  "forbiddenRegions": [],
  "requiredHiddenLabels": [],
  "visibleAreaCaps": {},
  "weapon": {
    "bladeCenterline": [
      123,
      110,
      132,
      165
    ],
    "bladeLengthRangePx": [
      55,
      65
    ],
    "exitWindow": [
      123,
      110,
      132,
      194
    ],
    "guardWindow": [
      114,
      160,
      141,
      171
    ],
    "hiddenGrip": [
      127,
      185
    ],
    "maxBladeWidthPx": 9,
    "maxGemAreaPx": 20,
    "minBladeWidthPx": 7,
    "tipRegion": [
      123,
      108,
      132,
      117
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Transparent-background 2D game sprite layer, 256x256 master canvas with a uniform #00ff00 chroma background. Generate only one compact upright ancestral cursed sword; no character, paws, body, face, eyes, sword scabbard, glow, magic, particles, beam, ground, text or shadow.

Use the formal Demonbound Idle DR only as the exact sword design reference. Keep its compact narrow blade proportions: silver-gray blade and guard, no jewel or decoration at the tip, no oversized guard. The grip itself is brown-red wood. The pommel is silver-gray metal with one small embedded red gemstone. Preserve the same restrained noble heirloom finish, dark outline, line weight and material read as the Idle sword.

Place the sword vertically at the canvas centre for later compositing: narrow blade on x=127, tip around y=110, guard around y=165, grip/pommel below it around y=190. The blade is straight and compact, not a greatsword and not taller than the ears of the later character. No static visual effect of any kind.
