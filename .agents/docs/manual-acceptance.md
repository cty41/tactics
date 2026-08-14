# Tactics Manual Acceptance Ledger

This is the current cross-project manual acceptance state. Stable IDs are authoritative; list numbers are temporary.

## Pending

### MQA-GODOT-AVAILABILITY-TURNS-LOS — Skill availability, defeated turns, and LOS

- Status: `pending`
- Source: Phase 7B–8E battle availability and LOS parity repair
- Action: Exhaust Mana, kill a party member, then test a ranged skill with a living unit between caster and target; repeat after moving the blocker and with a corpse or dropped spear in between.
- Expected: Insufficient-Mana/used/precondition skills are disabled with a reason and cannot enter targeting; defeated units are skipped; living units block Fireball and other ordinary ranged LOS, while corpses/dropped spears do not. Bone Spear retains Unity first-enemy interception.
- Observe: Action buttons/tooltips, Turn Order/current actor, target highlights/Hover reason, CheatConsole, and Output.
- Preserve on failure: Short video, actor/target/blocker cells, skill ID, Turn Order, event log, and Output.
- Save boundary: Battle mutations apply; use a disposable or checkpointed encounter.
- Automated evidence: Availability snapshot/intent rejection, consecutive dead-unit wrap, supercover corner blockers, Preview/AI/Transition shared probes, Poison Spear and Bone Spear exceptions are asserted; interaction clarity remains manual.
- User verdict: Partial pass: skill availability and defeated-turn skipping are OK; living-unit LOS blocking has not yet been manually tested.

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
- User verdict: Partial pass: normal and Poison tick numbers were observed; critical has not yet appeared during manual play.

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

### MQA-GODOT-FULL-RUN — Complete Run shell and route recovery

- Status: `pending`
- Source: Phase 8E N3 authoritative resume fix
- Reopen reason: User reported that the third battle node first returned `run.not_ready`, then `save.starting_skill_invalid` after an actual starting skill had been upgraded to Lv2.
- Action: Complete N1 and its growth, complete N2 and its growth, then click N3; later continue through BossVictory and alternate Layer 4/6 routes in disposable Runs.
- Expected: N3 opens immediately when Ready; a PendingBattle resumes the same checkpoint and incomplete growth routes back to Progression instead of leaving a dead map node. Later route transactions resolve once and terminal summary appears once.
- Observe: Map node status/detail, N3 battle title, Progression, Settlement, Home Continue, and Summary.
- Preserve on failure: Run seed, node state/reason, save and backup copies, screenshot, and Output.
- Save boundary: Use separate disposable Runs for mutually exclusive routes; never reuse a valuable save as a fixture.
- Automated evidence: Two victories plus two completed growth transactions now assert Ready/index 2/N3 request; pending/resume and full deterministic journeys are also covered. End-to-end UX remains manual.
- User verdict: Failed before this fix: N3 rejected the live V5 save because Bone Spear Lv1 had legitimately advanced to Lv2.

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

### MQA-GODOT-DEFEAT-FLOW — Party defeat terminal flow

- Status: `pending`
- Source: Phase 7B–8E summon AI and defeat-flow repair
- Reopen reason: The prior Elite run stalled after the visible party died; friendly summon ownership and terminal submission now share the automatic controller path.
- Action: Let all persistent party characters reach zero HP, with and without a surviving summon.
- Expected: A true party defeat resolves once, shows the Defeated summary, and returns safely to Home; a surviving AI-owned summon must not leave an unplayable human turn.
- Observe: Battle phase, Turn Order, presentation queue, terminal summary, Home, CheatConsole, and Godot Output.
- Preserve on failure: Keep the battle open; record surviving units including summons, current actor, playback pause state, event log, Run seed/revision, and Output.
- Save boundary: The current Elite checkpoint is valuable diagnostic evidence; do not overwrite or abandon it before copying the save.
- Automated evidence: Friendly summon survival, all-player-faction defeat, one-shot BattleResult, late Elite/Boss defeat settlement, presentation drain and Summary routing are asserted; visual timing and Return Home interaction remain manual.
- User verdict: Failed before this repair: after the whole visible party died in the first Elite battle, the battle remained stuck instead of showing defeat and returning Home.

### MQA-GODOT-SUMMON-CONTROL — Unity summon AI ownership parity

- Status: `pending`
- Source: Phase 7B–8E frozen summon AI and controller repair
- Reopen reason: Skeleton Warrior, Skeleton Mage and Fire Demon now resolve internal AI/loadout by Unit ContentId; Decoy is explicitly non-acting.
- Action: Summon Skeleton Warrior, Skeleton Mage, and Fire Demon, then advance to each summon turn.
- Expected: Unity-authored summons carrying an AiBrain execute automatically even though they share the human PlayerNumber; Decoy remains a non-attacking special unit.
- Observe: Turn Order, action buttons/input lock, AI decision log in CheatConsole, and unit actions.
- Preserve on failure: Summon kind/level, current actor, available actions, AI log, and Output.
- Save boundary: Ordinary battle mutation; use a replayable checkpoint.
- Automated evidence: Frozen internal AI/skill resources, Lv1/Lv2 loadout selection, friendly automatic turns, input rejection, Decoy skip and deterministic events are asserted in Application/GdUnit and both renderers.
- User verdict: Parity gap was confirmed before this repair; current behavior awaits manual replay.

### MQA-GODOT-INVENTORY — Backpack and loadout workflow

- Status: `pending`
- Source: Phase 7B–8E Inventory projector and navigation parity repair
- Reopen reason: Inventory now consumes Application-owned base/bonus/total/derived projections and is reachable only from Rogue Map.
- Action: From the Rogue Map, inspect purchased Equipment details, equip it, compare base/total attributes and derived combat values, then Reload.
- Expected: Item bonuses and the equipped slot are visible; character totals change immediately and persist. Inventory is entered from the Run/Map flow, not Home.
- Observe: Inventory item detail, equipment slots, character stats/derived stats, Rogue Map entry, Home menu, and Output.
- Preserve on failure: Screenshot, item definition/instance ID, character before/after values, source route, save copy, and Output.
- Save boundary: Purchases and loadout operations persist; keep a copy of the Store save.
- Automated evidence: Base/bonus/total and derived projection, item details, Equip/Replace/Unequip identity, persistence, and the unique Map entry are asserted; readability and interaction remain manual.
- User verdict: Failed before this repair: purchase worked, but equipment properties/equipped gains were not adequately inspectable and Home exposed a noncanonical Inventory button.

## Passed

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

1. `MQA-GODOT-SUMMON-CONTROL`
2. `MQA-GODOT-DEFEAT-FLOW`
3. `MQA-GODOT-INVENTORY`
4. `MQA-GODOT-AVAILABILITY-TURNS-LOS`
5. `MQA-GODOT-FULL-RUN`
6. `MQA-GODOT-DAMAGE-NUMBERS`
7. `MQA-GODOT-PROGRESSION-ATOMIC`
8. `MQA-GODOT-RELOAD-OUTPUT`
