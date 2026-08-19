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
- pose_guide: Tools/artworks/doge/demonbound/cast_down_right_raised_sword_pose_guide_v5.png @ 0f160dbc887d84bce8e596500519242b8e7e9f10c33591baa653e910da53e1c0

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
      88,
      132,
      163
    ],
    "exitWindow": [
      123,
      92,
      132,
      166
    ],
    "forbiddenGemRegions": [
      [
        120,
        60,
        135,
        90
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
      60,
      135,
      90
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Transparent-background 2D game character sprite, 256x256 master composition, down-right three-quarter view. Use the supplied Idle DR only as the exact identity and equal-width core anchor, and the supplied pose guide only as the composition guide.

Depict the exact same compact Demonbound Doberman as the Idle DR anchor, never a front-facing new dog: preserve clear down-right three-quarter asymmetry, the rigid straight equal-width capsule body, warm dark coat, deliberate half-body alternate coat colour, exactly one alternate-colour ear, and the small flat gray-white blaze between the ears. No human hair. Exactly four small paws attached directly to the body; no arms or legs.

This is a serious concentrated cast pose. The head lifts slightly. Both pupils visibly converge inward and upward toward the sword tip, with strong deliberate focus; this intentional cross-eyed convergence is required, not a flat sideways glance. Keep the expression severe and concentrated rather than cute or surprised.

Two attached front paws clasp the compact inherited one-handed sword at the chest centreline, one paw on each side of the grip. The blade is narrow and almost perfectly vertical on the centreline. Restore a long, prominent blade: from the guard it reaches high above both ears, with the plain metal tip near the upper safe zone. The guard remains below the mouth and above the collar, never overlapping either eye; blade covers only a thin central facial strip. Keep the guard compact, not oversized.

CRITICAL JEWEL RULE: exactly ONE tiny restrained red gemstone, embedded only at the exact centre of the guard. No gemstone, jewel, ornament, red dot, or coloured fitting exists on blade, sword tip, pommel, grip end, or anywhere else. No pale pommel ornament.

No scabbard. No glow, light, energy, orb, beam, flare, flame, particles, sparks, aura, slash, crescent effect, or magical visual effect. Unlit physical sword and pose only on a uniform #00ff00 chroma background; no text, frame, ground, or shadow.
