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
