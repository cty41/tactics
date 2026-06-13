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
