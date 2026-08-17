# Amazon Attack Prompts

## Animation Contract

```text
Animation contract:
attack animation for a 2D isometric battle sprite.
Facing down-right.
Use a short readable combat action with clear anticipation, strike, and recovery.
Keep the action compact and game-usable.
Do not turn it into a dramatic illustration pose.
Maintain sprite readability and consistent proportions.
```

## Full Prompt Template

```text
[Paste base_style_prompt.md]

[Paste character_prompt.md]

Animation contract:
attack animation for a 2D isometric battle sprite.
Facing down-right.
Use a short readable combat action with clear anticipation, strike, and recovery.
Keep the action compact and game-usable.
Do not turn it into a dramatic illustration pose.
Maintain sprite readability and consistent proportions.

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
head focused forward
torso slightly lowered into anticipation
left arm keeps the shield close in defense
right arm draws the javelin arm slightly back
left leg planted in front for balance
right leg planted behind for support
weapon state: one javelin only, preparing to thrust or slash, not thrown
shield state: one shield only, compact guarding position
hair state: ponytail compresses slightly with the body lowering

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 2 of 6

```text
Frame contract:
This is frame 2 of 6.

Pose details:
head stays locked on the target direction
torso coils a bit more for a compact wind-up
left arm shield remains close and stable
right arm pulls the javelin arm further back
left leg stays planted
right leg loads more weight
weapon state: one javelin only, clear anticipation silhouette, no duplicate spear
shield state: one shield only, no extra shield rim
hair state: ponytail begins trailing opposite the coming strike

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 3 of 6

```text
Frame contract:
This is frame 3 of 6.

Pose details:
head forward and committed
torso drives forward into the strike
left arm braces the shield defensively
right arm thrusts or slashes the javelin forward in a short readable attack
left leg anchors the forward motion
right leg pushes from behind
weapon state: one javelin only, strongest attack silhouette, no thrown-release motion
shield state: one shield only, still attached to the guarding arm
hair state: ponytail trails backward from the strike acceleration

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 4 of 6

```text
Frame contract:
This is frame 4 of 6.

Pose details:
head follows through slightly
torso reaches the end of the strike and begins to recover
left arm keeps the shield guarding the body
right arm starts pulling the javelin back from the hit frame
left leg remains planted
right leg begins regaining balance
weapon state: one javelin only, leaving the peak strike silhouette
shield state: one shield only, unchanged defensive identity
hair state: ponytail continues trailing then starts to settle

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 5 of 6

```text
Frame contract:
This is frame 5 of 6.

Pose details:
head returns toward neutral battle focus
torso rises back toward idle posture
left arm shield returns to compact guard
right arm retracts the javelin toward ready position
left leg stays stable
right leg returns under the body
weapon state: one javelin only, no duplicate tip or extra shaft
shield state: one shield only, clean readable outline
hair state: ponytail settles back toward rest

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 6 of 6

```text
Frame contract:
This is frame 6 of 6.

Pose details:
head returns to the neutral combat-ready state
torso returns to the same stable ready posture used before the attack
left arm shield guard matches the resting combat pose
right arm javelin ready position matches the resting combat pose
left leg returns to the starting combat footing
right leg returns to the starting combat footing
weapon state: one javelin only, loops cleanly into the ready state
shield state: one shield only, loops cleanly into the ready state
hair state: ponytail returns to the resting pose

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```
