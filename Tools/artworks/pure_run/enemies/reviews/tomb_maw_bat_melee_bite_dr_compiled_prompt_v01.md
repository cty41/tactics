# Deterministic ImageGen Task Packet
## Frozen invariants
- equal-width rigid capsule body
- exactly four paws directly attached to the body
- no arms and no legs between paws and body
- gray-white forehead blaze and heterochromic ear
- half-body alternate coat color
- no scabbard anywhere

## Reference responsibilities
- mother_anchor: Tools/artworks/pure_run/enemies/approved/tomb_maw_bat_ranged_color_v06.png @ d71bd44be7dec83f116133ce8ab03394930dd3ade61cd21ea68fbb463464bc8a

## Composition
```json
{
  "canvas": [
    256,
    256
  ],
  "coreAxis": {
    "bottom": [
      132,
      178
    ],
    "tiltDegrees": [
      -8.0,
      8.0
    ],
    "top": [
      124,
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
      102,
      126,
      154,
      181
    ],
    "hiddenGrip": [
      128,
      145
    ],
    "tipRegion": [
      101,
      132,
      155,
      188
    ]
  }
}
```

## Unresolved fixes
- none

## Base prompt
Create one native down-right, front three-quarter isometric melee action Sprite for the Tomb Maw Bat at the peak of a vampiric bite lunge.

Input responsibilities:
- The approved down-right Idle Sprite is the only identity, scale, colour, linework, spherical-core, face, ear, wing anatomy, hover-height, and isometric-direction mother image.
- The pose guide controls only the screen-space action axis and mouth target region. It is not identity or style reference.

Action:
- Freeze the instant immediately before the jaws clamp onto a nearby target: a short aggressive forward bite, not Idle hovering.
- Open the mouth substantially wider than Idle. Make the dark red mouth cavity and two dominant long ivory upper fangs immediately readable at 128px; smaller lower fangs may be visible but must not look like a second mouth.
- Push the muzzle and lower jaw slightly toward screen down-right while the spherical core counter-leans only subtly. Keep the body a near-round ball, never a long oval or capsule.
- Sweep both wings backward and slightly inward as braking/counterforce for the lunge. They must read as an attack silhouette, not the horizontal Wind Blade sweep and not a symmetrical Idle spread.
- Keep both yellow eyes visible, focused and predatory. Preserve the same two ears and the same dark plum/red palette.

Vampiric meaning and boundaries:
- Communicate damage and life-drain potential through the forceful bite and unmistakable fangs only.
- Do not draw blood, wounds, a victim, red energy, healing particles, numbers, wind blades, speed lines, ground, shadow, text, or watermark. Runtime presentation may add hit/heal feedback later without changing gameplay results.

Sprite contract:
- One complete opaque subject on a perfectly flat solid #00ff00 chroma-key background.
- Background is one uniform colour with no gradient, texture, floor plane, lighting variation, cast shadow, or reflection. Do not use #00ff00 in the subject.
- Keep the entire character inside generous canvas padding. Preserve a clean continuous outline and crisp project-style antialiasing.
- The spherical core is the scale and horizontal anchor; wings do not participate in scaling or centering.
- Virtual landing point remains (128,236), vertically aligned with the spherical core and the 64x32 Tile center.
