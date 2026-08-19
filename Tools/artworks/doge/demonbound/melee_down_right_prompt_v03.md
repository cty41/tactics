# Demonbound melee down-right ImageGen task v3

Generate one native down-right single-frame melee pose for the approved Demonbound Idle DR. This is the same character performing a compact one-handed sword attack, not a redesign.

Reference responsibilities:

- The approved Demonbound Idle DR is the only authority for identity, exact narrow equal-width capsule core, Doberman face, split alternate coat, broad gray-white forehead blaze, ear colors, four-paw topology, collar, and the noble cursed ancestral sword.
- The runtime Hunter Melee DR supplies only the grounded forward melee action language. Do not copy its breed, orange coat, spear, shield, equipment, gaze, or proportions.

World-space action:

- The character faces down-right and performs a controlled one-handed diagonal cleave at a target one adjacent tile ahead.
- The character actively watches that target. Both pupils follow the sword's attack line toward screen-right/lower-right; neither eye looks at the viewer or out through the camera.
- Shift the rigid capsule modestly toward its forward side while both foot paws stay grounded. No bending, jumping, or spinning.
- The active sword-swing frame has no scabbard whatsoever: no sheath, empty scabbard, belt sheath, back sheath, or duplicate sword-shaped object.

Screen-space acceptance:

- The capsule top center is visibly to screen-right of the midpoint between the two grounded foot paws, producing a modest but readable forward lean.
- Preserve the exact narrow approved core: approximately 64 pixels wide after calibration, with near-parallel middle and lower sides and no pear-shaped expansion.
- Restore the broad gray-white forehead blaze between the ears and the approved alternate-color ear.
- The sword grip sits at the lower front-center of the body. The sword-holding near paw is a small attached contour pad mostly hidden inside the capsule edge, never a complete circular disk and never connected by an arm.
- The far hand is a smaller asymmetric attached edge arc on the opposite contour. It does not mirror the sword hand.
- Preserve the original ornate ancestral sword at its believable approved scale. The entire sword should read at roughly three quarters of the capsule core height; the visible blade from guard to tip should be roughly 55–60% of the core height. This is longer and more imposing than rejected v2, but clearly smaller than rejected v1's oversized broad blade.
- The narrow blade projects toward screen-right/lower-right and stays below the eyes and muzzle. Do not turn it into a dagger, broad greatsword, spear, or generic plain blade.
- Both pupils are displaced toward screen-right/lower-right within their visible eye shapes. Their gaze converges on the same off-body attack target along the blade direction.
- Preserve exactly two hand paws and two foot paws. No arms, legs, floating paws, or single-pixel contacts. Both feet finish on the y=236 baseline.

Preserve the approved face, muzzle, split gray/black coat, orange markings, collar, alternate ear, forehead blaze, line weight, and original sword ornamentation. Equipment changes the outer silhouette but never the core scale.

Output one isolated full-body subject on a perfectly uniform flat solid `#00FF00` chroma background. No gradient, shadow, ground, scenery, text, border, watermark, slash trail, magic effect, blood, shield, spear, second weapon, scabbard, sheath, visible limb, pear shape, camera-facing gaze, or crop.
