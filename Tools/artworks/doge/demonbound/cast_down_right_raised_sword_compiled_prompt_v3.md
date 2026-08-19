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
- pose_guide: Tools/artworks/doge/demonbound/cast_down_right_raised_sword_pose_guide_v3.png @ 7a21af087e46df4117ecb49b1cdc58ea92cdb1966a57f7b511ecc291fa2a2d48

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
      100,
      132,
      163
    ],
    "exitWindow": [
      123,
      104,
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
      120,
      76,
      135,
      101
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Transparent-background 2D game character sprite, 256x256 master composition, down-right three-quarter view. Use the supplied Idle DR only as the identity and equal-width core anchor, and the supplied pose guide only as the composition guide.

Depict the same compact capsule-bodied Demonbound Doberman: a rigid, straight, equal-width body; warm dark coat with a deliberate half-body alternate coat color; one alternate-color ear; a small flat gray-white blaze between the ears; canine face. No human hair. Exactly four small attached paws, with no arms and no legs.

This is a serious, focused cast pose. The head lifts slightly and both eyes look upward toward the sword tip. Two attached front paws hold the compact ancestral cursed one-handed sword at the chest centerline: one paw on each side of the grip, close to the body. The narrow blade is held almost perfectly vertical on the character centerline. The guard stays below the mouth and above the collar; it must not cover either eye. The blade can cover only a thin central strip of the face. The tip is above both ears with clear canvas margin. Preserve the established compact sword length, guard shape, and one small restrained red gemstone; do not enlarge the sword or gemstone.

No scabbard. No glow, no orb, no beam, no flame, no particles, no sparks, no magical aura, and no crescent slash. The sprite is only the unlit pose and sword on a clean #00ff00 chroma background; no text, no frame, no ground shadow.
