import assert from "node:assert/strict";
import test from "node:test";
import { compileAuthoringSpec } from "../src/authoring/compiler.js";

const kinds = ["run-map", "event", "treasure", "encounter", "battle-layout", "ai", "skill", "presentation"] as const;
const documents: Record<(typeof kinds)[number], Record<string, unknown>> = {
  "run-map": { contentId: "run-map.test", schemaVersion: 1, layoutVersion: 2, nodes: [], connections: [] },
  event: { contentId: "event.test", schemaVersion: 2, sourceId: "event_test", title: "Test", description: "", options: [{ id: "continue", text: "Continue", attribute: "None", baseSuccessRate: 100, success: { type: "Nothing", target: "Self", amount: 0, description: "" }, failure: null }] },
  treasure: { contentId: "treasure.test", schemaVersion: 2, goldMinimum: 0, goldMaximum: 1, entries: [] },
  encounter: { contentId: "encounter.test", schemaVersion: 1, layoutContentId: "battle-layout.test", monsterUnitContentIds: ["unit.test"], monsterAiContentIds: ["ai.test"], healthMultiplier: 1, outputMultiplier: 1, minimumStartingMana: 0, encounterClass: "Normal" },
  "battle-layout": { contentId: "battle-layout.test", schemaVersion: 1, partySpawns: [{ x: 1, y: 1 }], enemySpawns: [{ x: 8, y: 8 }], blockedCells: [] },
  ai: { contentId: "ai.test", schemaVersion: 1, archetype: "Charger", skillContentIds: ["skill.test"], patternSkillContentIds: [], distanceWeight: 1, damageWeight: 1, targetCountWeight: 0, harmfulStatusWeight: 0, maximumEngageCandidatesPerTarget: 3, preferredMinimumRange: 1, preferredMaximumRange: 2, preferredRangeRepositionBonus: 0.5, sourceSha256: "sha256:test", nodes: [{ nodeId: "intent", kind: "Intent", type: "BasicAttack", enabled: true, parameter: 1, x: 0, y: 0, curve: [] }], edges: [] },
  skill: { contentId: "skill.test", schemaVersion: 1, sourceId: "skill_test", displayName: "Test", description: "", role: "Any", kind: "Basic", level: 1, manaCost: 0, minRange: 1, maxRange: 1, executionKind: "MeleeAttack", damage: 1, damageKind: "Physical", statusContentId: null, statusDuration: 0, hidden: false, externalDependency: false, isBasicAbility: true, maxUsesPerTurn: 1, canCrit: true, branchId: "", prerequisiteContentId: null, prerequisiteBranchId: "", growthVisible: true, requiredAttribute: "", minimumAttribute: 0, executionProfile: { areaRadius: 0, orderedTargetCount: 0, summonDefinitionId: null, summonCount: 0, summonLimit: 0, summonCategory: "", requiresCorpse: false, ignoreLineOfSight: false, shieldMultiplier: 0, shieldAbsorbsAllDamage: false, cleanseHarmful: false, secondaryDamage: 0, areaShape: "", statusChancePercent: 0, detonateStatusContentId: null, bounceRange: 0, bounceCount: 0, pierceAll: false, allowsEmptyTarget: false, movementDamagePerCell: 0, summonAttackContentId: null, corruptionCost: 0, damageScaling: "None" }, sourceKind: "GodotAuthored", sourcePath: "", sourceGuid: "", sourceLocalFileId: 0, graphPath: "", graphDependencyHash: "" },
  presentation: { contentId: "presentation.test", schemaVersion: 1, resourceClass: "SkillPresentationResource", properties: { AuthoringGraphJsonValue: { kind: "String", value: "{\"nodes\":[{\"id\":\"root\",\"kind\":\"Root\",\"property\":\"\",\"x\":0,\"y\":0,\"enabled\":true}],\"edges\":[]}" } } }
};

test("compiles all supported authoring kinds without writing resources", () => {
  const result = compileAuthoringSpec({ schemaVersion: 1, assets: kinds.map((kind, index) => ({
    operation: "create", kind, contentId: `${kind}.test`, document: { ...documents[kind], index }
  })) });
  assert.equal(result.valid, true, JSON.stringify(result.diagnostics));
  assert.equal(result.batch?.lifecycle.length, 8);
  assert.ok(result.batch?.lifecycle.every(value => value.initialSnapshot));
});

test("projects explicit Unity-style Event graph to the current flat runtime document", () => {
  const result = compileAuthoringSpec({ schemaVersion: 1, assets: [{ operation: "create", kind: "event", contentId: "event.test", eventGraph: {
    sourceId: "test", title: "Door", description: "", options: [{ id: "open", text: "Open", check: { attribute: "Strength", baseSuccessRate: 60 },
      success: { type: "Gold", target: "All", amount: 5 }, failure: { type: "Damage", target: "Self", amount: 2 } }],
    graphLayout: { layoutSchemaVersion: 1, nodes: [{ nodeId: "start", x: 1, y: 2 }] }
  } }] });
  assert.equal(result.valid, true, JSON.stringify(result.diagnostics));
  const snapshot = JSON.parse(result.batch!.lifecycle[0].initialSnapshot!);
  assert.equal(snapshot.options[0].attribute, "Strength"); assert.equal(snapshot.options[0].success.type, "Gold");
  assert.deepEqual(snapshot.graphLayout.nodes, [{ nodeId: "start", x: 1, y: 2 }]);
});

test("requires revision fencing for updates and deletes", () => {
  assert.equal(compileAuthoringSpec({ schemaVersion: 1, assets: [{ operation: "update", kind: "skill", contentId: "skill.x", document: { ...documents.skill, contentId: "skill.x" } }] }).valid, false);
  assert.equal(compileAuthoringSpec({ schemaVersion: 1, assets: [{ operation: "delete", kind: "skill", contentId: "skill.x" }] }).valid, false);
  const deleteWithSnapshot = compileAuthoringSpec({ schemaVersion: 1, assets: [{ operation: "delete", kind: "skill", contentId: "skill.x", expectedReferenceRevision: "refs", document: { ...documents.skill, contentId: "skill.x" } }] });
  assert.equal(deleteWithSnapshot.valid, false);
});

test("orders dependencies deterministically", () => {
  const result = compileAuthoringSpec({ schemaVersion: 1, assets: [
    { operation: "create", kind: "encounter", contentId: "encounter.x", dependencies: ["battle-layout.x"], document: { ...documents.encounter, contentId: "encounter.x", layoutContentId: "battle-layout.x" } },
    { operation: "create", kind: "battle-layout", contentId: "battle-layout.x", document: { ...documents["battle-layout"], contentId: "battle-layout.x" } }
  ] });
  assert.deepEqual(result.batch?.lifecycle.map(value => value.contentId), ["battle-layout.x", "encounter.x"]);
});

test("returns diagnostics for dependency cycles and duplicate event option identities", () => {
  const cycle = compileAuthoringSpec({ schemaVersion: 1, assets: [
    { operation: "create", kind: "skill", contentId: "skill.a", dependencies: ["skill.b"], document: { ...documents.skill, contentId: "skill.a" } },
    { operation: "create", kind: "skill", contentId: "skill.b", dependencies: ["skill.a"], document: { ...documents.skill, contentId: "skill.b" } }
  ] });
  assert.equal(cycle.valid, false); assert.equal(cycle.diagnostics[0].code, "AuthoringDependencyCycle");
  const duplicate = compileAuthoringSpec({ schemaVersion: 1, assets: [{ operation: "create", kind: "event", contentId: "event.duplicate", eventGraph: {
    sourceId: "duplicate", title: "Duplicate", description: "", options: ["a", "b"].map(() => ({ id: "same", text: "Same", check: { attribute: "None", baseSuccessRate: 100 }, success: { type: "Nothing", target: "Self", amount: 0 } }))
  } }] });
  assert.equal(duplicate.valid, false); assert.equal(duplicate.diagnostics[0].code, "AuthoringCompileFailed");
});
