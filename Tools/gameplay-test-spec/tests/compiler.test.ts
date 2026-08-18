import { readFile } from "node:fs/promises";
import assert from "node:assert/strict";
import test from "node:test";
import { compileScenarioSpec } from "../src/compiler.js";
import { parseGameplayTestDocument } from "../src/frontmatter.js";
import { generateScenarioSpec } from "../src/generator.js";
import { GodotExecutableScenarioPlanSchema, type ScenarioSpec } from "../src/schema.js";
import { validateScenarioSpec } from "../src/validator.js";

const fixturesDirUrl = new URL("../../../../Tests/gameplay-specs/", import.meta.url);

async function readFixture(name: string): Promise<string> {
  return readFile(new URL(name, fixturesDirUrl), "utf8");
}

function normalizePlan(plan: unknown): unknown {
  return JSON.parse(JSON.stringify(plan));
}

test("generates and compiles self heal scenario fixture", async () => {
  const markdown = await readFixture("self-heal.gameplay-test.md");
  const planJson = await readFixture("self-heal.plan.json");
  const doc = parseGameplayTestDocument(markdown);

  const generated = generateScenarioSpec("自身治疗技能，caster HP 从 6 到 10");
  assert.equal(generated.needsClarification, false);
  assert.ok(generated.spec);
  assert.deepEqual(generated.spec, doc.frontmatter);

  const validation = validateScenarioSpec(doc.frontmatter);
  assert.equal(validation.valid, true);

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, true);
  assert.equal(compiled.plan?.scenarioName, "SkillGraph.SelfHealSkillRaisesCasterHealth");
  assert.equal(compiled.plan?.assertionPlans.length, 2);
  assert.deepEqual(normalizePlan(compiled.plan), JSON.parse(planJson));
});

test("generates and compiles single target damage scenario fixture", async () => {
  const markdown = await readFixture("single-target-damage.gameplay-test.md");
  const planJson = await readFixture("single-target-damage.plan.json");
  const doc = parseGameplayTestDocument(markdown);

  const generated = generateScenarioSpec("单体伤害技能，目标 HP 从 10 到 3");
  assert.equal(generated.needsClarification, false);
  assert.ok(generated.spec);
  assert.deepEqual(generated.spec, doc.frontmatter);

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, true);
  assert.equal(compiled.plan?.scenarioName, "SkillGraph.SingleTargetDamageReducesTargetHealth");
  assert.equal(compiled.plan?.assertionPlans.length, 2);
  assert.deepEqual(normalizePlan(compiled.plan), JSON.parse(planJson));
});

test("generates and compiles invalid graph scenario fixture", async () => {
  const markdown = await readFixture("invalid-graph.gameplay-test.md");
  const planJson = await readFixture("invalid-graph.plan.json");
  const doc = parseGameplayTestDocument(markdown);

  const generated = generateScenarioSpec("无终点节点的非法技能图");
  assert.equal(generated.needsClarification, false);
  assert.ok(generated.spec);
  assert.deepEqual(generated.spec, doc.frontmatter);

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, true);
  assert.equal(compiled.plan?.scenarioName, "SkillGraph.InvalidGraphWithoutTerminalIsRejected");
  assert.equal(compiled.plan?.assertionPlans.length, 2);
  assert.deepEqual(normalizePlan(compiled.plan), JSON.parse(planJson));
});

test("reports unrecognized natural language input", () => {
  const generated = generateScenarioSpec("测一下这个技能别太离谱");
  assert.equal(generated.needsClarification, true);
  assert.equal(generated.missingFields.includes("scenarioIntent"), true);
  assert.equal(generated.diagnostics[0].code, "UnrecognizedIntent");
});

test("parses markdown frontmatter and rejects unsupported assertions fixture", async () => {
  const markdown = await readFixture("unsupported-assertion.gameplay-test.md");
  const doc = parseGameplayTestDocument(markdown);

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, false);
  assert.ok(compiled.diagnostics.some(d => d.code === "UnsupportedAssertionKind"));
});

test("generates and compiles mana success ability scenario", () => {
  const generated = generateScenarioSpec("蓝量足够时释放自愈技能，成功后扣 3 点 Mana，caster 从 6 回到 10");
  assert.equal(generated.needsClarification, false);
  assert.ok(generated.spec);
  assert.equal(generated.spec?.scenario, "ManaConsumedOnSuccessfulAbilityUse");

  const validation = validateScenarioSpec(generated.spec);
  assert.equal(validation.valid, true, validation.diagnostics.map(d => d.message).join("\n"));

  const compiled = compileScenarioSpec(generated.spec);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.equal(compiled.plan?.runtimeActions[0].kind, "executeAbilityOnTarget");
  assert.ok(compiled.plan?.assertionPlans.some(assertion => assertion.kind === "unitManaEquals"));
});

test("generates and compiles mana insufficient ability scenario", () => {
  const generated = generateScenarioSpec("蓝量不足时释放自愈技能，失败且不扣 Mana");
  assert.equal(generated.needsClarification, false);
  assert.ok(generated.spec);
  assert.equal(generated.spec?.scenario, "ManaInsufficientPreventsAbilityUse");

  const compiled = compileScenarioSpec(generated.spec);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.ok(compiled.plan?.assertionPlans.some(assertion => assertion.kind === "lastErrorContains"));
});

test("generates and compiles out of range ability scenario", () => {
  const generated = generateScenarioSpec("目标超出射程时单体伤害技能失败且不扣 Mana");
  assert.equal(generated.needsClarification, false);
  assert.ok(generated.spec);
  assert.equal(generated.spec?.scenario, "TargetOutOfRangePreventsAbilityUse");

  const compiled = compileScenarioSpec(generated.spec);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.equal(compiled.plan?.runtimeActions[0].kind, "executeAbilityOnTarget");
});

test("generates and compiles no valid target ability scenario", () => {
  const generated = generateScenarioSpec("没有任何有效目标时单体伤害技能失败且不扣 Mana");
  assert.equal(generated.needsClarification, false);
  assert.ok(generated.spec);
  assert.equal(generated.spec?.scenario, "NoValidTargetPreventsAbilityUse");

  const compiled = compileScenarioSpec(generated.spec);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.equal(compiled.plan?.runtimeActions[0].kind, "executeAbilityOnCell");
  assert.ok(compiled.plan?.assertionPlans.some(assertion => assertion.kind === "stepMessageContains"));
});

test("compiles battle advance round fixture", async () => {
  const markdown = await readFixture("battle-advance-turn.gameplay-test.md");
  const planJson = await readFixture("battle-advance-turn.plan.json");
  const doc = parseGameplayTestDocument(markdown);

  const validation = validateScenarioSpec(doc.frontmatter);
  assert.equal(validation.valid, true, validation.diagnostics.map(d => d.message).join("\n"));

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.ok(compiled.plan);
  assert.equal(compiled.plan.scenarioName, "Battle.BattleAdvancesRound");
  assert.deepEqual(normalizePlan(compiled.plan), JSON.parse(planJson));
});

test("rejects battle fixture with unsupported action kind", async () => {
  const markdown = await readFixture("battle-unsupported-kind.gameplay-test.md");
  const doc = parseGameplayTestDocument(markdown);

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, false);
  assert.ok(compiled.diagnostics.some(d => d.code === "UnsupportedActionKind"));
});

// Mixed-adapter routing tests

test("mixed-adapter: Battle + Skill routes correctly", async () => {
  const markdown = await readFixture("battle-full-combat-victory.gameplay-test.md");
  const doc = parseGameplayTestDocument(markdown);

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.ok(compiled.plan);

  // Setup routing
  const setupKinds = compiled.plan.setupActions.map(a => `${a.kind}(${a.adapter})`);
  assert.ok(setupKinds.includes("bindBattleController(Battle)"), `bindBattleController should route to Battle, got: ${setupKinds.join(", ")}`);
  assert.ok(setupKinds.includes("createSkillGraph(Skill)"), `createSkillGraph should route to Skill, got: ${setupKinds.join(", ")}`);

  // Action routing
  const actionKinds = compiled.plan.runtimeActions.map(a => `${a.kind}(${a.adapter})`);
  assert.ok(actionKinds.includes("executeBattleSkillGraph(Battle)"), `executeBattleSkillGraph should route to Battle, got: ${actionKinds.join(", ")}`);

  // Assertion routing
  const assertionKinds = compiled.plan.assertionPlans.map(a => `${a.kind}(${a.adapter})`);
  assert.ok(assertionKinds.includes("unitAliveEquals(Battle)"), `unitAliveEquals should route to Battle, got: ${assertionKinds.join(", ")}`);
  assert.ok(assertionKinds.includes("battleResultEquals(Battle)"), `battleResultEquals should route to Battle, got: ${assertionKinds.join(", ")}`);
  assert.ok(assertionKinds.includes("battleIsActive(Battle)"), `battleIsActive should route to Battle, got: ${assertionKinds.join(", ")}`);
});

test("mixed-adapter: Map + Battle routes correctly", async () => {
  const markdown = await readFixture("map/map-battle-node.gameplay-test.md");
  const doc = parseGameplayTestDocument(markdown);

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.ok(compiled.plan);

  // Setup routing
  const setupKinds = compiled.plan.setupActions.map(a => `${a.kind}(${a.adapter})`);
  assert.ok(setupKinds.includes("loadRoguelikeMap(Map)"), `loadRoguelikeMap should route to Map, got: ${setupKinds.join(", ")}`);
  assert.ok(setupKinds.includes("bindBattleController(Battle)"), `bindBattleController should route to Battle, got: ${setupKinds.join(", ")}`);
  assert.ok(setupKinds.includes("createSkillGraph(Skill)"), `createSkillGraph should route to Skill, got: ${setupKinds.join(", ")}`);

  // Action routing
  const actionKinds = compiled.plan.runtimeActions.map(a => `${a.kind}(${a.adapter})`);
  assert.ok(actionKinds.includes("enterNode(Map)"), `enterNode should route to Map, got: ${actionKinds.join(", ")}`);
  assert.ok(actionKinds.includes("executeBattleSkillGraph(Battle)"), `executeBattleSkillGraph should route to Battle, got: ${actionKinds.join(", ")}`);
  assert.ok(actionKinds.includes("completeNode(Map)"), `completeNode should route to Map, got: ${actionKinds.join(", ")}`);

  // Assertion routing
  const assertionKinds = compiled.plan.assertionPlans.map(a => `${a.kind}(${a.adapter})`);
  assert.ok(assertionKinds.includes("mapIsActive(Map)"), `mapIsActive should route to Map, got: ${assertionKinds.join(", ")}`);
  assert.ok(assertionKinds.includes("battleIsActive(Battle)"), `battleIsActive should route to Battle, got: ${assertionKinds.join(", ")}`);
  assert.ok(assertionKinds.includes("nodeIsVisited(Map)"), `nodeIsVisited should route to Map, got: ${assertionKinds.join(", ")}`);
});

test("mixed-adapter: UI + Map + Battle routes correctly", async () => {
  const markdown = await readFixture("ui/ui-map-battle-integration.gameplay-test.md");
  const doc = parseGameplayTestDocument(markdown);

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.ok(compiled.plan);

  // Setup routing
  const setupKinds = compiled.plan.setupActions.map(a => `${a.kind}(${a.adapter})`);
  assert.ok(setupKinds.includes("loadPureRunMap(Map)"), `loadPureRunMap should route to Map, got: ${setupKinds.join(", ")}`);
  assert.ok(setupKinds.includes("bindBattleController(Battle)"), `bindBattleController should route to Battle, got: ${setupKinds.join(", ")}`);
  assert.ok(setupKinds.includes("createSkillGraph(Skill)"), `createSkillGraph should route to Skill, got: ${setupKinds.join(", ")}`);

  // Action routing
  const actionKinds = compiled.plan.runtimeActions.map(a => `${a.kind}(${a.adapter})`);
  assert.ok(actionKinds.includes("openUI(UI)"), `openUI should route to UI, got: ${actionKinds.join(", ")}`);
  assert.ok(actionKinds.includes("executeBattleSkillGraph(Battle)"), `executeBattleSkillGraph should route to Battle, got: ${actionKinds.join(", ")}`);
  assert.ok(actionKinds.includes("completeNode(Map)"), `completeNode should route to Map, got: ${actionKinds.join(", ")}`);

  // Assertion routing
  const assertionKinds = compiled.plan.assertionPlans.map(a => `${a.kind}(${a.adapter})`);
  assert.ok(assertionKinds.includes("mapIsActive(Map)"), `mapIsActive should route to Map, got: ${assertionKinds.join(", ")}`);
  assert.ok(assertionKinds.includes("battleIsActive(Battle)"), `battleIsActive should route to Battle, got: ${assertionKinds.join(", ")}`);
  assert.ok(assertionKinds.includes("nodeIsVisited(Map)"), `nodeIsVisited should route to Map, got: ${assertionKinds.join(", ")}`);
  assert.ok(assertionKinds.includes("elementVisible(UI)"), `elementVisible should route to UI, got: ${assertionKinds.join(", ")}`);
});

test("battle-test-config fixture compiles with stable adapter routing", async () => {
  const markdown = await readFixture("battle-test-config/load-encounter-config.gameplay-test.md");
  const planJson = await readFixture("battle-test-config/load-encounter-config.plan.json");
  const doc = parseGameplayTestDocument(markdown);

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.ok(compiled.plan);
  assert.deepEqual(normalizePlan(compiled.plan), JSON.parse(planJson));

  const setupKinds = compiled.plan.setupActions.map(a => `${a.kind}(${a.adapter})`);
  const actionKinds = compiled.plan.runtimeActions.map(a => `${a.kind}(${a.adapter})`);
  assert.ok(setupKinds.includes("loadTestEncounterConfig(Skill)"), `loadTestEncounterConfig should route to Skill, got: ${setupKinds.join(", ")}`);
  assert.ok(actionKinds.includes("setBattleTestMode(Skill)"), `setBattleTestMode should route to Skill, got: ${actionKinds.join(", ")}`);
});

test("interactable corpse fixture compiles with stable adapter routing", async () => {
  const markdown = await readFixture("interactable-corpse/spawn-interactable-corpse.gameplay-test.md");
  const planJson = await readFixture("interactable-corpse/spawn-interactable-corpse.plan.json");
  const doc = parseGameplayTestDocument(markdown);

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.ok(compiled.plan);
  assert.deepEqual(normalizePlan(compiled.plan), JSON.parse(planJson));

  const actionKinds = compiled.plan.runtimeActions.map(a => `${a.kind}(${a.adapter})`);
  const assertionKinds = compiled.plan.assertionPlans.map(a => `${a.kind}(${a.adapter})`);
  assert.ok(actionKinds.includes("spawnInteractableCorpse(Battle)"), `spawnInteractableCorpse should route to Battle, got: ${actionKinds.join(", ")}`);
  assert.ok(assertionKinds.includes("interactableCorpseExistsAt(Battle)"), `interactableCorpseExistsAt should route to Battle, got: ${assertionKinds.join(", ")}`);
  assert.ok(assertionKinds.includes("cellOccupiedByInteractable(Battle)"), `cellOccupiedByInteractable should route to Battle, got: ${assertionKinds.join(", ")}`);
});

test("necromancer corpse dependency fixture compiles with stable adapter routing", async () => {
  const markdown = await readFixture("necromancer/summon-requires-corpse.gameplay-test.md");
  const planJson = await readFixture("necromancer/summon-requires-corpse.plan.json");
  const doc = parseGameplayTestDocument(markdown);

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.ok(compiled.plan);
  assert.deepEqual(normalizePlan(compiled.plan), JSON.parse(planJson));
});

test("compiles run seed and growth assertions from the authored source spec", async () => {
  const markdown = await readFixture("map/run-seed-growth-assertions.gameplay-test.md");
  const generatedPlan = await readFixture("map/run-seed-growth-assertions.plan.json");
  const doc = parseGameplayTestDocument(markdown);

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.ok(compiled.plan);
  assert.deepEqual(normalizePlan(compiled.plan), JSON.parse(generatedPlan));
  assert.equal(compiled.plan.setupActions[0].kind, "setRunSeed");
  assert.equal(compiled.plan.setupActions[0].adapter, "Map");
  assert.equal(compiled.plan.setupActions[1].parameters.strictAsset, true);
  assert.ok(compiled.plan.assertionPlans.every(assertion => assertion.adapter === "Map"));
});

test("compiles Pure Run mixed level-up assertions from the authored source spec", async () => {
  const markdown = await readFixture("map/pure-run-mixed-levelup-candidates.gameplay-test.md");
  const generatedPlan = await readFixture("map/pure-run-mixed-levelup-candidates.plan.json");
  const doc = parseGameplayTestDocument(markdown);

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.ok(compiled.plan);
  assert.deepEqual(normalizePlan(compiled.plan), JSON.parse(generatedPlan));
  assert.deepEqual(
    compiled.plan.assertionPlans.map(assertion => assertion.kind),
    [
      "mapIsActive",
      "rosterCharacterSkillLevelEquals",
      "pureRunSkillChoiceContains",
      "pureRunSkillChoicesAreMixed"
    ]
  );
  assert.ok(compiled.plan.assertionPlans.every(assertion => assertion.adapter === "Map"));
});

test("compiles shared battle primitive specs with stable Battle routing", async () => {
  const fixtureNames = [
    "facing-and-initiative",
    "status-turn-semantics",
    "summon-registry-order",
    "ability-availability-reason",
    "ordered-target-selection-state"
  ];

  for (const fixtureName of fixtureNames) {
    const markdown = await readFixture(`shared/${fixtureName}.gameplay-test.md`);
    const generatedPlan = await readFixture(`shared/${fixtureName}.plan.json`);
    const compiled = compileScenarioSpec(parseGameplayTestDocument(markdown).frontmatter);
    assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
    assert.ok(compiled.plan);
    assert.deepEqual(normalizePlan(compiled.plan), JSON.parse(generatedPlan));
    assert.ok(compiled.plan.runtimeActions.every(action => action.adapter === "Battle"));
  }
});

test("routes shared UI interaction actions and observable assertions to UI", () => {
  const compiled = compileScenarioSpec({
    feature: "UI",
    scenario: "SharedInteractionRouting",
    tags: ["ui"],
    requiredAdapters: ["UI"],
    timeoutMs: 10000,
    setup: [],
    actions: [
      { kind: "hoverElement", parameters: { elementName: "Card" } },
      { kind: "rightClickElement", parameters: { elementName: "Card" } },
      { kind: "pressKey", parameters: { key: "Escape" } }
    ],
    assertions: [
      { kind: "elementClassContains", target: "Card", expected: "selected", parameters: {} },
      { kind: "elementChildOrderEquals", target: "Deck", expected: ["Card"], parameters: {} },
      { kind: "elementRectRelationEquals", target: "Card", expected: "rightOf", parameters: { otherElement: "Move" } },
      { kind: "abilityCardAvailabilityEquals", target: "Card", expected: "DisabledClickable", parameters: {} },
      { kind: "targetMarkerOrderEquals", target: "Markers", expected: ["1", "2"], parameters: {} },
      { kind: "selectionStageEquals", adapter: "UI", expected: "Selecting", parameters: {} }
    ]
  });

  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.ok(compiled.plan);
  assert.ok(compiled.plan.runtimeActions.every(action => action.adapter === "UI"));
  assert.ok(compiled.plan.assertionPlans.every(assertion => assertion.adapter === "UI"));
});

test("routes semantic player input actions to PlayerInput", () => {
  const compiled = compileScenarioSpec({
    feature: "PlayerInput",
    scenario: "PlayerInputRouting",
    tags: ["player-input-e2e"],
    requiredAdapters: ["UI", "PlayerInput"],
    timeoutMs: 10000,
    setup: [{ kind: "initializePlayerInput", parameters: {} }],
    actions: [
      { kind: "movePointerToTarget", target: "NewGameButton", parameters: { targetKind: "UiElement" } },
      { kind: "clickPointerTarget", target: "NewGameButton", parameters: { targetKind: "UiElement" } },
      { kind: "rightClickPointerTarget", target: "Card", parameters: { targetKind: "UiElement" } },
      { kind: "pressInputKey", parameters: { key: "Escape" } },
      { kind: "waitForPlayerObservable", parameters: { observable: "uiVisible", uiId: "Home" } },
      { kind: "waitForFrames", parameters: { frames: 3 } },
      { kind: "playBattleThroughInput", parameters: { maximumActions: 100 } }
    ],
    assertions: [{ kind: "elementExists", adapter: "UI", target: "NewGameButton", expected: true, parameters: {} }]
  });

  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.ok(compiled.plan);
  assert.ok(compiled.plan.setupActions.every(action => action.adapter === "PlayerInput"));
  assert.ok(compiled.plan.runtimeActions.every(action => action.adapter === "PlayerInput"));
  assert.ok(compiled.plan.assertionPlans.every(assertion => assertion.adapter === "UI"));
});

test("keeps legacy Unity plans unchanged and emits capability-bound Godot v2 plans", () => {
  const spec: ScenarioSpec = {
    feature: "GodotQA",
    scenario: "DefeatFlow",
    tags: ["godot-qa"],
    requiredAdapters: ["Map", "Battle", "UI"],
    timeoutMs: 10000,
    setup: [{ kind: "loadValidatedCheckpoint", parameters: {
      id: "defeat-no-summon", path: "Tests/checkpoints/defeat.json", semanticHash: "a".repeat(64)
    } }],
    actions: [{ kind: "endTurnOnlyUntilTerminal", parameters: {} }],
    assertions: [{ kind: "terminalSummaryOutcomeEquals", expected: "Defeated", parameters: {} }]
  };

  const unity = compileScenarioSpec(spec);
  const godot = compileScenarioSpec(spec, { runtime: "Godot" });
  assert.equal(unity.valid, false);
  assert.ok(unity.diagnostics.some(value => value.code === "UnsupportedRuntimeCapability"));
  assert.equal(godot.valid, true, godot.diagnostics.map(value => value.message).join("\n"));
  assert.equal(godot.plan?.schemaVersion, 2);
  if (godot.plan?.schemaVersion === 2) {
    assert.equal(godot.plan.runtime, "Godot");
    assert.equal(godot.plan.checkpoint?.source, "validated_checkpoint");
    assert.equal(godot.plan.saveIsolation.protectProductionSave, true);
    assert.ok(godot.plan.requiredCapabilities.includes("action:endTurnOnlyUntilTerminal"));
  }
});

test("emits backward-compatible Godot v3 only for Demonbound runtime probes", () => {
  const compiled = compileScenarioSpec({
    feature: "Demonbound", scenario: "CorruptionProbe", tags: ["godot-qa"],
    requiredAdapters: ["PlayerInput", "Battle"], timeoutMs: 10000,
    setup: [{ kind: "initializePlayerInput", parameters: {} }],
    actions: [{ kind: "clickPointerTarget", target: "MeditateAction", parameters: {} }],
    assertions: [
      { kind: "demonboundCorruptionEquals", target: "party-pure_run_demonbound", expected: 0, parameters: {} },
      { kind: "demonboundPossessedEquals", target: "party-pure_run_demonbound", expected: false, parameters: {} }
    ]
  }, { runtime: "Godot" });
  assert.equal(compiled.valid, true, compiled.diagnostics.map(value => value.message).join("\n"));
  assert.equal(compiled.plan?.schemaVersion, 3);
});

test("rejects capabilities not implemented by the Godot runtime", () => {
  const compiled = compileScenarioSpec({
    feature: "GodotQA", scenario: "RejectShortcut", tags: [], requiredAdapters: ["Map"], timeoutMs: 10000,
    setup: [], actions: [{ kind: "enterNode", parameters: { nodeId: "n1" } }],
    assertions: [{ kind: "mapIsActive", expected: true, parameters: {} }]
  }, { runtime: "Godot" });
  assert.equal(compiled.valid, false);
  assert.ok(compiled.diagnostics.some(value => value.code === "UnsupportedRuntimeCapability"));
});

test("rejects malformed Godot action parameters, assertion values, and adapter declarations", () => {
  const base = {
    feature: "GodotQA", scenario: "StrictContract", tags: [], requiredAdapters: ["UI"], timeoutMs: 10000,
    setup: [], actions: [{ kind: "setPresentationSpeed", parameters: { speed: "fast" } }],
    assertions: [{ kind: "presentationNodeCountEquals", expected: "zero", parameters: {} }]
  };
  const malformed = compileScenarioSpec(base, { runtime: "Godot" });
  assert.equal(malformed.valid, false);
  assert.ok(malformed.diagnostics.some(value => value.code === "InvalidPresentationSpeed"));
  assert.ok(malformed.diagnostics.some(value => value.code === "InvalidAssertionExpectedType"));

  const mismatched = compileScenarioSpec({ ...base,
    actions: [{ kind: "setPresentationPaused", adapter: "Skill", parameters: { paused: true } }],
    assertions: [{ kind: "runtimeHasNoErrors", expected: true, parameters: {} }]
  }, { runtime: "Godot" });
  assert.equal(mismatched.valid, false);
  assert.ok(mismatched.diagnostics.some(value => value.code === "RuntimeAdapterMismatch"));
});

test("rejects a directly tampered Godot v2 probe contract", () => {
  const compiled = compileScenarioSpec({
    feature: "GodotQA", scenario: "ProbeIntegrity", tags: [], requiredAdapters: ["UI"], timeoutMs: 10000,
    setup: [], actions: [{ kind: "setPresentationPaused", parameters: { paused: true } }],
    assertions: [{ kind: "runtimeHasNoErrors", expected: true, parameters: {} }]
  }, { runtime: "Godot" });
  assert.equal(compiled.valid, true);
  const tampered = structuredClone(compiled.plan!);
  tampered.probeRequests[0] = { adapter: "Skill", kind: "executeSkillGraph", parameters: {} };
  assert.equal(GodotExecutableScenarioPlanSchema.safeParse(tampered).success, false);
});

test("rejects every strict PlayerInput setup and runtime shortcut boundary", () => {
  const createSpec = (): ScenarioSpec => ({
    feature: "PlayerInput",
    scenario: "StrictPlayerInputBoundary",
    tags: ["player-input-e2e"],
    requiredAdapters: ["UI", "PlayerInput"],
    timeoutMs: 10000,
    setup: [{ kind: "initializePlayerInput", adapter: "PlayerInput", parameters: {} }],
    actions: [{ kind: "pressInputKey", adapter: "PlayerInput", parameters: { key: "Escape" } }],
    assertions: [{ kind: "elementExists", adapter: "UI", target: "Home", expected: true, parameters: {} }]
  });

  const cases: Array<{
    name: string;
    diagnosticCode: string;
    mutate: (spec: ScenarioSpec) => void;
  }> = [
    {
      name: "missing required PlayerInput adapter",
      diagnosticCode: "MissingPlayerInputAdapter",
      mutate: spec => { spec.requiredAdapters = ["UI"]; }
    },
    {
      name: "Map setup action",
      diagnosticCode: "PlayerInputE2ESetupShortcut",
      mutate: spec => { spec.setup = [{ kind: "setRunSeed", adapter: "Map", parameters: { seed: 1 } }]; }
    },
    {
      name: "initializePlayerInput with a wrong adapter",
      diagnosticCode: "PlayerInputE2ESetupShortcut",
      mutate: spec => { spec.setup[0].adapter = "Map"; }
    },
    {
      name: "Map runtime action",
      diagnosticCode: "PlayerInputE2EActionShortcut",
      mutate: spec => { spec.actions = [{ kind: "enterNode", adapter: "Map", target: "node", parameters: {} }]; }
    },
    {
      name: "UI runtime action",
      diagnosticCode: "PlayerInputE2EActionShortcut",
      mutate: spec => { spec.actions = [{ kind: "clickElement", adapter: "UI", target: "button", parameters: {} }]; }
    },
    {
      name: "Battle runtime action",
      diagnosticCode: "PlayerInputE2EActionShortcut",
      mutate: spec => { spec.actions = [{ kind: "advanceTurn", adapter: "Battle", parameters: {} }]; }
    },
    {
      name: "Skill runtime action",
      diagnosticCode: "PlayerInputE2EActionShortcut",
      mutate: spec => { spec.actions = [{ kind: "executeSkillGraph", adapter: "Skill", parameters: {} }]; }
    },
    {
      name: "allowed action kind with a wrong adapter",
      diagnosticCode: "PlayerInputE2EActionShortcut",
      mutate: spec => { spec.actions[0].adapter = "UI"; }
    }
  ];

  for (const testCase of cases) {
    const spec = createSpec();
    testCase.mutate(spec);
    const validation = validateScenarioSpec(spec);
    assert.equal(validation.valid, false, `${testCase.name} unexpectedly validated.`);
    assert.ok(
      validation.diagnostics.some(diagnostic => diagnostic.code === testCase.diagnosticCode),
      `${testCase.name} did not report ${testCase.diagnosticCode}. Diagnostics=${validation.diagnostics.map(diagnostic => diagnostic.code).join(",")}`
    );
  }
});

test("compiles structured AI turn result assertions from the authored source spec", async () => {
  const markdown = await readFixture("battle-ai-turn-result.gameplay-test.md");
  const generatedPlan = await readFixture("battle-ai-turn-result.plan.json");
  const doc = parseGameplayTestDocument(markdown);
  const source = doc.frontmatter as ScenarioSpec;
  source.assertions.push(
    { kind: "aiTurnAbilityEquals", expected: "BasicAttack", parameters: {} },
    { kind: "aiTurnDestinationEquals", expected: "13,25", parameters: {} },
    { kind: "aiTurnTargetPointEquals", expected: "17,25", parameters: {} },
    { kind: "aiTurnTargetCountEquals", expected: 1, parameters: {} },
    { kind: "aiTurnPatternStepEquals", expected: "0", parameters: {} }
  );

  const compiled = compileScenarioSpec(source);
  assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
  assert.ok(compiled.plan);
  assert.ok(compiled.plan.assertionPlans.every(assertion => assertion.adapter === "Battle"));

  const fixtureCompiled = compileScenarioSpec(parseGameplayTestDocument(markdown).frontmatter);
  assert.ok(fixtureCompiled.plan);
  assert.deepEqual(normalizePlan(fixtureCompiled.plan), JSON.parse(generatedPlan));
});

test("deep-compares all strict PlayerInput sources with the runtime plans consumed by Unity", {
  skip: process.env.GODOT_OWNED_VERIFY === "1" ? "Unity runtime plans are outside the Godot-owned repository boundary." : false
}, async () => {
  for (const [sourceName, runtimePlanName] of [
    ["battle/battle-player-input-smoke.gameplay-test.md", "compiled/battle-player-input-smoke.plan.json"],
    ["ui/inventory-reentry-player-input.gameplay-test.md", "compiled/inventory-reentry-player-input.plan.json"],
    ["map/pure-run-real-player-route.gameplay-test.md", "compiled/pure-run-real-player-route.plan.json"],
    ["map/pure-run-mystery-real-player-commit.gameplay-test.md", "compiled/pure-run-mystery-real-player-commit.plan.json"],
    ["map/pure-run-mystery-real-player-result-page.gameplay-test.md", "compiled/pure-run-mystery-real-player-result-page.plan.json"]
  ] as const) {
    const spec = parseGameplayTestDocument(await readFixture(sourceName)).frontmatter as ScenarioSpec;
    const compiled = compileScenarioSpec(spec);
    assert.equal(compiled.valid, true, compiled.diagnostics.map(d => d.message).join("\n"));
    assert.ok(compiled.plan);
    assert.deepEqual(normalizePlan(compiled.plan), JSON.parse(await readFixture(runtimePlanName)));
  }
});

test("validates and compiles both shortcut-free Mystery player-input E2E fixtures", async () => {
  for (const fixtureName of [
    "map/pure-run-mystery-real-player-commit.gameplay-test.md",
    "map/pure-run-mystery-real-player-result-page.gameplay-test.md"
  ]) {
    const spec = parseGameplayTestDocument(await readFixture(fixtureName)).frontmatter as ScenarioSpec;
    assert.ok(spec.tags.includes("player-input-e2e"));
    assert.deepEqual(spec.setup.map(step => step.kind), ["initializePlayerInput"]);
    assert.ok(spec.actions.every(action => action.adapter === "PlayerInput"));
    assert.equal(spec.actions.filter(action => action.kind === "playBattleThroughInput").length, 3);

    const validation = validateScenarioSpec(spec);
    assert.equal(validation.valid, true, validation.diagnostics.map(d => d.message).join("\n"));
    assert.equal(compileScenarioSpec(spec).valid, true);
  }
});

test("rejects malformed run seed, strict asset, and AI turn result expected values", async () => {
  const mapMarkdown = await readFixture("map/run-seed-growth-assertions.gameplay-test.md");
  const mapSpec = parseGameplayTestDocument(mapMarkdown).frontmatter as ScenarioSpec;
  mapSpec.setup[0].parameters.seed = 1.5;
  mapSpec.setup[1].parameters.strictAsset = "yes";

  const mapValidation = validateScenarioSpec(mapSpec);
  assert.equal(mapValidation.valid, false);
  assert.ok(mapValidation.diagnostics.some(d => d.code === "InvalidRunSeed"));
  assert.ok(mapValidation.diagnostics.some(d => d.code === "InvalidStrictAsset"));

  const battleMarkdown = await readFixture("battle-ai-turn-result.gameplay-test.md");
  const battleSpec = parseGameplayTestDocument(battleMarkdown).frontmatter as ScenarioSpec;
  battleSpec.assertions[0].expected = "true";

  const battleValidation = validateScenarioSpec(battleSpec);
  assert.equal(battleValidation.valid, false);
  assert.ok(battleValidation.diagnostics.some(d => d.code === "InvalidAssertionExpectedType"));
});
