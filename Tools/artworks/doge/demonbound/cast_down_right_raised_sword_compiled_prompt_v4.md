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
- pose_guide: Tools/artworks/doge/demonbound/cast_down_right_raised_sword_pose_guide_v4.png @ 7a21af087e46df4117ecb49b1cdc58ea92cdb1966a57f7b511ecc291fa2a2d48

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
    "forbiddenGemRegions": [
      [
        120,
        76,
        135,
        101
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
Transparent-background 2D game character sprite, 256x256 master composition, down-right three-quarter view. Use the supplied Idle DR only as the exact identity and equal-width core anchor, and the supplied pose guide only as the composition guide.

Depict the same compact capsule-bodied Demonbound Doberman from the Idle DR anchor, not a new front-facing dog: retain its deliberate down-right asymmetry, warm dark coat, half-body alternate coat color, exactly one alternate-color ear, and small flat gray-white blaze between the ears. No human hair. Rigid straight equal-width capsule body; exactly four small attached paws; no arms or legs.

This is a serious, focused cast pose. The head lifts slightly. Keep the pupils in the normal Idle DR direction: do not make them look inward, cross-eyed, or stare upward. Convey focus only with a subtly lifted head and tightened serious upper eyelids.

Two attached front paws clasp a compact inherited cursed one-handed sword at the chest centerline, one paw on each side of the grip. The narrow blade is almost perfectly vertical on the centerline. The guard remains below the mouth and above the collar; it never covers either eye. The blade covers only a thin central facial stripe. The plain metal tip is above both ears with a clear canvas margin.

There is exactly one small restrained red gemstone and it is embedded only in the exact centre of the guard. No gemstone, jewel, ornament, red dot, or coloured fitting exists on the blade, sword tip, or pommel. Preserve the anchor's compact sword scale; do not make the guard, paws, or sword oversized.

No scabbard. No glow, light, energy, orb, beam, flare, flame, particles, sparks, aura, slash, crescent effect, or magical visual effect. Pose and unlit physical sword only on a uniform #00ff00 chroma background; no text, frame, ground, or shadow.
