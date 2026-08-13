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
- Action: Produce normal damage, critical, Miss, healing, and MP recovery while trying Pause and all speed values.
- Expected: Numbers appear above the affected unit at the committed impact, use the correct color/text, pause with presentation, and leave no residue.
- Observe: Unit head anchor, animation timing, HUD speed, and Godot Output.
- Preserve on failure: Screenshot/video plus event log and speed/pause state.
- Save boundary: Ordinary battle state changes apply; use a disposable Run if exact replay matters.
- Automated evidence: Event-to-number identity, multi-hit sequence, pause/speed propagation, and cleanup are asserted; readability and timing remain manual.
- User verdict: none.

### MQA-GODOT-PROGRESSION — Growth priority and three-card UI

- Status: `pending`
- Source: Phase 7B–8E progression parity fixes
- Action: Complete a victory progression, allocate one of six attributes, and inspect the three skill cards and current-skill section.
- Expected: Exactly three unique Learn/Upgrade candidates appear; current skills show names, levels, descriptions, type and MP; starting advanced and legal Lv2 priority match Unity.
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
- Action: Check vertical Lightning, overlapping alpha-aware selection, Poison Spear held/unarmed/recovery, HUD controls, and Backquote CheatConsole.
- Expected: Lightning strikes from above; click/hover select the visible intended actor; Amazon changes spear sprite from committed state; Console does not steal or leak intents.
- Observe: Battle actors, HUD, hover meter/detail, CheatConsole, and Output.
- Preserve on failure: Screenshot/video, selected actor ID/tile, skill event log, and current encounter.
- Save boundary: Skills mutate the battle; use a replayable encounter checkpoint.
- Automated evidence: Event-derived targets, alpha hit ordering, spear state projection, console guards, and renderer smoke are asserted; visual parity remains manual.
- User verdict: none after latest fixes.

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
8. `MQA-GODOT-FULL-RUN`
9. `MQA-GODOT-RELOAD-OUTPUT`
