# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color
- no scabbard anywhere

## Reference responsibilities
- identity_anchor: Tools/artworks/doge/calibrated/doge_capsule_demonbound_idle_dr_v01.png @ 21559ca7aedd3e146feb2b93c5a6633affc6ce544122628c960972a982b0b5e7
- pose_guide: Tools/artworks/doge/demonbound/cast_down_right_body_layer_pose_guide_v3.png @ faa70198adccd0ccb1363d275120c053bcb9426fb4fa292033f5e5ea70e0d40d

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
      126,
      132,
      165
    ],
    "exitWindow": [
      123,
      126,
      132,
      166
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
      123,
      112,
      132,
      126
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Transparent-background 2D game sprite, 256x256 master, clear down-right three-quarter view. Use the supplied formal Demonbound Idle DR as the sole identity and exact core-height anchor.

Create only the no-sword BODY LAYER for Cast. Match the Idle DR core capsule height exactly: the collar-to-foot body section must not be longer or taller than Idle. Keep the head, ears, and four paws compact; shorten the torso rather than stretching the body. Maintain a straight equal-width capsule, two front paws directly attached at a tight empty chest-centre grip, and two rear paws directly attached at the normal baseline. No arms, no legs, and no connecting limb segments.

Keep the same flat irregular gray-white forehead blaze, single alternate-colour ear and half-body alternate coat as Idle; no diamonds, crests, ornaments or symbols. Keep the tighter asymmetric DR eyes and serious focus. Absolutely no sword, scabbard, metal, glow, magic, particles, text, ground or shadow; uniform #00ff00 chroma background only.
