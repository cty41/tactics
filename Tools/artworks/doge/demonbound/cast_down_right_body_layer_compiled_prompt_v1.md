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
- pose_guide: Tools/artworks/doge/demonbound/cast_down_right_body_layer_pose_guide_v1.png @ faa70198adccd0ccb1363d275120c053bcb9426fb4fa292033f5e5ea70e0d40d

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
Transparent-background 2D game sprite, 256x256 master, down-right three-quarter view. Use the supplied formal Demonbound Idle DR only as the exact identity and core-body anchor.

Create only the BODY LAYER for a serious focused Cast pose: the exact same compact Demonbound Doberman, rigid straight equal-width capsule body, same down-right three-quarter asymmetry, dark coat with deliberate half-body alternate coat, exactly one alternate-colour ear, and a small flat gray-white forehead blaze between ears. No human hair. Exactly four small paws attached directly to body; no arms or legs.

Head lifts slightly. Both eyes are close together at the face centre and pupils converge mildly inward/upward toward a future sword tip; do not spread eyes to the far sides. Two attached front paws form a tight, symmetric empty clamp at chest centre around an invisible narrow vertical grip, leaving only a small central gap for a later sword overlay. Feet remain stable at the normal baseline.

ABSOLUTELY NO sword, blade, guard, hilt, scabbard, jewel, metal, staff, weapon, glow, magic effect, aura, particles, beam, text, frame, ground, or shadow. Uniform #00ff00 chroma background only.
