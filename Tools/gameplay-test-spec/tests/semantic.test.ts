import assert from "node:assert/strict";
import test from "node:test";
import { compileScenarioSpec } from "../src/compiler.js";
import { generateScenarioSpec } from "../src/generator.js";
import { validateScenarioSpec } from "../src/validator.js";

function expectValidSpec(text: string, expectedScenario: string): ReturnType<typeof compileScenarioSpec>["plan"] {
  const generated = generateScenarioSpec(text);
  assert.equal(generated.needsClarification, false, generated.diagnostics.map(d => d.message).join("\n"));
  assert.ok(generated.spec, "generator should return a spec");
  assert.equal(generated.spec.scenario, expectedScenario);

  const validation = validateScenarioSpec(generated.spec);
  assert.equal(validation.valid, true, validation.diagnostics.map(d => d.message).join("\n"));

  const compiled = compileScenarioSpec(generated.spec);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.ok(compiled.plan, "compiler should return a plan");
  return compiled.plan;
}

test("generates buff scenario from natural language", () => {
  const plan = expectValidSpec("给自己施加一个持续 2 回合的增益效果", "ApplySelfBuff");
  assert.equal(plan?.runtimeActions[0].kind, "executeSkillGraph");
  assert.ok(plan?.assertionPlans.some(assertion => assertion.kind === "unitHasBuff"));
  assert.ok(plan?.assertionPlans.some(assertion => assertion.kind === "unitBuffDurationEquals"));
});

test("generates area damage scenario from natural language", () => {
  const plan = expectValidSpec("范围伤害命中半径内的多个目标", "AreaDamageHitsAllTargetsInRadius");
  assert.equal(plan?.runtimeActions[0].kind, "executeSkillGraph");
  assert.ok(plan?.setupActions.some(action => action.kind === "createSkillGraph" && action.parameters.graphKind === "areaDamage"));
  assert.ok(plan?.assertionPlans.filter(assertion => assertion.kind === "unitHealthEquals").length >= 2);
});

test("generates knockback scenario from natural language", () => {
  const plan = expectValidSpec("击退目标并让它移动到下一格", "KnockbackMovesTargetAwayFromCaster");
  assert.equal(plan?.runtimeActions[0].kind, "executeSkillGraph");
  assert.ok(plan?.setupActions.some(action => action.kind === "createSkillGraph" && action.parameters.graphKind === "knockback"));
  assert.ok(plan?.assertionPlans.some(assertion => assertion.kind === "unitCellEquals"));
});

test("generates ally heal scenario from natural language", () => {
  const plan = expectValidSpec("治疗友军目标并恢复 4 点生命", "AllyHealRestoresFriendlyUnitHealth");
  assert.equal(plan?.runtimeActions[0].kind, "executeSkillGraph");
  assert.ok(plan?.setupActions.some(action => action.kind === "createSkillGraph" && action.parameters.graphKind === "allyHeal"));
  assert.ok(plan?.assertionPlans.some(assertion => assertion.kind === "unitHealthEquals"));
});

test("generates mark then damage scenario from natural language", () => {
  const plan = expectValidSpec("先给敌人挂上标记，再让下一次攻击必定暴击", "MarkedTargetTakesCriticalDamage");
  assert.equal(plan?.runtimeActions.length, 2);
  assert.ok(plan?.setupActions.some(action => action.kind === "createSkillGraph" && action.parameters.graphKind === "applyBuff"));
  assert.ok(plan?.assertionPlans.some(assertion => assertion.kind === "unitHasBuff"));
  assert.ok(plan?.assertionPlans.some(assertion => assertion.kind === "unitHealthEquals"));
});

test("generates counter then damage scenario from natural language", () => {
  const plan = expectValidSpec("让敌人获得反击状态，然后被近战攻击触发反击", "CounterBuffRetaliatesWhenDamaged");
  assert.equal(plan?.runtimeActions.length, 2);
  assert.ok(plan?.setupActions.some(action => action.kind === "createSkillGraph" && action.parameters.graphKind === "applyBuff"));
  assert.ok(plan?.assertionPlans.some(assertion => assertion.kind === "unitHealthEquals" && assertion.target === "caster"));
  assert.ok(plan?.assertionPlans.some(assertion => assertion.kind === "unitHealthEquals" && assertion.target === "target"));
});

test("generates charge scenario from natural language", () => {
  const plan = expectValidSpec("冲锋到目标并撞击造成伤害", "ChargeStrikeMovesAndDamagesTarget");
  assert.equal(plan?.runtimeActions[0].kind, "executeSkillGraph");
  assert.ok(plan?.setupActions.some(action => action.kind === "createSkillGraph" && action.parameters.graphKind === "charge"));
  assert.ok(plan?.assertionPlans.some(assertion => assertion.kind === "unitCellEquals"));
  assert.ok(plan?.assertionPlans.some(assertion => assertion.kind === "unitHealthEquals"));
});

test("rejects specs that reference missing aliases", () => {
  const validation = validateScenarioSpec({
    feature: "SkillGraph",
    scenario: "BrokenAliasReference",
    tags: ["mvp", "skill"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      { kind: "createSkillAbilityConfig", parameters: { alias: "ability", graphAlias: "missingGraph", manaCost: 1, targetRange: 1 } }
    ],
    actions: [
      { kind: "executeSkillGraph", parameters: { graphAlias: "missingGraph", casterAlias: "caster" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Completed", parameters: {} }
    ]
  });

  assert.equal(validation.valid, false);
  assert.ok(validation.diagnostics.some(diagnostic => diagnostic.code === "UnknownGraphAlias"));
});

test("rejects buff assertions with malformed expected values", () => {
  const validation = validateScenarioSpec({
    feature: "SkillGraph",
    scenario: "MalformedBuffAssertion",
    tags: ["mvp", "skill"],
    requiredAdapters: ["Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "createSkillTestWorld", parameters: {} },
      { kind: "createSkillGraph", parameters: { alias: "graph", graphKind: "applyBuff", selectionKind: "self", buffName: "Might", buffEffectType: "None", triggerTiming: "None", duration: 2 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, health: 10, maxHealth: 10 } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } }
    ],
    actions: [
      { kind: "executeSkillGraph", parameters: { graphAlias: "graph", casterAlias: "caster" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Completed", parameters: {} },
      { kind: "unitBuffDurationEquals", target: "caster", expected: "two", parameters: { buffName: "Might" } }
    ]
  });

  assert.equal(validation.valid, false);
  assert.ok(validation.diagnostics.some(diagnostic => diagnostic.code === "InvalidAssertionExpectedType"));
});

// UI assertion negative tests

test("rejects elementVisible without target", () => {
  const validation = validateScenarioSpec({
    feature: "UI",
    scenario: "ElementVisibleNoTarget",
    tags: ["ui"],
    requiredAdapters: ["UI"],
    timeoutMs: 10000,
    setup: [],
    actions: [{ kind: "openUI", parameters: { uiId: "Home" } }],
    assertions: [{ kind: "elementVisible", expected: true, parameters: {} }]
  });

  assert.equal(validation.valid, false);
  assert.ok(validation.diagnostics.some(d => d.code === "MissingElementTarget"));
});

test("rejects elementVisible without expected", () => {
  const validation = validateScenarioSpec({
    feature: "UI",
    scenario: "ElementVisibleNoExpected",
    tags: ["ui"],
    requiredAdapters: ["UI"],
    timeoutMs: 10000,
    setup: [],
    actions: [{ kind: "openUI", parameters: { uiId: "Home" } }],
    assertions: [{ kind: "elementVisible", target: "MyButton", parameters: {} }]
  });

  assert.equal(validation.valid, false);
  assert.ok(validation.diagnostics.some(d => d.code === "InvalidAssertionExpectedType"));
});

test("rejects elementText without target", () => {
  const validation = validateScenarioSpec({
    feature: "UI",
    scenario: "ElementTextNoTarget",
    tags: ["ui"],
    requiredAdapters: ["UI"],
    timeoutMs: 10000,
    setup: [],
    actions: [{ kind: "openUI", parameters: { uiId: "Home" } }],
    assertions: [{ kind: "elementText", expected: "Hello", parameters: {} }]
  });

  assert.equal(validation.valid, false);
  assert.ok(validation.diagnostics.some(d => d.code === "MissingElementTarget"));
});

test("rejects elementText with non-string expected", () => {
  const validation = validateScenarioSpec({
    feature: "UI",
    scenario: "ElementTextBadExpected",
    tags: ["ui"],
    requiredAdapters: ["UI"],
    timeoutMs: 10000,
    setup: [],
    actions: [{ kind: "openUI", parameters: { uiId: "Home" } }],
    assertions: [{ kind: "elementText", target: "MyLabel", expected: 123, parameters: {} }]
  });

  assert.equal(validation.valid, false);
  assert.ok(validation.diagnostics.some(d => d.code === "InvalidAssertionExpectedType"));
});

test("rejects openUI without uiId", () => {
  const validation = validateScenarioSpec({
    feature: "UI",
    scenario: "OpenUINoUiId",
    tags: ["ui"],
    requiredAdapters: ["UI"],
    timeoutMs: 10000,
    setup: [],
    actions: [{ kind: "openUI", parameters: {} }],
    assertions: [{ kind: "elementVisible", target: "MyButton", expected: true, parameters: {} }]
  });

  assert.equal(validation.valid, false);
  assert.ok(validation.diagnostics.some(d => d.code === "MissingUiId"));
});

test("rejects clickElement without elementName", () => {
  const validation = validateScenarioSpec({
    feature: "UI",
    scenario: "ClickElementNoName",
    tags: ["ui"],
    requiredAdapters: ["UI"],
    timeoutMs: 10000,
    setup: [],
    actions: [{ kind: "clickElement", parameters: {} }],
    assertions: [{ kind: "elementVisible", target: "MyButton", expected: true, parameters: {} }]
  });

  assert.equal(validation.valid, false);
  assert.ok(validation.diagnostics.some(d => d.code === "MissingElementName"));
});

test("allows real SkillGraph assets inside the lightweight deterministic world", () => {
  const validation = validateScenarioSpec({
    feature: "MageSkillLevels",
    scenario: "RealGraphInLightweightWorld",
    tags: ["mage", "assets"],
    requiredAdapters: ["Battle", "Skill"],
    timeoutMs: 10000,
    setup: [
      { kind: "useRealAssets", parameters: {} },
      { kind: "createSkillTestWorld", parameters: {} },
      { kind: "loadSkillGraphAsset", parameters: { alias: "graph", assetPath: "Assets/Graph.asset" } },
      { kind: "createCell", parameters: { alias: "casterCell", x: 0, y: 0 } },
      { kind: "createCell", parameters: { alias: "targetCell", x: 1, y: 0 } },
      { kind: "createUnit", parameters: { alias: "caster", playerNumber: 0, cellAlias: "casterCell" } },
      { kind: "createUnit", parameters: { alias: "target", playerNumber: 1, cellAlias: "targetCell" } },
      { kind: "setTurnContext", parameters: { currentPlayerNumber: 0, playableUnitAliases: ["caster"] } }
    ],
    actions: [
      { kind: "executeSkillGraph", parameters: { graphAlias: "graph", casterAlias: "caster", targetPointAlias: "targetCell" } }
    ],
    assertions: [
      { kind: "executionStateEquals", expected: "Completed", parameters: {} }
    ]
  });

  assert.equal(validation.valid, true, validation.diagnostics.map(diagnostic => diagnostic.message).join("\n"));
});

test("rejects pressKey without key", () => {
  const validation = validateScenarioSpec({
    feature: "UI",
    scenario: "PressKeyWithoutKey",
    tags: ["ui"],
    requiredAdapters: ["UI"],
    timeoutMs: 10000,
    setup: [],
    actions: [{ kind: "pressKey", parameters: {} }],
    assertions: [{ kind: "elementExists", target: "Root", expected: true, parameters: {} }]
  });

  assert.equal(validation.valid, false);
  assert.ok(validation.diagnostics.some(d => d.code === "MissingActionParameter"));
});

test("rejects player-input-e2e setup and runtime shortcuts", () => {
  const validation = validateScenarioSpec({
    feature: "PlayerInput",
    scenario: "ShortcutRejected",
    tags: ["player-input-e2e"],
    requiredAdapters: ["PlayerInput", "UI", "Map"],
    timeoutMs: 10000,
    setup: [{ kind: "loadPureRunMap", adapter: "Map", parameters: { mapConfigPath: "Assets/Map.asset" } }],
    actions: [{ kind: "clickElement", adapter: "UI", parameters: { elementName: "NewGameButton" } }],
    assertions: [{ kind: "elementExists", adapter: "UI", target: "NewGameButton", expected: true, parameters: {} }]
  });

  assert.equal(validation.valid, false);
  assert.ok(validation.diagnostics.some(d => d.code === "PlayerInputE2ESetupShortcut"));
  assert.ok(validation.diagnostics.some(d => d.code === "PlayerInputE2EActionShortcut"));
});

test("validates player input semantic target and observable contracts", () => {
  const validation = validateScenarioSpec({
    feature: "PlayerInput",
    scenario: "InvalidSemanticTargets",
    tags: ["player-input-e2e"],
    requiredAdapters: ["PlayerInput", "UI"],
    timeoutMs: 10000,
    setup: [{ kind: "initializePlayerInput", parameters: {} }],
    actions: [
      { kind: "clickPointerTarget", parameters: { targetKind: "Unknown" } },
      { kind: "waitForPlayerObservable", parameters: { observable: "unknown" } },
      { kind: "playBattleThroughInput", parameters: { maximumActions: 101 } }
    ],
    assertions: [{ kind: "elementExists", adapter: "UI", target: "Root", expected: true, parameters: {} }]
  });

  assert.equal(validation.valid, false);
  assert.ok(validation.diagnostics.some(d => d.code === "InvalidPlayerInputTargetKind"));
  assert.ok(validation.diagnostics.some(d => d.code === "InvalidPlayerObservable"));
  assert.ok(validation.diagnostics.some(d => d.code === "InvalidMaximumPlayerActions"));
});

test("allows production battle lifecycle observables for player input", () => {
  const validation = validateScenarioSpec({
    feature: "PlayerInput",
    scenario: "BattleLifecycleObservable",
    tags: ["player-input-e2e"],
    requiredAdapters: ["PlayerInput", "UI"],
    timeoutMs: 10000,
    setup: [{ kind: "initializePlayerInput", adapter: "PlayerInput", parameters: {} }],
    actions: [
      { kind: "waitForPlayerObservable", adapter: "PlayerInput", parameters: { observable: "battleReady" } },
      { kind: "waitForPlayerObservable", adapter: "PlayerInput", parameters: { observable: "humanTurn" } },
      { kind: "waitForPlayerObservable", adapter: "PlayerInput", parameters: { observable: "battleEnded" } }
    ],
    assertions: [{ kind: "elementExists", adapter: "UI", target: "EndTurnButton", expected: true, parameters: {} }]
  });
  assert.equal(validation.valid, true, JSON.stringify(validation.diagnostics));
});

test("allows Godot adventure pointer targets, lifecycle observables, and typed assertions", () => {
  const spec = {
    feature: "AdventureBoard",
    scenario: "MoveLeaderAndResolveInteraction",
    tags: ["player-input-e2e", "godot", "adventure-board"],
    requiredAdapters: ["PlayerInput", "Map"],
    timeoutMs: 30000,
    setup: [{ kind: "initializePlayerInput", adapter: "PlayerInput", parameters: {} }],
    actions: [
      { kind: "waitForPlayerObservable", adapter: "PlayerInput", parameters: { observable: "adventureBoardReady" } },
      { kind: "clickPointerTarget", adapter: "PlayerInput", target: "4,4", parameters: { targetKind: "AdventureCell" } },
      { kind: "clickPointerTarget", adapter: "PlayerInput", target: "party-mage", parameters: { targetKind: "AdventureActor" } },
      { kind: "clickPointerTarget", adapter: "PlayerInput", target: "campfire", parameters: { targetKind: "AdventureObject" } },
      { kind: "clickPointerTarget", adapter: "PlayerInput", target: "layer04-rest", parameters: { targetKind: "RouteNode" } },
      { kind: "waitForPlayerObservable", adapter: "PlayerInput", parameters: { observable: "adventureInteractionResolved" } }
    ],
    assertions: [
      { kind: "adventureActorCellEquals", adapter: "Map", target: "party-mage", expected: "4,4", parameters: {} },
      { kind: "activeAdventureLeaderEquals", adapter: "Map", expected: "party-mage", parameters: {} },
      { kind: "runNodeLifecycleEquals", adapter: "Map", target: "layer04-rest", expected: "Resolved", parameters: {} },
      { kind: "immediateSuccessorNodeIdsEqual", adapter: "Map", expected: ["layer04-rest", "layer04-store", "layer04-event"], parameters: {} },
      { kind: "adventureObjectStateEquals", adapter: "Map", target: "campfire", expected: "Resolved", parameters: {} },
      { kind: "storeOfferCountEquals", adapter: "Map", expected: 3, parameters: {} },
      { kind: "storeSoldOfferCountEquals", adapter: "Map", expected: 0, parameters: {} },
      { kind: "backpackContainsContentId", adapter: "Map", target: "item.consumable.life-potion", expected: true, parameters: {} },
      { kind: "eventResolutionEquals", adapter: "Map", expected: "Resolved", parameters: {} },
      { kind: "pendingBattleContextKindEquals", adapter: "Map", expected: "CursedChest", parameters: {} },
      { kind: "escortStateEquals", adapter: "Map", expected: "Active", parameters: {} },
      { kind: "protectedNpcAliveEquals", adapter: "Map", expected: true, parameters: {} },
      { kind: "runSaveSchemaVersionEquals", adapter: "Map", expected: 8, parameters: {} }
    ]
  };

  const validation = validateScenarioSpec(spec);
  assert.equal(validation.valid, true, JSON.stringify(validation.diagnostics));
  const compiled = compileScenarioSpec(spec, { runtime: "Godot" });
  assert.equal(compiled.valid, true, JSON.stringify(compiled.diagnostics));
  assert.equal(compiled.plan?.schemaVersion, 3);
});

test("rejects shared sequence assertion with non-string-array expected value", () => {
  const validation = validateScenarioSpec({
    feature: "Battle",
    scenario: "InvalidCurrentRoundOrder",
    tags: ["battle"],
    requiredAdapters: ["Battle"],
    timeoutMs: 10000,
    setup: [{ kind: "bindBattleController", parameters: {} }],
    actions: [{ kind: "initializeInitiativeOrder", parameters: {} }],
    assertions: [{ kind: "currentRoundOrderEquals", expected: "p1_0", parameters: {} }]
  });

  assert.equal(validation.valid, false);
  assert.ok(validation.diagnostics.some(d => d.code === "InvalidAssertionExpectedType"));
});

test("rejects invalid shared battle primitive parameters", () => {
  const validation = validateScenarioSpec({
    feature: "Battle",
    scenario: "InvalidSharedPrimitiveParameters",
    tags: ["battle"],
    requiredAdapters: ["Battle"],
    timeoutMs: 10000,
    setup: [],
    actions: [
      { kind: "setUnitFacing", parameters: { unitAlias: "actor", facing: "Diagonal" } },
      { kind: "registerSummon", parameters: { ownerAlias: "actor", summonAlias: "summon", maximumActive: 0 } }
    ],
    assertions: [{ kind: "currentRoundOrderEquals", expected: ["actor"], parameters: {} }]
  });

  assert.equal(validation.valid, false);
  assert.ok(validation.diagnostics.some(d => d.code === "InvalidFacing"));
  assert.ok(validation.diagnostics.some(d => d.code === "InvalidMaximumActive"));
});
