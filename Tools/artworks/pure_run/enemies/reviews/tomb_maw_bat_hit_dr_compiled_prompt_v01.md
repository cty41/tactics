# Deterministic ImageGen Task Packet
## Frozen invariants
- near-round spherical flying core locked to the approved bat anchor
- exactly two pointed ears and exactly two membrane wings attached to the core
- no paws, arms, legs, humanoid torso, or tail
- dark plum body, red wing membranes, yellow eyes, and ivory fangs
- preserve the approved hover height and virtual tile landing axis
- no scabbard anywhere

## Reference responsibilities
- hit_pose: godot/assets/units/actions/doge_hunter_hit_dr.png @ a921486bec40be843d02efdce8617e2a1b764c33e6a9e8e4278cf76c92fa9e01
- identity: Tools/artworks/pure_run/enemies/approved/tomb_maw_bat_ranged_color_v06.png @ d71bd44be7dec83f116133ce8ab03394930dd3ade61cd21ea68fbb463464bc8a

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
      92,
      104,
      166,
      151
    ],
    "hiddenGrip": [
      128,
      145
    ],
    "tipRegion": [
      87,
      100,
      171,
      157
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
# Tomb Maw Bat Hit DR v01

Generate one native down-right three-quarter Hit frame for the approved Tomb Maw Bat.

- Identity source: the approved Tomb Maw Bat Idle DR is the sole authority for its spherical dark-indigo core, triangular ears, yellow eyes, red mouth interior, magenta wing membranes, line weight, rendering style, scale, and down-right anatomy.
- Pose source: the approved red-shiba Amazon Hit DR supplies only the medium chibi impact peak and facial-reaction language. Do not transfer dog anatomy, fur, paws, shield, clothing, body proportions, or grounded stance.
- Treat the central spherical core as one rigid body recoiling toward screen-right. The core leans as a whole without stretching, flattening, bending, or becoming pear-shaped.
- Both wings remain anatomically attached but flinch backward/inward with impact inertia. Preserve clear near/far depth; do not detach, duplicate, or symmetrically spread them into an attack pose.
- Fold both ears slightly backward/downward with inertia while preserving their identity and attachment.
- Show two widened yellow eyes with distinctly smaller dark pupils, a small tense wavy mouth, and exactly two short blue-white tear streaks, one emerging from each visible eye toward the sides.
- Tears are facial reaction marks only: no water splash, droplet cluster, stars, impact burst, blood, wound, damage number, wind blade, projectile, glow, or magic effect.
- Preserve the flying unit's normal apparent body size and generous canvas padding. Uniform flat #00ff00 chroma background only, with no shadow, floor, text, UI, border, or watermark.
