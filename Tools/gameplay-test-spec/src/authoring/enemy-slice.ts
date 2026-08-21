import { z } from "zod";
import { compileAuthoringSpec } from "./compiler.js";

const Cell = z.object({ x: z.number().int().min(0).max(9), y: z.number().int().min(0).max(9) }).strict();
const AssetRoles = z.object({
  idleDownRight: z.string().min(1), idleUpLeft: z.string().min(1), death: z.string().min(1),
  meleeDownRight: z.string().min(1), meleeUpLeft: z.string().min(1),
  hitDownRight: z.string().min(1), hitUpLeft: z.string().min(1)
}).strict();

export const EnemySliceDraftSchema = z.object({
  schemaVersion: z.literal(1),
  unit: z.object({ contentId: z.string().min(1), sourceId: z.string().min(1), displayName: z.string().min(1),
    familyId: z.string().min(1), roleId: z.string().min(1), maxHealth: z.number().int().positive(),
    maxMana: z.number().int().nonnegative(), startingMana: z.number().int().nonnegative(),
    moveRange: z.number().int().nonnegative(), speed: z.number().nonnegative(), initiative: z.number().nonnegative(),
    movementKind: z.enum(["land", "air", "swim"]), canProduceCorpse: z.boolean(), assetRoles: AssetRoles }).strict(),
  skill: z.object({ contentId: z.string().min(1), sourceId: z.string().min(1), displayName: z.string().min(1),
    description: z.string(), damage: z.number().int().nonnegative(), minRange: z.number().int().nonnegative(),
    maxRange: z.number().int().nonnegative(), maxUsesPerTurn: z.number().int().positive(), canCrit: z.boolean(),
    lifeStealPercent: z.number().int().min(0).max(100) }).strict(),
  ai: z.object({ contentId: z.string().min(1), archetype: z.literal("PredatoryDiver"), skillContentIds: z.array(z.string().min(1)).min(1), maximumEngageCandidatesPerTarget: z.number().int().positive().default(3) }).strict(),
  layout: z.object({ contentId: z.string().min(1), partySpawns: z.array(Cell).min(1), enemySpawns: z.array(Cell).min(1), blockedCells: z.array(Cell), shallowWaterCells: z.array(Cell) }).strict(),
  encounter: z.object({ contentId: z.string().min(1), monsterUnitContentIds: z.array(z.string().min(1)).min(1), monsterAiContentIds: z.array(z.string().min(1)).min(1) }).strict()
}).strict();
export type EnemySliceDraft = z.infer<typeof EnemySliceDraftSchema>;
export interface CatalogRevision { contentId: string; revision: string }

export function projectEnemySliceDraft(input: unknown, catalog: readonly CatalogRevision[], approvedAssets: readonly string[]) {
  const draft = EnemySliceDraftSchema.parse(input);
  const allowed = new Set(approvedAssets);
  for (const path of Object.values(draft.unit.assetRoles)) if (!allowed.has(path))
    throw new Error(`Presentation asset '${path}' is not in the approved allowlist.`);
  if (draft.encounter.monsterUnitContentIds.length !== draft.encounter.monsterAiContentIds.length)
    throw new Error("Encounter unit and AI bindings must be aligned.");
  const revisions = new Map(catalog.map(value => [value.contentId, value.revision]));
  const profile = { areaRadius: 0, orderedTargetCount: 0, summonDefinitionId: null, summonCount: 0,
    summonLimit: 0, summonCategory: "", requiresCorpse: false, ignoreLineOfSight: false,
    shieldMultiplier: 0, shieldAbsorbsAllDamage: false, cleanseHarmful: false, secondaryDamage: 0,
    areaShape: "", statusChancePercent: 100, detonateStatusContentId: null, bounceRange: 0,
    bounceCount: 0, pierceAll: false, allowsEmptyTarget: false, movementDamagePerCell: 0,
    summonAttackContentId: null, corruptionCost: 0, damageScaling: "None", lifeStealPercent: draft.skill.lifeStealPercent };
  const documents = [
    { kind: "unit", contentId: draft.unit.contentId, dependencies: ["packed-scene.unit-actor"], document: { ...draft.unit, schemaVersion: 1 } },
    { kind: "skill", contentId: draft.skill.contentId, dependencies: [], document: { ...draft.skill, schemaVersion: 1,
      role: "Any", kind: "Active", level: 1, manaCost: 0, executionKind: "DirectAttack", damageKind: "Physical",
      statusContentId: null, statusDuration: 0, hidden: false, externalDependency: false, isBasicAbility: false,
      branchId: "enemy.maw-bat-bite", prerequisiteContentId: null, prerequisiteBranchId: "", growthVisible: true,
      requiredAttribute: "", minimumAttribute: 0, executionProfile: profile, sourceKind: "GodotAuthored",
      sourcePath: "", sourceGuid: "", sourceLocalFileId: 0, graphPath: "", graphDependencyHash: "" } },
    { kind: "ai", contentId: draft.ai.contentId, dependencies: draft.ai.skillContentIds, document: { ...draft.ai,
      schemaVersion: 1, patternSkillContentIds: [], distanceWeight: 1, damageWeight: 1, targetCountWeight: 0,
      harmfulStatusWeight: 0, preferredMinimumRange: 1, preferredMaximumRange: 1, preferredRangeRepositionBonus: 0,
      sourceSha256: "deterministic:predatory-diver-v1", nodes: [
        { nodeId: "intent.attack", kind: "Intent", type: "BasicAttack", enabled: true, parameter: 0, x: 0, y: 0, curve: [] },
        { nodeId: "intent.engage", kind: "Intent", type: "Engage", enabled: true, parameter: 0, x: 0, y: 120, curve: [] }
      ], edges: [] } },
    { kind: "battle-layout", contentId: draft.layout.contentId, dependencies: [], document: { ...draft.layout, schemaVersion: 2 } },
    { kind: "encounter", contentId: draft.encounter.contentId,
      dependencies: [draft.layout.contentId, ...draft.encounter.monsterUnitContentIds, ...draft.encounter.monsterAiContentIds],
      document: { ...draft.encounter, schemaVersion: 1, layoutContentId: draft.layout.contentId,
        healthMultiplier: 1, outputMultiplier: 1, minimumStartingMana: 0, encounterClass: "Normal" } }
  ];
  const assets = documents.map(value => {
    const revision = revisions.get(value.contentId);
    return { ...value, operation: revision ? "update" : "create", ...(revision ? { expectedRevision: revision } : {}) };
  });
  const spec = { schemaVersion: 2 as const, assets };
  const compiled = compileAuthoringSpec(spec);
  if (!compiled.valid) throw new Error(compiled.diagnostics.map(value => value.message).join("; "));
  return { draft, preview: assets.map(value => ({ operation: value.operation, kind: value.kind, contentId: value.contentId, expectedRevision: "expectedRevision" in value ? value.expectedRevision : undefined })), spec, batch: compiled.batch };
}
