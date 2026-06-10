import assert from "node:assert/strict";
import test from "node:test";
import { compileScenarioSpec } from "../src/compiler.js";
import { parseGameplayTestDocument } from "../src/frontmatter.js";
import { generateScenarioSpec } from "../src/generator.js";

test("generates and compiles self heal scenario", () => {
  const generated = generateScenarioSpec("自身治疗技能，caster HP 从 6 到 10");
  assert.equal(generated.needsClarification, false);
  assert.ok(generated.spec);

  const compiled = compileScenarioSpec(generated.spec);
  assert.equal(compiled.valid, true);
  assert.equal(compiled.plan?.scenarioName, "SkillGraph.SelfHealSkillRaisesCasterHealth");
  assert.equal(compiled.plan?.assertionPlans.length, 2);
});

test("reports unrecognized natural language input", () => {
  const generated = generateScenarioSpec("测一下这个技能别太离谱");
  assert.equal(generated.needsClarification, true);
  assert.equal(generated.missingFields.includes("scenarioIntent"), true);
  assert.equal(generated.diagnostics[0].code, "UnrecognizedIntent");
});

test("parses markdown frontmatter and rejects unsupported assertions", () => {
  const doc = parseGameplayTestDocument(`---
feature: SkillGraph
scenario: UnsupportedAssertion
requiredAdapters:
  - Skill
setup: []
actions:
  - kind: executeSkillGraph
assertions:
  - kind: visualLooksCorrect
timeoutMs: 10000
---

# Unsupported
`);

  const compiled = compileScenarioSpec(doc.frontmatter);
  assert.equal(compiled.valid, false);
  assert.equal(compiled.diagnostics.some(d => d.code === "UnsupportedAssertionKind"), true);
});
