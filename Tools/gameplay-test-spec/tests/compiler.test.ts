import { readFile } from "node:fs/promises";
import assert from "node:assert/strict";
import test from "node:test";
import { compileScenarioSpec } from "../src/compiler.js";
import { parseGameplayTestDocument } from "../src/frontmatter.js";
import { generateScenarioSpec } from "../src/generator.js";
import type { ScenarioSpec } from "../src/schema.js";
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
