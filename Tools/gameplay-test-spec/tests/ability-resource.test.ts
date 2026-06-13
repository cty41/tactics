import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { compileScenarioSpec } from "../src/compiler.js";
import { parseGameplayTestDocument } from "../src/frontmatter.js";
import { generateScenarioSpec } from "../src/generator.js";
import { validateScenarioSpec } from "../src/validator.js";

const fixturesDirUrl = new URL("../../../../Tests/gameplay-specs/", import.meta.url);

const abilityScenarios = [
  {
    fixtureBaseName: "mana-success",
    text: "蓝量足够时释放自愈技能，成功后扣 3 点 Mana，caster 从 6 回到 10"
  },
  {
    fixtureBaseName: "mana-insufficient",
    text: "蓝量不足时释放自愈技能，失败且不扣 Mana"
  },
  {
    fixtureBaseName: "out-of-range-failure",
    text: "目标超出射程时单体伤害技能失败且不扣 Mana"
  },
  {
    fixtureBaseName: "no-valid-target-failure",
    text: "没有任何有效目标时单体伤害技能失败且不扣 Mana"
  }
] as const;

const skillArchetypeScenarios = [
  {
    fixtureBaseName: "barbarian-counter",
    text: "让敌人获得反击状态，然后被近战攻击触发反击"
  },
  {
    fixtureBaseName: "hunter-mark",
    text: "先给敌人挂上标记，再让下一次攻击必定暴击"
  },
  {
    fixtureBaseName: "mage-fireball",
    text: "范围伤害命中半径内的多个目标"
  },
  {
    fixtureBaseName: "barbarian-charge-strike",
    text: "冲锋到目标并撞击造成伤害"
  },
  {
    fixtureBaseName: "melee-heal",
    text: "治疗友军目标并恢复 4 点生命"
  }
] as const;

async function readFixture(name: string): Promise<string> {
  return readFile(new URL(name, fixturesDirUrl), "utf8");
}

function normalizePlan(plan: unknown): unknown {
  return JSON.parse(JSON.stringify(plan));
}

for (const scenario of abilityScenarios) {
  test(`round-trips ${scenario.fixtureBaseName} gameplay test fixture`, async () => {
    const markdown = await readFixture(`${scenario.fixtureBaseName}.gameplay-test.md`);
    const planJson = await readFixture(`${scenario.fixtureBaseName}.plan.json`);
    const doc = parseGameplayTestDocument(markdown);

    const generated = generateScenarioSpec(scenario.text);
    assert.equal(generated.needsClarification, false);
    assert.ok(generated.spec);
    assert.deepEqual(generated.spec, doc.frontmatter);

    const validation = validateScenarioSpec(doc.frontmatter);
    assert.equal(validation.valid, true, validation.diagnostics.map(diagnostic => diagnostic.message).join("\n"));

    const compiled = compileScenarioSpec(doc.frontmatter);
    assert.equal(compiled.valid, true, compiled.diagnostics.map(diagnostic => diagnostic.message).join("\n"));
    assert.ok(compiled.plan);
    assert.deepEqual(normalizePlan(compiled.plan), JSON.parse(planJson));
  });
}

for (const scenario of skillArchetypeScenarios) {
  test(`round-trips ${scenario.fixtureBaseName} gameplay test fixture`, async () => {
    const markdown = await readFixture(`${scenario.fixtureBaseName}.gameplay-test.md`);
    const planJson = await readFixture(`${scenario.fixtureBaseName}.plan.json`);
    const doc = parseGameplayTestDocument(markdown);

    const generated = generateScenarioSpec(scenario.text);
    assert.equal(generated.needsClarification, false);
    assert.ok(generated.spec);
    assert.deepEqual(generated.spec, doc.frontmatter);

    const validation = validateScenarioSpec(doc.frontmatter);
    assert.equal(validation.valid, true, validation.diagnostics.map(diagnostic => diagnostic.message).join("\n"));

    const compiled = compileScenarioSpec(doc.frontmatter);
    assert.equal(compiled.valid, true, compiled.diagnostics.map(diagnostic => diagnostic.message).join("\n"));
    assert.ok(compiled.plan);
    assert.deepEqual(normalizePlan(compiled.plan), JSON.parse(planJson));
  });
}
