# Tactics Manual Acceptance Ledger

This is the current cross-project manual acceptance state. Stable IDs are authoritative; list numbers are temporary.

## Pending

### MQA-GODOT-BOARD-FIT — Isometric board framing and input

- Status: `pending`
- Source: Phase 8E camera/menu/damage/growth transaction
- Action: Open a battle, resize across 16:9, 16:10, and 21:9, then hover and click edge and overlapping units.
- Expected: The complete 10×10 board is centered without the old right-side reserve; HUD stays clear and pointer selection remains accurate.
- Observe: Battle board, HUD bounds, hover details, and selected tile/unit.
- Preserve on failure: Screenshot with resolution, selected coordinate, and current encounter.
- Save boundary: Uses the current Run; resizing and selection do not mutate the save.
- Automated evidence: Board AABB fitting and screen/grid round trips are asserted; visual framing and pointer feel remain manual.
- User verdict: none.

### MQA-GODOT-PAUSE-MENU — Esc pause and safe exit flow

- Status: `pending`
- Source: Phase 8E camera/menu/damage/growth transaction
- Action: Exercise targeting cancel, Esc menu, Options/Back, Continue, Main Menu, Save and Quit, plus CheatConsole precedence.
- Expected: No Abandon button; menus do not leak input, pause ownership is restored, and the Run resumes from its committed checkpoint.
- Observe: Pause overlay, HUD playback controls, Home Continue state, and CheatConsole.
- Preserve on failure: Screenshot and the exact action sequence; keep the Run and Output open.
- Save boundary: Main Menu and Save and Quit preserve Active Run; battle resumes from the pre-battle checkpoint.
- Automated evidence: Menu priority, intent blocking, and pause ownership are asserted; interaction feel and navigation remain manual.
- User verdict: none.

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

### MQA-GODOT-PROGRESSION — Growth priority and three-card UI

- Status: `pending`
- Source: Phase 7B–8E selected-starting-branch and class-attribute fixes
- Reopen reason: Growth guarantee now follows the player's actual New Run starting skill; Mage uses Intelligence and Necromancer uses Charisma, including Bone Spear.
- Action: Complete a victory progression, allocate one of six attributes, and inspect the three skill cards and current-skill section.
- Expected: Exactly three unique Learn/Upgrade candidates appear; current skills show names, levels, descriptions, type and MP; the advanced guarantee follows the chosen starting branch. Mage requirements are Intelligence and Necromancer requirements are Charisma.
- Observe: Attribute page and Skill Selection page.
- Preserve on failure: Screenshot, character, starting skill, attributes, Run seed, and all three cards.
- Save boundary: Attribute and skill choices remain drafts until final confirmation.
- Automated evidence: Fixed-seed ordering, uniqueness, prerequisites, guarantee suppression, and metadata are asserted; card readability and flow remain manual.
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
- Source: Phase 8C–8E presentation and HUD fixes
- Reopen reason: Dropped Spear now has a persistent programmatic board marker and poison feedback has a dedicated marker.
- Action: Check vertical Lightning, overlapping alpha-aware selection, Poison Spear held/unarmed/drop/Pickup or Recover, HUD controls, and Backquote CheatConsole.
- Expected: Lightning strikes from above; click/hover select the visible intended actor; Amazon changes spear sprite from committed state; the dropped tile shows a spear until recovery; Console does not steal or leak intents.
- Observe: Battle actors, HUD, hover meter/detail, CheatConsole, and Output.
- Preserve on failure: Screenshot/video, selected actor ID/tile, skill event log, and current encounter.
- Save boundary: Skills mutate the battle; use a replayable encounter checkpoint.
- Automated evidence: Event-derived targets, alpha hit ordering, spear state projection/marker lifecycle, console guards, and renderer smoke are asserted; visual parity remains manual.
- User verdict: none after latest fixes.

### MQA-GODOT-UNIT-MOTION-CONTACT — Unit motion, hit, defeat, and ground contact

- Status: `pending`
- Source: Phase 8 presentation parity fix after user screenshot feedback
- Reopen reason: Fourteen approved Mage/Necromancer/Amazon Cast/Hit/Melee/Thrown textures are now migrated and combined with the existing programmatic motion.
- Action: In one battle, move at least two cells; cast with Mage and Necromancer; use Amazon Thrust and Poison Spear; receive a nonlethal hit; defeat one unit; pause midway, then resume at 0.5× and 4×.
- Expected: Player Body switches to the correct directional action pose during the authored window and returns to idle at Release/Recovery; enemies/summons safely use programmatic fallback. Move sway, hit recoil, lethal collapse and corpse landing remain serial; Shadow and overlays do not inherit Body deformation; dead units show no status icons.
- Observe: Unit Body, feet, Shadow, status layer and HP/MP anchor during Move, Hit and Defeat.
- Preserve on failure: Short video or sequential screenshots, actor/unit, speed, cue type, and Godot Output.
- Save boundary: Uses the current battle checkpoint; presentation speed and pause do not alter gameplay state.
- Automated evidence: Hash-bound 14-texture converter, ResourceSaver references, directional pose/fallback tests, Body-only transforms, interrupt cleanup, serial cue order, death status cleanup, GdUnit, Compatibility and Forward+ are asserted; motion feel and contact spacing remain manual.
- User verdict: User reported missing authored hit/action poses and status icons persisting on death before this fix.

### MQA-GODOT-FULL-RUN — Complete Run shell and route recovery

- Status: `pending`
- Source: Phase 7C–8E combined acceptance
- Action: Complete N1 through BossVictory, cover alternate Layer 4/6 routes in separate Runs, and test PendingBattle/Continue recovery.
- Expected: Settlement and mandatory growth route correctly, selected nodes resolve once, saves resume the correct phase, and the terminal result appears once.
- Observe: Roguelike map connections/status, Settlement, Progression, route pages, Home Continue, and Summary.
- Preserve on failure: Run seed, route, save and backup copies, screenshot, and Output.
- Save boundary: Use separate disposable Runs for mutually exclusive routes; never reuse the user's valuable save as a fixture.
- Automated evidence: Deterministic journey, route transactions, save recovery, and boss/defeat flows are asserted; end-to-end UX remains manual.
- User verdict: Earlier subsets were tested, but later routing/UI changes reopened the combined flow.

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

1. `MQA-GODOT-BOARD-FIT`
2. `MQA-GODOT-PAUSE-MENU`
3. `MQA-GODOT-DAMAGE-NUMBERS`
4. `MQA-GODOT-PROGRESSION`
5. `MQA-GODOT-PROGRESSION-ATOMIC`
6. `MQA-GODOT-INVENTORY`
7. `MQA-GODOT-HUD-SKILL-PRESENTATION`
8. `MQA-GODOT-UNIT-MOTION-CONTACT`
9. `MQA-GODOT-FULL-RUN`
10. `MQA-GODOT-RELOAD-OUTPUT`
