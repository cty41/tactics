# Deterministic ImageGen Task Packet

## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color
- no scabbard anywhere

## Reference responsibilities
- equipment: Tools/artworks/doge/calibrated/doge_capsule_demonbound_melee_dr_v01.png @ a0fee8512cf8578f57b8b3d3c5e2b74a1f68ac3afdcbefd15a87c82f5a85eaf5
- identity: Tools/artworks/doge/calibrated/doge_capsule_demonbound_idle_dr_v01.png @ 21559ca7aedd3e146feb2b93c5a6633affc6ce544122628c960972a982b0b5e7
- pose: Tools/artworks/doge/calibrated/doge_capsule_hunter_death_color_v04.png @ f5997651edd71cc78b926d5824ef4ac178f28604f5d7c9846f5a81ee0a2b3d19

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
# Demonbound Death v1

Generate one independent death-state sprite for the approved Demonbound capsule ground unit.

- Identity source: approved Demonbound Idle DR is the sole authority for the dark gray and black coat split, broad gray-white forehead blaze, orange inner ears and paws, red collar, compact Doberman-like face, line weight and ancestral sword design.
- Death-pose reference: approved Hunter Death supplies only the corpse direction, supine posture and compact cartoon exaggeration; do not migrate the Hunter breed, orange coat, shield or equipment.
- Rebuild the body as a short thick straight capsule lying flat on its back. Face and chest point upward; the head points toward screen upper-right; the long axis is about 60 degrees in screen space. Do not rotate the standing sprite, curl the body, arch the back or make it slender.
- Keep exactly two ears, two hands and two feet. The paws rest naturally against the fallen body without arms or legs. Eyes are closed or unfocused; no tears, blood, wounds, stars or death effects.
- Preserve exactly one compact ancestral sword, but it must be fully released from the hand. Lay it close below the corpse, separated from every paw by a clear transparent gap. Use the same narrow silver-gray blade, restrained guard, brown-red grip and one tiny red gem. No scabbard and no second weapon.
- Center the complete corpse-and-sword silhouette on the 256 by 256 canvas with generous safe margins. Do not use the standing foot baseline as the visual anchor.
- Use a perfectly flat solid `#00ff00` chroma-key background without shadow, gradient, texture, floor, reflection, text or watermark.
