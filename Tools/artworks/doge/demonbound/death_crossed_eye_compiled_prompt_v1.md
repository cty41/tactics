# Deterministic ImageGen Task Packet

## Frozen invariants
- transparent expression overlay only
- exactly two compact crossed-eye marks
- no face, coat, ears, mouth, collar, paws, equipment, effects, text, or watermark
- no scabbard anywhere

## Reference responsibilities
- edit_target: Tools/artworks/doge/candidates/doge_capsule_demonbound_death_geometry_wip_v04.png @ 6679c76575de7c340dafed107ce485498dfe6e4cb308a19240cc6fa2aad032c0
- eye_guide: Tools/artworks/doge/demonbound/death_crossed_eye_guide_v1.png @ fb78f7261e85506d39ccab6d1cfc6f221196bb2063d0a866ca76765f7f0c225d

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
      88,
      172
    ],
    "tiltDegrees": [
      38.0,
      48.0
    ],
    "top": [
      166,
      104
    ]
  },
  "equipmentState": {
    "scabbard": "absent",
    "staticEffects": "absent"
  },
  "footCenter": [
    127,
    150
  ],
  "forbiddenRegions": [],
  "requiredHiddenLabels": [],
  "visibleAreaCaps": {},
  "weapon": {
    "bladeCenterline": [
      111,
      187,
      184,
      216
    ],
    "exitWindow": [
      94,
      180,
      122,
      207
    ],
    "guardWindow": [
      96,
      181,
      121,
      205
    ],
    "hiddenGrip": [
      103,
      193
    ],
    "maxBladeWidthPx": 9,
    "maxGemAreaPx": 24,
    "tipRegion": [
      171,
      201,
      193,
      224
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
# Demonbound Death Crossed-Eye Overlay v1

Generate only two compact black cartoon X eye marks for the supplied final Demonbound death geometry.

- The final corpse reference fixes the face and eye positions; do not redraw any character pixel.
- The guide supplies the two allowed eye rectangles.
- Output exactly two black X marks, one centered in each rectangle, with rounded hand-drawn strokes matching the character outline.
- Everything except the two X marks must be flat `#00ff00` chroma background.
- No face color, fur, ears, mouth, nose, collar, paws, sword, shadow, text, watermark or additional mark.
