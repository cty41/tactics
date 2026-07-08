# Amazon Idle Prompts

## Animation Contract

```text
Animation contract:
idle combat stance for a 2D isometric battle sprite.
Facing down-right.
Body upright and stable.
Ready for combat, but not attacking.
No dramatic motion.
No running energy.
The pose should feel controlled, planted, and loop-friendly.
```

## Full Prompt Template

```text
[Paste base_style_prompt.md]

[Paste character_prompt.md]

Animation contract:
idle combat stance for a 2D isometric battle sprite.
Facing down-right.
Body upright and stable.
Ready for combat, but not attacking.
No dramatic motion.
No running energy.
The pose should feel controlled, planted, and loop-friendly.

View contract:
2D isometric 45-degree angle view.
Facing down-right.

Frame contract:
This is frame [X] of 6.

Pose details:
[head state]
[torso state]
[left arm state]
[right arm state]
[left leg state]
[right leg state]
[weapon state]
[shield state]
[hair state]

Final frame requirements:
single sprite frame only
full body visible
centered
no extra objects
no duplicate weapon
no duplicate limbs
no effects
no cast shadow
```

## Frame Skeletons

### Frame 1 of 6

```text
Frame contract:
This is frame 1 of 6.

Pose details:
head upright and level
torso compact and stable
left arm holds the round shield close to the body
right arm holds the javelin in a relaxed ready position
left leg planted firmly
right leg planted firmly in a balanced stance
weapon state: one javelin held clearly and simply, not being thrown
shield state: one round shield facing outward in a defensive ready position
hair state: ponytail resting naturally with minimal motion

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 2 of 6

```text
Frame contract:
This is frame 2 of 6.

Pose details:
head slightly lowered but still level
torso steady with a very small breathing lift
left arm keeps the shield close to the torso
right arm relaxes the javelin angle slightly downward
left leg planted
right leg planted with a tiny weight shift
weapon state: one javelin only, still in ready position
shield state: one shield only, unchanged silhouette
hair state: ponytail barely shifting with the breathing motion

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 3 of 6

```text
Frame contract:
This is frame 3 of 6.

Pose details:
head centered and calm
torso returns to neutral center
left arm keeps the shield in a compact guard
right arm keeps the javelin close to the body with a slight upward ready angle
left leg firmly planted
right leg firmly planted
weapon state: one javelin only, stable silhouette, no throw motion
shield state: one shield only, stable round silhouette
hair state: ponytail nearly still

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 4 of 6

```text
Frame contract:
This is frame 4 of 6.

Pose details:
head upright and level
torso shows a very small breathing drop
left arm shield guard remains close and readable
right arm lets the javelin angle dip slightly
left leg planted with stable balance
right leg planted with a slight opposite weight shift
weapon state: one javelin only, no duplicate shaft, no attack wind-up
shield state: one shield only, unchanged defensive position
hair state: ponytail follows the subtle body settling motion

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 5 of 6

```text
Frame contract:
This is frame 5 of 6.

Pose details:
head calm and steady
torso rises back toward neutral breathing height
left arm shield position remains compact
right arm returns the javelin to a clearer ready angle
left leg firmly planted
right leg firmly planted
weapon state: one javelin only, controlled ready pose
shield state: one shield only, readable outer edge
hair state: ponytail settles back toward rest

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 6 of 6

```text
Frame contract:
This is frame 6 of 6.

Pose details:
head returns to the same neutral level as frame 1
torso returns to the same stable idle center as frame 1
left arm shield guard matches the starting pose
right arm javelin ready position matches the starting pose
left leg planted exactly like the starting pose
right leg planted exactly like the starting pose
weapon state: one javelin only, silhouette loops cleanly back to frame 1
shield state: one shield only, loops cleanly back to frame 1
hair state: ponytail returns to the resting starting pose

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```
