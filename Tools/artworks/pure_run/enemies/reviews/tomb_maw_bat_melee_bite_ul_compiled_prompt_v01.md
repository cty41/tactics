# Deterministic ImageGen Task Packet
## Frozen invariants
- near-round spherical flying core locked to the approved bat anchor
- exactly two pointed ears and exactly two membrane wings attached to the core
- no paws, arms, legs, humanoid torso, or tail
- dark plum body, red wing membranes, yellow eyes, and ivory fangs
- preserve the approved hover height and virtual tile landing axis
- no scabbard anywhere

## Reference responsibilities
- action_reference: Tools/artworks/pure_run/enemies/approved/tomb_maw_bat_melee_bite_attack_dr_v01.png @ 275e36c12c1ff77f9c26d76cdebad6500a81419f56b8d50971053e17c1fbb585
- mother_anchor: Tools/artworks/pure_run/enemies/approved/tomb_maw_bat_ranged_color_ul_v01.png @ d8c0037bd029f6f59049d846a4698e0fed63d1d139fb6fde0642cc944c12facb

## Composition
```json
{
  "canvas": [
    256,
    256
  ],
  "coreAxis": {
    "bottom": [
      130,
      178
    ],
    "tiltDegrees": [
      -8.0,
      8.0
    ],
    "top": [
      126,
      92
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
      68,
      119,
      128,
      181
    ],
    "hiddenGrip": [
      104,
      145
    ],
    "tipRegion": [
      60,
      126,
      126,
      190
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Create one native up-left, rear three-quarter isometric melee action Sprite for the Tomb Maw Bat at the same vampiric bite-lunge peak as the approved DR action.

Input responsibilities:
- The approved UL Idle Sprite is the only identity, scale, colour, linework, spherical-core, rear head anatomy, ear, wing anatomy, hover-height, and up-left direction mother image.
- The approved DR bite Sprite controls only the frozen action moment, mouth opening intensity, prominent fangs, and backward braking-wing intent. Do not copy its front-facing anatomy or screen-side layout.
- The pose guide controls only the screen-space core axis and bite target region. It is not identity or style reference.

Action and projection:
- Reconstruct the same short aggressive bite lunge in 3D while the bat faces world up-left under the fixed isometric camera.
- The muzzle and open jaws project from the screen-left side of the spherical core toward the nearby up-left target. The mouth must not appear centered as a full frontal face.
- Show only the single yellow eye and side facial plane that are anatomically visible from this rear three-quarter view. Do not invent a second visible eye or turn the head into a front view.
- Keep the dark red mouth cavity and one or two visible long ivory fangs readable at 128px. Partial occlusion of the far fang is correct for this view.
- Sweep both wings backward and slightly inward relative to the lunge. Preserve clear near/far depth: the near wing may be larger and more visible; the far wing may be partially hidden by the spherical core.
- This must read as the UL counterpart of the approved bite DR, not Idle flight, Wind Blade release, hit reaction, or death.

Vampiric meaning and boundaries:
- Communicate damage and life-drain through the forceful bite and fangs only.
- Do not draw blood, wounds, victim, red energy, healing particles, numbers, wind blades, speed lines, ground, shadow, text, or watermark.

Sprite contract:
- One complete opaque subject on a perfectly flat uniform solid #00ff00 chroma-key background, with no gradient, texture, floor, shadow, reflection, or lighting variation. Do not use #00ff00 in the subject.
- Preserve a near-round dark-plum flying core, exactly two pointed ears, exactly two red membrane wings, no paws, arms, legs, humanoid torso, or tail.
- Keep the whole silhouette inside generous padding. The spherical core is the scale anchor; wings do not participate in scaling or centering.
- Preserve the approved UL hover height and virtual landing axis at (128,236).
