# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color
- no scabbard anywhere

## Reference responsibilities
- action: Tools/artworks/doge/candidates/doge_capsule_demonbound_hit_dr_wip_v02.png @ 80c4c75b9b52dacc637fb579174a2316cb8e8d0226eb5a4ab4aa41ed27e70ba2
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
      6.0,
      11.0
    ],
    "top": [
      143,
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
  "requiredHiddenLabels": [
    "equipment"
  ],
  "visibleAreaCaps": {},
  "weapon": {
    "bladeCenterline": [
      96,
      165,
      123,
      226
    ],
    "exitWindow": [
      88,
      157,
      111,
      183
    ],
    "guardWindow": [
      91,
      158,
      112,
      179
    ],
    "hiddenGrip": [
      101,
      171
    ],
    "maxBladeWidthPx": 12,
    "maxGemAreaPx": 64,
    "tipRegion": [
      105,
      205,
      126,
      230
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
# Demonbound Hit Up-Left v1

Generate one swordless Demonbound Hit up-left body sprite.

- Identity source: approved Demonbound Idle UL. Preserve the native back-facing three-quarter Shiba anatomy, gray-and-black coat, white forehead blaze, orange inner ear and paws, red collar, short thick capsule core, and no arms or legs.
- Action source: user-selected Demonbound Hit DR v2. Transfer only the same rigid-body screen-right lean, folded-ear inertia, widened-eye reaction, tense mouth and tear motion.
- Visibility: show only the eye and one short blue-white tear streak that are anatomically visible from the up-left back-facing view. Do not reveal a second eye or redraw a front face.
- Equipment: omit the sword completely; it will be assembled from the approved Demonbound equipment component.
- Exclude: weapon fragments, scabbard, shield, spear, magic glow, blood, stars, impact flash, extra tear droplets, text, watermark, extra limbs, or floating paws.
- Background: perfectly flat solid `#00ff00`, without shadow, gradient, texture, floor plane, or reflection.
