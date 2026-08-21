# Deterministic ImageGen Task Packet
## Frozen invariants
- near-round spherical flying core locked to the approved bat anchor
- exactly two pointed ears and exactly two membrane wings attached to the core
- no paws, arms, legs, humanoid torso, or tail
- dark plum body, red wing membranes, yellow eyes, and ivory fangs
- preserve the approved hover height and virtual tile landing axis
- no scabbard anywhere

## Reference responsibilities
- action: Tools/artworks/pure_run/enemies/approved/tomb_maw_bat_hit_dr_v01.png @ a62bef402f80a4eb30957e2f6884ac0f3c29db7fd51e1faa375a83abc8c43f5a
- hit_pose: godot/assets/units/actions/doge_hunter_hit_ul.png @ 0eb88380ebee80e99d1208b0adf5dca6b91ed368c94039ae298b9c4bb0c297af
- identity: Tools/artworks/pure_run/enemies/approved/tomb_maw_bat_ranged_color_ul_v01.png @ d8c0037bd029f6f59049d846a4698e0fed63d1d139fb6fde0642cc944c12facb

## Composition
```json
{
  "canvas": [
    256,
    256
  ],
  "coreAxis": {
    "bottom": [
      124,
      178
    ],
    "tiltDegrees": [
      6.0,
      12.0
    ],
    "top": [
      139,
      88
    ]
  },
  "equipmentState": {
    "scabbard": "absent",
    "staticEffects": "absent"
  },
  "footCenter": [
    128,
    236
  ],
  "forbiddenRegions": [],
  "weapon": {
    "exitWindow": [
      87,
      103,
      146,
      151
    ],
    "hiddenGrip": [
      128,
      145
    ],
    "tipRegion": [
      80,
      99,
      149,
      157
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
# Tomb Maw Bat Hit UL v01

Generate one native up-left rear three-quarter Hit frame for the approved Tomb Maw Bat.

- Identity source: approved Tomb Maw Bat Idle UL is the sole authority for rear anatomy, spherical dark-indigo core, ear direction, wing construction, magenta membranes, line weight, rendering style, scale, and up-left orientation.
- Action source: approved Tomb Maw Bat Hit DR supplies only the accepted impact peak: rigid spherical recoil toward screen-right, backward/inward wing flinch, folded-ear inertia, startled eye treatment, tense mouth, and tear motion. Do not copy its frontal anatomy or two-eye visibility.
- Pose source: formal red-shiba Amazon Hit UL supplies only the native rear-view cartoon exaggeration and visibility discipline. Do not transfer dog anatomy, fur, paws, shield, clothing, or grounded stance.
- Preserve the back of the spherical skull and torso. Show only the one near-side yellow eye anatomically visible from this rear three-quarter view, with a small dark pupil, plus exactly one short blue-white tear streak moving outward. Never reveal a second eye or reconstruct a frontal face.
- Recoil the whole ball core toward screen-right as one rigid body without stretching, flattening, bending, or becoming pear-shaped.
- Both wings remain attached and flinch backward/inward with clear near/far depth. Fold both ears backward/downward with inertia while preserving the approved UL ear orientation.
- No splash clusters, extra tears, stars, impact burst, blood, wound, damage number, wind blade, projectile, glow, or magic effect.
- Preserve normal flying scale and generous padding. Uniform flat #00ff00 chroma background only; no shadow, floor, text, UI, border, or watermark.
