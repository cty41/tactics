# Tactics Manual Acceptance Ledger

This is the current cross-project manual acceptance state. Stable IDs are authoritative; list numbers are temporary.

## Pending

### MQA-GODOT-DAMAGE-NUMBERS — Floating combat feedback

- Status: `pending`
- Source: Phase 8E camera/menu/damage/growth transaction
- Reopen reason: Poison tick now has an explicit committed Impact cue instead of falling through at frame completion.
- Action: Visually sample Miss, healing, and MP recovery once at a comfortable speed; normal, Poison and critical feedback no longer need repeating.
- Expected: Healing shows green `+N`, MP recovery blue `+N MP`, and Miss gray; each appears above the affected unit at its committed impact, pauses with presentation, and leaves no residue.
- Observe: Unit head anchor, animation timing, HUD speed, and Godot Output.
- Preserve on failure: Screenshot/video plus event log and speed/pause state.
- Save boundary: Ordinary battle state changes apply; use a disposable Run if exact replay matters.
- Automated evidence: Gameplay Specs now execute deterministic Miss and Mana recovery through Main.tscn and assert event identity, color node, pause/speed behavior, cleanup and production-save isolation. Healing event mapping is covered at the Application/presentation layer; only readability and animation feel remain manual.
- User verdict: Partial pass: normal damage, Poison tick and gold critical numbers were observed; Miss, healing and MP recovery remain unconfirmed.

### MQA-GODOT-RELOAD-OUTPUT — Reload and diagnostics cleanup

- Status: `pending`
- Source: Phase 7B–8E combined acceptance
- Action: Trigger one real Godot C# Assembly Reload, Continue the active Run, replay one battle action, and inspect logs.
- Expected: No stale Tween, temporary node, duplicate signal, input lock, corrupted save, or Unicode/NUL error.
- Observe: Godot Output and CheatConsole; verify current page and actor state visually.
- Preserve on failure: Full Output excerpt, page/encounter, save copy, and reproduction order.
- Save boundary: Reload may restart the current battle from its committed checkpoint; retain save evidence on failure.
- Automated evidence: Gameplay Specs restart the real Main scene/process, Continue a PendingBattle, verify normalized state, input and zero temporary presentation nodes, and prove the production save unchanged. Only the real Editor Assembly Reload lifecycle remains manual.
- User verdict: none after latest lifecycle-affecting changes.

### MQA-GODOT-DEFEAT-FLOW — Party defeat terminal flow

- Status: `pending`
- Source: Phase 7B–8E summon AI and defeat-flow repair
- Reopen reason: The prior Elite run stalled after the visible party died; friendly summon ownership and terminal submission now share the automatic controller path.
- Action: In one disposable encounter, let the final player-faction entity die and observe the visible transition into Defeated Summary and Home.
- Expected: The last defeat presentation reads naturally, the Summary appears once, and Return Home has no visible hitch or duplicate page.
- Observe: Battle phase, Turn Order, presentation queue, terminal summary, Home, CheatConsole, and Godot Output.
- Preserve on failure: Keep the battle open; record surviving units including summons, current actor, playback pause state, event log, Run seed/revision, and Output.
- Save boundary: The current Elite checkpoint is valuable diagnostic evidence; do not overwrite or abandon it before copying the save.
- Automated evidence: Gameplay Specs and Application tests cover with/without summon survival, automatic summon turns, one-shot BattleResult, presentation drain, Defeated Summary, Return Home cleanup and production-save isolation. Only visual transition feel remains manual.
- User verdict: Failed before this repair: after the whole visible party died in the first Elite battle, the battle remained stuck instead of showing defeat and returning Home.

### MQA-GODOT-INVENTORY — Backpack and loadout workflow

- Status: `pending`
- Source: Phase 7B–8E Inventory projector and navigation parity repair
- Reopen reason: Inventory now consumes Application-owned base/bonus/total/derived projections and is reachable only from Rogue Map.
- Action: From the Rogue Map, inspect one purchased Equipment item, equip it, and judge whether the base/bonus/total and slot presentation are easy to understand.
- Expected: The selected item, affected slot and positive/negative deltas are readable without guessing; Inventory remains a Map-only workflow.
- Observe: Inventory item detail, equipment slots, character stats/derived stats, Rogue Map entry, Home menu, and Output.
- Preserve on failure: Screenshot, item definition/instance ID, character before/after values, source route, save copy, and Output.
- Save boundary: Purchases and loadout operations persist; keep a copy of the Store save.
- Automated evidence: Gameplay Specs now equip through the production Main UI, enter a real battle, compare BattleUnitState against Application base/bonus/total and derived projection, Reload, and prove instance uniqueness plus production-save isolation. Only UI readability and interaction feel remain manual.
- User verdict: Partial pass: Inventory operation and equipment detail UI are OK; the resulting battle-stat increase has not yet been verified in combat.

## Passed

### MQA-GODOT-FULL-RUN — Complete Run shell and route recovery

- Status: `passed`
- Source: Phase 8E Boss settlement transaction revision fix
- Reopen reason: The second live replay proved terminal detection and presentation drain completed; the actual failure was `save.non_increasing_revision` after terminal transition reused revision 148. The transition and settlement coordinator are now fixed and reviewed.
- Action: Continue the preserved Boss checkpoint, defeat the final enemy, inspect the terminal settlement lines, then use Return Home.
- Expected: The final animation completes, settlement advances from Submitting to Saved and NavigationCompleted, BossVictory Summary appears once, and Return Home ends the Run.
- Observe: Battle page, CheatConsole `BattleSettlementDiagnostic`, BossVictory Summary, Home Continue state, and Godot Output.
- Preserve on failure: Keep the page open; copy all CheatConsole logs, retain the production save and backup, and record the last settlement stage/error.
- Save boundary: This replay commits the terminal Summary and ends the Run.
- Automated evidence: Terminal revision increases through ApplyFullRunTransition and a controlled store; ActiveRun becomes null, Summary is one-shot, failed readback recovery and duplicate callbacks are guarded.
- User verdict: Passed in the latest report.

### MQA-GODOT-CHEAT-CONSOLE-COPY — Read-only battle log copying

- Status: `passed`
- Source: Phase 8E Boss settlement diagnostics and CheatConsole copy
- Action: Open CheatConsole, drag-select several lines and copy with Ctrl+C and the right-click menu; then test Copy Visible under a filter and Copy All.
- Expected: Selected text copies normally; Copy Visible contains only rendered filtered lines, Copy All contains all retained lines, and no console interaction triggers battle commands.
- Observe: CheatConsole selection/context menu/status text and an external text editor used only for paste verification.
- Preserve on failure: Screenshot, selected filter, copied text, and any Godot Output error.
- Save boundary: Console copying does not mutate the Run or save.
- Automated evidence: Selection mode, filtered/all rendering, injected clipboard behavior and gameplay input blocking are asserted.
- User verdict: Passed in the latest report.

### MQA-GODOT-MYSTERY-ADJUDICATION — Fixed-option deterministic event adjudicator

- Status: `passed`
- Source: Phase 7B–8E Mystery adjudicator redesign
- Action: Enter a Mystery node, note the assigned party member and all fixed options, Reload before choosing, then Continue and resolve one option.
- Expected: The assigned member, options, rates and eventual result remain deterministic across Reload.
- Observe: Mystery page, result page, Continue and Output.
- Preserve on failure: Run seed, event ID, assigned character, options/rates and save copy.
- Save boundary: Resolving an option mutates the Run.
- Automated evidence: Deterministic assignment, option attributes, roll and Save V5 round-trip are asserted.
- User verdict: Passed in the latest report.

### MQA-GODOT-SUMMON-CONTROL — Unity summon AI ownership parity

- Status: `passed`
- Source: Phase 7B–8E frozen summon AI and controller repair
- Reopen reason: Skeleton Warrior, Skeleton Mage and Fire Demon now resolve internal AI/loadout by Unit ContentId; Decoy is explicitly non-acting.
- Action: Summon Skeleton Warrior, Skeleton Mage and Fire Demon, then advance to each summon turn.
- Expected: AI-authored summons act automatically; Decoy remains non-attacking.
- Observe: Turn Order, input lock, CheatConsole AI log and unit actions.
- Preserve on failure: Summon type/level, current actor, AI log and Output.
- Save boundary: Ordinary battle mutation.
- Automated evidence: Internal resources, loadouts, automatic turns, input rejection and Decoy skip are asserted.
- User verdict: Passed with no anomaly in the latest report.

### MQA-GODOT-PROGRESSION-ATOMIC — Non-skippable atomic growth

- Status: `passed`
- Source: Phase 8E camera/menu/damage/growth transaction
- Action: Allocate an attribute, close before choosing a skill, Continue, then finish progression once.
- Expected: Continue restarts at attribute allocation; final confirmation applies once and unlocks the next node once.
- Observe: Progression pages, map node state and Home Continue.
- Preserve on failure: Save copy, screenshots and Output.
- Save boundary: PendingProgression persists; unfinished UI drafts are not written.
- Automated evidence: Revision, V5 normalization, one-shot transaction and unlock semantics are asserted.
- User verdict: Passed with no anomaly in the latest report.

### MQA-GODOT-AVAILABILITY-TURNS-LOS — Skill availability, defeated turns, and LOS

- Status: `passed`
- Source: Phase 7B–8E battle availability and LOS parity repair
- Reopen reason: Move now uses the same authoritative availability boundary, and LOS hover exposes the first structured blocker instead of only a generic rejection.
- Action: Move once, exhaust Mana, kill a party member, then test a ranged skill with a living unit between caster and target; repeat after moving the blocker and with a corpse or dropped spear in between.
- Expected: Move and unavailable skills are disabled with a reason and cannot enter targeting; defeated units are skipped; living units block ordinary ranged LOS while corpses/dropped spears do not.
- Observe: Action buttons, Turn Order, target highlights, Hover reason and CheatConsole.
- Preserve on failure: Actor/target/blocker cells, skill ID, event log and Output.
- Save boundary: Ordinary battle mutation.
- Automated evidence: Availability, dead-turn skipping, structured blockers and shared Preview/AI/Transition probes remain asserted.
- User verdict: Passed in the latest report.

### MQA-GODOT-TURN-PACING — Playable enemy initiative pacing

- Status: `passed`
- Source: Phase 7B–8E playable enemy speed profile
- Action: Observe the first two rounds of a normal encounter.
- Expected: Enemy archetypes retain relative differences without the entire enemy team consistently preceding all player characters.
- Observe: Turn Order and CheatConsole.
- Preserve on failure: Encounter ID, Turn Order and actor sequence.
- Save boundary: Ordinary battle mutation.
- Automated evidence: Speed profile generation, player preservation and derived-stat recomputation remain asserted.
- User verdict: Passed in the latest report.

### MQA-GODOT-L4-RECOVERY — Layer 4 legacy-save recovery

- Status: `passed`
- Source: Phase 7B–8E L4 map invariant repair
- Reopen reason: The live V5 save reached `AwaitingLayerFourChoice` with three completed battles but no MapState, leaving all Layer 4 routes locked.
- Action: Continue the preserved current Run and enter Layer 4.
- Expected: Layer 4 routes are reachable and the selected route advances to the first Elite battle.
- Observe: Rogue Map, selected route, Continue, and Godot Output.
- Preserve on failure: Save/backup, Run seed/revision, map screenshot, and Output.
- Save boundary: Uses the current persistent Run.
- Automated evidence: Legacy V5 repair, deterministic projection and invariant checks remain covered.
- User verdict: Passed; the Run has advanced to the first Elite encounter.

### MQA-GODOT-FIRE-DEMON-COMBAT — Dedicated Fire Demon attack

- Status: `passed`
- Source: Phase 7B–8E frozen Fire Demon attack migration
- Action: Summon a Fire Demon and use its dedicated attack.
- Expected: The attack targets correctly and follows its frozen damage, Ignite, range and per-turn contract.
- Observe: Fire Demon action, target HP/status, CheatConsole, and Output.
- Preserve on failure: Cells, HP before/after, log and current turn.
- Save boundary: Ordinary battle mutation.
- Automated evidence: Frozen contract, Resource identity, damage/status/no-crit and use limit remain covered.
- User verdict: Passed in the latest report.

### MQA-GODOT-HUD-SKILL-PRESENTATION — HUD and committed skill visuals

- Status: `passed`
- Source: Phase 8E compact action bar and committed-presentation highlight fix
- Reopen reason: Action buttons now use display names and compact second-line costs; active-tile highlighting is suppressed during committed action playback.
- Action: Check the compact action bar, vertical Lightning, overlapping alpha-aware selection, Poison Spear held/unarmed/drop/Pickup or Recover, HUD controls, and Backquote CheatConsole.
- Expected: Basic attacks have no MP line; skill IDs have no `skill.` prefix; positive MP cost is on line two; during action animation the acting unit has no selected tile. Remaining skill/HUD visuals follow their committed state.
- Observe: Action buttons, actor feet/tile, battle actors, HUD, hover detail, CheatConsole, and Output.
- Preserve on failure: Screenshot/video, selected actor ID/tile, button text, skill event log, and current encounter.
- Save boundary: Skills mutate the battle; use a replayable encounter checkpoint.
- Automated evidence: Label formatting, action-state marker suppression, event-derived targets, alpha hit ordering, spear lifecycle, console guards, and renderer smoke are asserted; sizing and visual parity remain manual.
- User verdict: Passed in the latest user report.

### MQA-GODOT-PAUSE-MENU — Esc pause and safe exit flow

- Status: `passed`
- Source: Phase 8E pause overlay hierarchy and menu scope fix
- Reopen reason: The reported overlay rendered below actors and exposed a noncanonical Save and Quit action; the fix raises the overlay and removes that action.
- Action: During a battle press Esc, exercise Continue, Options/Back and Main Menu, then open Esc again while targeting and while CheatConsole is visible.
- Expected: The dark overlay and menu cover all actors/HUD; only Continue, Options and Main Menu appear; targeting/Console take Esc precedence; Quit remains only on Home.
- Observe: Pause overlay, battle HUD, Home menu, playback state, and CheatConsole.
- Preserve on failure: Screenshot and exact Esc/menu action sequence; keep the Run and Output open.
- Save boundary: Main Menu preserves the Active Run and Continue restarts from the committed pre-battle checkpoint.
- Automated evidence: Z-order, menu actions, input blocking, pause ownership and absence of Save and Quit are asserted; visual stacking and interaction feel remain manual.
- User verdict: Passed in the latest user report.

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

1. `MQA-GODOT-INVENTORY`
2. `MQA-GODOT-DEFEAT-FLOW`
3. `MQA-GODOT-DAMAGE-NUMBERS`
4. `MQA-GODOT-RELOAD-OUTPUT`
