# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color
- no scabbard anywhere

## Reference responsibilities
- hit_pose: godot/assets/units/actions/doge_hunter_hit_dr.png @ a921486bec40be843d02efdce8617e2a1b764c33e6a9e8e4278cf76c92fa9e01
- identity: Tools/artworks/doge/calibrated/doge_capsule_demonbound_idle_dr_v01.png @ 21559ca7aedd3e146feb2b93c5a6633affc6ce544122628c960972a982b0b5e7

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
  "forbiddenRegions": [
    {
      "label": "tear-cluster-left",
      "rect": [
        83,
        116,
        105,
        144
      ]
    },
    {
      "label": "tear-cluster-right",
      "rect": [
        144,
        111,
        166,
        141
      ]
    }
  ],
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
# Demonbound Hit Down-Right v1

Generate one Demonbound Hit down-right sprite.

- Identity source: approved Demonbound Idle DR. Preserve the gray-and-black Shiba identity, orange inner ear and paws, white forehead blaze, red collar, short thick capsule core, no arms or legs, and the character's single short sword.
- Pose source: approved Hunter Hit DR. Transfer only the medium chibi impact peak: the whole core leans screen-right as one rigid body, ears fold with inertia, both visible eyes widen with small pupils, exactly two short blue-white tear streaks, and a tense small wavy mouth.
- Equipment: keep the Demonbound sword compact at the screen-left side and moving with the body.
- Exclude: shield, spear, scabbard, magic glow, blood, stars, impact flash, text, watermark, extra droplets, extra limbs, or a second weapon.
- Background: perfectly flat solid `#00ff00`, without shadow, gradient, texture, floor plane, or reflection.
