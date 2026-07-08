# Amazon Walk Prompts

## Animation Contract

```text
Animation contract:
walk cycle for a 2D isometric battle sprite.
Facing down-right.
Movement should feel steady, readable, and suitable for a looping game animation.
Do not exaggerate limb motion.
Keep the torso stable.
Keep silhouette readable in every frame.
```

## Full Prompt Template

```text
[Paste base_style_prompt.md]

[Paste character_prompt.md]

Animation contract:
walk cycle for a 2D isometric battle sprite.
Facing down-right.
Movement should feel steady, readable, and suitable for a looping game animation.
Do not exaggerate limb motion.
Keep the torso stable.
Keep silhouette readable in every frame.

View contract:
2D isometric 45-degree angle view.
Facing down-right.

Frame contract:
This is frame [X] of 8.

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

### Frame 1 of 8

```text
Frame contract:
This is frame 1 of 8.

Pose details:
head level and facing down-right
torso upright with a slight forward traveling intent
left arm shield moves slightly back
right arm javelin hand swings slightly forward
left leg steps forward into contact
right leg pushes from behind
weapon state: one javelin only, held in a controlled walking swing
shield state: one shield only, kept readable and close to the body
hair state: ponytail trails slightly backward

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 2 of 8

```text
Frame contract:
This is frame 2 of 8.

Pose details:
head level
torso moves over the front foot with stable balance
left arm shield remains slightly back
right arm javelin hand begins to move toward center
left leg supports body weight
right leg lifts forward from behind
weapon state: one javelin only, no duplicate shaft
shield state: one shield only, stable readable round silhouette
hair state: ponytail follows the body movement with a small delay

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 3 of 8

```text
Frame contract:
This is frame 3 of 8.

Pose details:
head level and calm
torso passes through the middle of the stride
left arm shield returns toward center
right arm javelin hand moves back toward neutral
left leg remains under the body
right leg swings forward
weapon state: one javelin only, stable walking carry
shield state: one shield only, no extra rim or duplicate shield
hair state: ponytail settles behind the head

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 4 of 8

```text
Frame contract:
This is frame 4 of 8.

Pose details:
head level
torso remains stable with only slight vertical motion
left arm shield moves slightly forward
right arm javelin hand swings slightly back
left leg begins to push off behind
right leg reaches forward into contact
weapon state: one javelin only, not raised to throw
shield state: one shield only, compact guard while walking
hair state: ponytail lags behind the forward stepping motion

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 5 of 8

```text
Frame contract:
This is frame 5 of 8.

Pose details:
head level and steady
torso moves over the new front foot
left arm shield continues a small forward swing
right arm javelin hand begins to return toward center
left leg trails behind
right leg supports the body
weapon state: one javelin only, readable silhouette maintained
shield state: one shield only, no overlap that hides the character entirely
hair state: ponytail follows with a small bounce

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 6 of 8

```text
Frame contract:
This is frame 6 of 8.

Pose details:
head calm and level
torso passes through the center again
left arm shield moves back toward neutral
right arm javelin hand moves forward toward the next swing
left leg lifts and starts swinging forward
right leg stays under the body
weapon state: one javelin only, controlled walking arc
shield state: one shield only, stable round form
hair state: ponytail trails lightly behind

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 7 of 8

```text
Frame contract:
This is frame 7 of 8.

Pose details:
head level
torso stays upright and readable
left arm shield swings slightly back
right arm javelin hand swings slightly forward
left leg reaches forward toward contact
right leg begins pushing off behind
weapon state: one javelin only, no attack motion
shield state: one shield only, readable body-side placement
hair state: ponytail follows the stride with a tiny delay

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```

### Frame 8 of 8

```text
Frame contract:
This is frame 8 of 8.

Pose details:
head returns toward the same alignment as frame 1
torso returns toward the same loop point as frame 1
left arm shield placement loops toward frame 1
right arm javelin swing loops toward frame 1
left leg reaches the same front contact logic as frame 1
right leg reaches the same rear push logic as frame 1
weapon state: one javelin only, loops cleanly back to frame 1
shield state: one shield only, loops cleanly back to frame 1
hair state: ponytail returns toward the same trailing position as frame 1

Consistency constraints:
preserve the exact same character identity, same body proportions, same face style, same outfit shape, same colors, same javelin design, same shield design, same pixel density, same camera angle, same framing, and same scale.
```
