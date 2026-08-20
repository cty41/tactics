# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color
- no scabbard anywhere

## Reference responsibilities
- edit_target: Tools/artworks/doge/concepts/doge_capsule_demonbound_hit_ul_chroma_source_v01.png @ 43d82243275297cc71b853df915cd83731f545440991789856143e0531190042
- identity: Tools/artworks/doge/calibrated/doge_capsule_demonbound_idle_ul_v01.png @ ef9fd7521afafc4f46ef8fd355fc90337b493e268de61eaebfcd8d2ea3e529f4
- pose: godot/assets/units/actions/doge_hunter_hit_ul.png @ 0eb88380ebee80e99d1208b0adf5dca6b91ed368c94039ae298b9c4bb0c297af

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
# Demonbound Hit Up-Left v2

Edit the Hit UL v1 body while preserving its accepted identity, one visible eye, one tear streak, expression, colors and overall volume.

- Reverse the body axis: the core top center must be left of the foot center, matching the approved Amazon Hit UL projection.
- Fold both ears toward screen-right with impact inertia; do not leave them upright or tilt them with the body toward screen-left.
- Put both feet behind the body silhouette. The body must cover each foot's upper edge; show only compact grounded lower arcs below the body.
- Keep native up-left back-facing three-quarter anatomy, no front-face reconstruction, no second eye and no second tear.
- Keep the body frame swordless; the approved Demonbound sword will be assembled separately.
- Preserve the gray-black coat, white forehead blaze, orange inner ear and paws, red collar, short thick capsule body, black outline, no arms and no legs.
- Use a perfectly flat solid `#00ff00` background without shadow, gradient, texture, floor plane or reflection.
- Exclude weapons, fragments, floating paws, thin limbs, blood, stars, impact flash, magic, extra droplets, text and watermark.
