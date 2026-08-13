# Tactics Manual Acceptance Ledger

This is the current cross-project manual acceptance state. Stable IDs are authoritative; list numbers are temporary.

## Pending

### MQA-GODOT-PAUSE-MENU — Esc pause and safe exit flow

- Status: `pending`
- Source: Phase 8E pause overlay hierarchy and menu scope fix
- Reopen reason: The reported overlay rendered below actors and exposed a noncanonical Save and Quit action; the fix raises the overlay and removes that action.
- Action: During a battle press Esc, exercise Continue, Options/Back and Main Menu, then open Esc again while targeting and while CheatConsole is visible.
- Expected: The dark overlay and menu cover all actors/HUD; only Continue, Options and Main Menu appear; targeting/Console take Esc precedence; Quit remains only on Home.
- Observe: Pause overlay, battle HUD, Home menu, playback state, and CheatConsole.
- Preserve on failure: Screenshot and exact Esc/menu action sequence; keep the Run and Output open.
- Save boundary: Main Menu preserves the Active Run and Continue restarts from the committed pre-battle checkpoint.
- Automated evidence: Z-order, menu actions, input blocking, pause ownership and absence of Save and Quit are asserted; visual stacking and interaction feel remain manual.
- User verdict: Failed before this fix: actors appeared above the menu and Save and Quit should not exist there.

### MQA-GODOT-DAMAGE-NUMBERS — Floating combat feedback

- Status: `pending`
- Source: Phase 8E camera/menu/damage/growth transaction
- Reopen reason: Poison tick now has an explicit committed Impact cue instead of falling through at frame completion.
- Action: Produce normal damage, a Poison turn-start tick, critical, Miss, healing, and MP recovery while trying Pause and all speed values.
- Expected: Direct and Poison damage show white `-N`, healing green `+N`, MP blue `+N MP`, critical gold and Miss gray; each appears above the affected unit at its committed impact, pauses with presentation, and leaves no residue.
- Observe: Unit head anchor, animation timing, HUD speed, and Godot Output.
- Preserve on failure: Screenshot/video plus event log and speed/pause state.
- Save boundary: Ordinary battle state changes apply; use a disposable Run if exact replay matters.
- Automated evidence: Event-to-number identity, explicit Poison tick marker, multi-hit sequence, pause/speed propagation, and cleanup are asserted; readability and timing remain manual.
- User verdict: none.

### MQA-GODOT-PROGRESSION-ATOMIC — Non-skippable atomic growth

- Status: `pending`
- Source: Phase 8E camera/menu/damage/growth transaction
- Action: Allocate an attribute, close before choosing a skill, Continue, then finish the progression once.
- Expected: Continue restarts at attribute allocation; no Back to Map exists; final confirmation applies attributes and skill once and unlocks the next node once.
- Observe: Progression pages, map node state, and Home Continue.
- Preserve on failure: Copy of the save, screenshots before/after Continue, and Output.
- Save boundary: PendingProgression persists; unfinished UI drafts must not be written.
- Automated evidence: Revision, V5 normalization, one-shot transaction, and unlock semantics are asserted; navigation behavior remains manual.
- User verdict: none.

### MQA-GODOT-INVENTORY — Backpack and loadout workflow

- Status: `pending`
- Source: Phase 7B Inventory; never manually completed
- Action: Obtain Equipment and Consumable through Store/Mystery, then Equip, Replace, Unequip, Carry, Replace Carried, Unload, and Reload.
- Expected: Details and derived stats refresh immediately; each instance exists in exactly one location and survives Reload.
- Observe: Inventory character list, backpack tabs, item details, slots, carried item, and derived stats.
- Preserve on failure: Screenshot, item instance ID/name, route, save copy, and Output.
- Save boundary: Use a dedicated Run because purchases and loadout operations persist.
- Automated evidence: Atomic commands, instance uniqueness, projection, V5 round trip, and isolated Store journey are asserted; usability remains manual.
- User verdict: User previously stated Inventory had not been tested.

### MQA-GODOT-HUD-SKILL-PRESENTATION — HUD and committed skill visuals

- Status: `pending`
- Source: Phase 8E compact action bar and committed-presentation highlight fix
- Reopen reason: Action buttons now use display names and compact second-line costs; active-tile highlighting is suppressed during committed action playback.
- Action: Check the compact action bar, vertical Lightning, overlapping alpha-aware selection, Poison Spear held/unarmed/drop/Pickup or Recover, HUD controls, and Backquote CheatConsole.
- Expected: Basic attacks have no MP line; skill IDs have no `skill.` prefix; positive MP cost is on line two; during action animation the acting unit has no selected tile. Remaining skill/HUD visuals follow their committed state.
- Observe: Action buttons, actor feet/tile, battle actors, HUD, hover detail, CheatConsole, and Output.
- Preserve on failure: Screenshot/video, selected actor ID/tile, button text, skill event log, and current encounter.
- Save boundary: Skills mutate the battle; use a replayable encounter checkpoint.
- Automated evidence: Label formatting, action-state marker suppression, event-derived targets, alpha hit ordering, spear lifecycle, console guards, and renderer smoke are asserted; sizing and visual parity remain manual.
- User verdict: Poison Spear visual sub-check was reported OK before this action-bar change; the combined HUD item remains pending.

### MQA-GODOT-FULL-RUN — Complete Run shell and route recovery

- Status: `pending`
- Source: Phase 8E N3 authoritative resume fix
- Reopen reason: User reported that the third battle node could return `run.not_ready` after two victories.
- Action: Complete N1 and its growth, complete N2 and its growth, then click N3; later continue through BossVictory and alternate Layer 4/6 routes in disposable Runs.
- Expected: N3 opens immediately when Ready; a PendingBattle resumes the same checkpoint and incomplete growth routes back to Progression instead of leaving a dead map node. Later route transactions resolve once and terminal summary appears once.
- Observe: Map node status/detail, N3 battle title, Progression, Settlement, Home Continue, and Summary.
- Preserve on failure: Run seed, node state/reason, save and backup copies, screenshot, and Output.
- Save boundary: Use separate disposable Runs for mutually exclusive routes; never reuse a valuable save as a fixture.
- Automated evidence: Two victories plus two completed growth transactions now assert Ready/index 2/N3 request; pending/resume and full deterministic journeys are also covered. End-to-end UX remains manual.
- User verdict: Failed before this fix: N3 could not be entered after the first two battles.

### MQA-GODOT-RELOAD-OUTPUT — Reload and diagnostics cleanup

- Status: `pending`
- Source: Phase 7B–8E combined acceptance
- Action: Assembly Reload, Continue the active Run, replay one battle action, and inspect logs.
- Expected: No stale Tween, temporary node, duplicate signal, input lock, corrupted save, or Unicode/NUL error.
- Observe: Godot Output and CheatConsole; verify current page and actor state visually.
- Preserve on failure: Full Output excerpt, page/encounter, save copy, and reproduction order.
- Save boundary: Reload may restart the current battle from its committed checkpoint; retain save evidence on failure.
- Automated evidence: Headless reload and cleanup paths are asserted; canonical Editor reload behavior remains manual.
- User verdict: none after latest lifecycle-affecting changes.

## Passed

### MQA-GODOT-BOARD-FIT — Isometric board framing and input

- Status: `passed`
- Source: Phase 8E camera/menu/damage/growth transaction
- Action: Open a battle, resize across 16:9, 16:10, and 21:9, then hover and click edge and overlapping units.
- Expected: The complete 10×10 board is centered without the old right-side reserve; HUD stays clear and pointer selection remains accurate.
- Observe: Battle board, HUD bounds, hover details, and selected tile/unit.
- Preserve on failure: Screenshot with resolution, selected coordinate, and current encounter.
- Save boundary: Uses the current Run; resizing and selection do not mutate the save.
- Automated evidence: Board AABB fitting and screen/grid round trips are asserted; visual framing and pointer feel remain manual.
- User verdict: Passed in the latest user report.

### MQA-GODOT-PROGRESSION — Growth priority and three-card UI

- Status: `passed`
- Source: Phase 7B–8E selected-starting-branch and class-attribute fixes
- Action: Historical growth priority, requirements, card details and current skills acceptance.
- Expected: Three unique candidates with correct branch priority and class attribute requirements.
- Observe: Progression attribute and skill pages.
- Preserve on failure: Screenshot, character, starting skill, attributes and all cards.
- Save boundary: Drafts remain transient until final confirmation.
- Automated evidence: Ordering, uniqueness, metadata, branches and requirements remain covered.
- User verdict: Passed in the latest user report.

### MQA-GODOT-UNIT-MOTION-CONTACT — Unit motion, hit, defeat, and ground contact

- Status: `passed`
- Source: Phase 8 presentation parity fix after user screenshot feedback
- Reopen reason: Fourteen approved Mage/Necromancer/Amazon Cast/Hit/Melee/Thrown textures are now migrated and combined with the existing programmatic motion.
- Action: In one battle, move at least two cells; cast with Mage and Necromancer; use Amazon Thrust and Poison Spear; receive a nonlethal hit; defeat one unit; pause midway, then resume at 0.5× and 4×.
- Expected: Player Body switches to the correct directional action pose during the authored window and returns to idle at Release/Recovery; enemies/summons safely use programmatic fallback. Move sway, hit recoil, lethal collapse and corpse landing remain serial; Shadow and overlays do not inherit Body deformation; dead units show no status icons.
- Observe: Unit Body, feet, Shadow, status layer and HP/MP anchor during Move, Hit and Defeat.
- Preserve on failure: Short video or sequential screenshots, actor/unit, speed, cue type, and Godot Output.
- Save boundary: Uses the current battle checkpoint; presentation speed and pause do not alter gameplay state.
- Automated evidence: Hash-bound 14-texture converter, ResourceSaver references, directional pose/fallback tests, Body-only transforms, interrupt cleanup, serial cue order, death status cleanup, GdUnit, Compatibility and Forward+ are asserted; motion feel and contact spacing remain manual.
- User verdict: Passed in the latest user report.

### MQA-GODOT-UNIT-VISUAL — Unit Gallery and Spawn framing baseline

- Status: `passed`
- Source: Phase 4 closure
- Action: Historical Gallery/Spawn direction, tint, scale, and edge framing acceptance.
- Expected: Historical acceptance retained unless unit presentation/layout changes reopen it.
- Observe: Unit Gallery and SpawnFixture.
- Preserve on failure: Screenshot and Output.
- Save boundary: No save mutation.
- Automated evidence: Unit resources, bounds, directions, shader, and screenshots remain in regression coverage.
- User verdict: User explicitly accepted the Phase 4 visual baseline.

## Deferred or Blocked

No current items.

## Last Emitted Order

1. `MQA-GODOT-HUD-SKILL-PRESENTATION`
2. `MQA-GODOT-PAUSE-MENU`
3. `MQA-GODOT-FULL-RUN`
4. `MQA-GODOT-DAMAGE-NUMBERS`
5. `MQA-GODOT-PROGRESSION-ATOMIC`
6. `MQA-GODOT-INVENTORY`
7. `MQA-GODOT-RELOAD-OUTPUT`
