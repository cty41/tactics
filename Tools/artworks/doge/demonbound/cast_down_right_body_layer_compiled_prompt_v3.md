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
- pose_guide: Tools/artworks/doge/demonbound/cast_down_right_body_layer_pose_guide_v2.png @ faa70198adccd0ccb1363d275120c053bcb9426fb4fa292033f5e5ea70e0d40d

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
Transparent-background 2D game sprite, 256x256 master, clear down-right three-quarter view. The supplied formal Demonbound Idle DR is the sole identity, coat, face, line-weight and compact capsule-body anchor.

Create only the BODY LAYER for the same serious focused Demonbound Cast pose. Keep the rigid straight equal-width capsule body, split alternate coat, one alternate-colour ear, and the same small flat irregular gray-white forehead blaze between the ears. The blaze is a flush patch of fur: never a diamond, crest, jewel, symbol, separate ornament, metal plate, or human hairstyle.

Keep exactly four small paws attached directly to the capsule body: two front paws make a compact empty chest-centre clamp around an invisible narrow vertical grip, and two rear paws sit at the baseline. There are no arms and no legs: no limb segment, connector, shoulder, elbow, wrist, thigh, or shin may appear between a paw and the body.

Make the down-right head projection unmistakable. Tighten the eyes: the far eye is smaller, closer to the central facial plane and partly set behind the muzzle projection; the near eye is larger. Do not use equal-sized, widely separated horizontal eyes. The head lifts slightly with a serious concentrated expression.

No sword, blade, guard, hilt, scabbard, jewel, metal, staff, weapon, glow, magic effect, aura, particles, beam, text, frame, ground, or shadow. Use only a uniform #00ff00 chroma background.
