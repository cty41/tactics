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
- pose_guide: Tools/artworks/doge/demonbound/cast_down_right_pose_guide_v2.png @ af59f7724aa9f2025b7464cf36f5150bd61809aece0c95e5b05e29e309b487be

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
      -3.0,
      3.0
    ],
    "top": [
      127,
      121
    ]
  },
  "equipmentState": {
    "scabbard": "absent"
  },
  "footCenter": [
    127,
    236
  ],
  "forbiddenRegions": [
    {
      "name": "eyes",
      "rect": [
        101,
        126,
        157,
        158
      ]
    }
  ],
  "requiredHiddenLabels": [],
  "visibleAreaCaps": {
    "equipment": 0.22
  },
  "weapon": {
    "exitWindow": [
      155,
      142,
      174,
      188
    ],
    "hiddenGrip": [
      132,
      178
    ],
    "maxGemAreaPx": 36,
    "tipRegion": [
      174,
      100,
      224,
      158
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
# Demonbound Cast DR

Create the down-right three-quarter casting pose for the approved Demonbound capsule character. Preserve the approved Idle DR identity, equal-width rigid capsule geometry, Doberman face, half-body alternate coat color, heterochromic ear, gray-white forehead blaze, collar, four attached paws, and ancestral cursed sword design.

The character channels demonic magic toward a down-right target. Keep both eyes on that target. No arms or legs. The four paws remain attached directly to the capsule body. The sword is a compact one-handed ancestral treasure sword with one small red guard gem. No scabbard is present during the cast. Keep all equipment outside the eye exclusion zone and obey the supplied pose guide exactly.
