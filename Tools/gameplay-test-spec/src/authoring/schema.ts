import { z } from "zod";

export const AuthoringKindSchema = z.enum([
  "run-map", "event", "treasure", "encounter", "battle-layout", "ai", "skill", "unit", "presentation"
]);
export type AuthoringKind = z.infer<typeof AuthoringKindSchema>;

const LayoutNodeSchema = z.object({ nodeId: z.string().min(1), x: z.number().finite(), y: z.number().finite() }).strict();
export const AuthoringGraphLayoutSchema = z.object({
  layoutSchemaVersion: z.literal(1).default(1),
  nodes: z.array(LayoutNodeSchema).default([])
}).strict();

const OutcomeSchema = z.object({
  type: z.enum(["Gold", "Damage", "Item", "Buff", "Debuff", "Nothing"]),
  target: z.enum(["All", "Self"]), amount: z.number().int().nonnegative(),
  itemId: z.string().min(1).optional(), description: z.string().default("")
}).strict().superRefine((value, context) => {
  const requiresId = value.type === "Item" || value.type === "Buff" || value.type === "Debuff";
  if (requiresId !== Boolean(value.itemId)) context.addIssue({ code: z.ZodIssueCode.custom, message: `${value.type} ContentId presence is invalid.` });
});

const EventGraphOptionSchema = z.object({
  id: z.string().min(1), text: z.string().min(1),
  check: z.object({ attribute: z.enum(["None", "Strength", "Agility", "Constitution", "Intelligence", "Charisma", "Luck"]), baseSuccessRate: z.number().int().min(0).max(100) }).strict(),
  success: OutcomeSchema, failure: OutcomeSchema.nullable().optional()
}).strict();

export const EventGraphAssetSchema = z.object({
  sourceId: z.string().min(1), title: z.string().min(1), description: z.string().default(""),
  options: z.array(EventGraphOptionSchema).min(1), graphLayout: AuthoringGraphLayoutSchema.optional()
}).strict().superRefine((value, context) => {
  const allowed = new Set(["start", "end", ...value.options.flatMap(option => ["option", "check", "success", "failure"].map(role => `${role}:${option.id}`))]);
  for (const [index, node] of (value.graphLayout?.nodes ?? []).entries()) if (!allowed.has(node.nodeId))
    context.addIssue({ code: z.ZodIssueCode.custom, message: `Unknown Event layout node '${node.nodeId}'.`, path: ["graphLayout", "nodes", index, "nodeId"] });
});

const ContentIdDocument = z.object({ contentId: z.string().min(1), schemaVersion: z.number().int().positive() }).passthrough();
const CellSchema = z.object({ x: z.number().int(), y: z.number().int() }).strict();
const FlatEventOptionSchema = z.object({ id: z.string().min(1), text: z.string().min(1), attribute: z.string().min(1), baseSuccessRate: z.number().int().min(0).max(100), success: OutcomeSchema, failure: OutcomeSchema.nullable() }).strict();
const TreasureEntrySchema = z.object({ kind: z.enum(["Equipment", "Consumable", "Buff"]), contentId: z.string().min(1), weight: z.number().int().positive() }).strict();
const AiCurveKeySchema = z.object({ time: z.number().finite(), value: z.number().finite(), inSlope: z.number().finite(), outSlope: z.number().finite() }).strict();
const AiNodeSchema = z.object({ nodeId: z.string().min(1), kind: z.enum(["Intent", "Rule", "Score"]), type: z.string().min(1), enabled: z.boolean(), parameter: z.number().finite(), x: z.number().finite(), y: z.number().finite(), curve: z.array(AiCurveKeySchema) }).strict();
const AiEdgeSchema = z.object({ sourceNodeId: z.string().min(1), targetNodeId: z.string().min(1) }).strict();
const SkillExecutionProfileSchema = z.object({ areaRadius: z.number().int().nonnegative(), orderedTargetCount: z.number().int().nonnegative(), summonDefinitionId: z.string().min(1).nullable(), summonCount: z.number().int().nonnegative(), summonLimit: z.number().int().nonnegative(), summonCategory: z.string(), requiresCorpse: z.boolean(), ignoreLineOfSight: z.boolean(), shieldMultiplier: z.number().int().nonnegative(), shieldAbsorbsAllDamage: z.boolean(), cleanseHarmful: z.boolean(), secondaryDamage: z.number().int().nonnegative(), areaShape: z.string(), statusChancePercent: z.number().int().min(0).max(100), detonateStatusContentId: z.string().min(1).nullable(), bounceRange: z.number().int().nonnegative(), bounceCount: z.number().int().nonnegative(), pierceAll: z.boolean(), allowsEmptyTarget: z.boolean(), movementDamagePerCell: z.number().int().nonnegative(), summonAttackContentId: z.string().min(1).nullable(), corruptionCost: z.number().int().nonnegative(), damageScaling: z.string().min(1), lifeStealPercent: z.number().int().min(0).max(100).default(0) }).strict();
const PresentationValueSchema = z.object({ kind: z.enum(["String", "Integer", "Number", "Boolean", "Color", "Vector2"]), value: z.string() }).strict();
const DocumentSchemas: Record<AuthoringKind, z.ZodTypeAny> = {
  "run-map": ContentIdDocument.extend({ layoutVersion: z.number().int().positive(), nodes: z.array(z.object({ nodeId: z.string().min(1), layer: z.number().int().nonnegative(), kind: z.string().min(1), contentId: z.string(), title: z.string(), lane: z.number().int() }).strict()), connections: z.array(z.object({ from: z.string().min(1), to: z.string().min(1) }).strict()) }),
  event: ContentIdDocument.extend({ sourceId: z.string(), title: z.string().min(1), description: z.string(), options: z.array(FlatEventOptionSchema).min(1), graphLayout: AuthoringGraphLayoutSchema.optional() }),
  treasure: ContentIdDocument.extend({ goldMinimum: z.number().int().nonnegative(), goldMaximum: z.number().int().nonnegative(), entries: z.array(TreasureEntrySchema), graphLayout: AuthoringGraphLayoutSchema.optional() }),
  encounter: ContentIdDocument.extend({ layoutContentId: z.string().min(1), monsterUnitContentIds: z.array(z.string().min(1)), monsterAiContentIds: z.array(z.string().min(1)), healthMultiplier: z.number().positive(), outputMultiplier: z.number().positive(), minimumStartingMana: z.number().int().nonnegative(), encounterClass: z.string().min(1) }),
  "battle-layout": ContentIdDocument.extend({ partySpawns: z.array(CellSchema).min(1), enemySpawns: z.array(CellSchema).min(1), blockedCells: z.array(CellSchema), shallowWaterCells: z.array(CellSchema).default([]) }),
  ai: ContentIdDocument.extend({ archetype: z.string().min(1), skillContentIds: z.array(z.string().min(1)), patternSkillContentIds: z.array(z.string().min(1)), distanceWeight: z.number().finite(), damageWeight: z.number().finite(), targetCountWeight: z.number().finite(), harmfulStatusWeight: z.number().finite(), maximumEngageCandidatesPerTarget: z.number().int().positive(), preferredMinimumRange: z.number().int().nonnegative(), preferredMaximumRange: z.number().int().nonnegative(), preferredRangeRepositionBonus: z.number().finite(), sourceSha256: z.string().min(1), nodes: z.array(AiNodeSchema).min(1), edges: z.array(AiEdgeSchema) }),
  skill: ContentIdDocument.extend({ sourceId: z.string().min(1), displayName: z.string().min(1), description: z.string(), role: z.string().min(1), kind: z.string().min(1), level: z.number().int().positive(), manaCost: z.number().int().nonnegative(), minRange: z.number().int().nonnegative(), maxRange: z.number().int().nonnegative(), executionKind: z.string().min(1), damage: z.number().int().nonnegative(), damageKind: z.string().min(1), statusContentId: z.string().min(1).nullable(), statusDuration: z.number().int().nonnegative(), hidden: z.boolean(), externalDependency: z.boolean(), isBasicAbility: z.boolean(), maxUsesPerTurn: z.number().int().nonnegative(), canCrit: z.boolean(), branchId: z.string(), prerequisiteContentId: z.string().min(1).nullable(), prerequisiteBranchId: z.string(), growthVisible: z.boolean(), requiredAttribute: z.string(), minimumAttribute: z.number().int().nonnegative(), executionProfile: SkillExecutionProfileSchema, sourceKind: z.enum(["FrozenMigration", "GodotAuthored"]), sourcePath: z.string(), sourceGuid: z.string(), sourceLocalFileId: z.number().int(), graphPath: z.string(), graphDependencyHash: z.string() }),
  unit: ContentIdDocument.extend({ sourceId: z.string().min(1), displayName: z.string().min(1), familyId: z.string().min(1), roleId: z.string().min(1), maxHealth: z.number().int().positive(), maxMana: z.number().int().nonnegative(), startingMana: z.number().int().nonnegative(), moveRange: z.number().int().nonnegative(), speed: z.number().nonnegative(), initiative: z.number().nonnegative(), movementKind: z.enum(["land", "air", "swim"]), canProduceCorpse: z.boolean(), assetRoles: z.object({ idleDownRight: z.string().min(1), idleUpLeft: z.string().min(1), death: z.string().min(1), meleeDownRight: z.string().min(1), meleeUpLeft: z.string().min(1), hitDownRight: z.string().min(1), hitUpLeft: z.string().min(1) }).strict() }),
  presentation: ContentIdDocument.extend({ resourceClass: z.string().min(1), properties: z.record(PresentationValueSchema) })
};

export const AuthoringAssetSpecSchema = z.object({
  operation: z.enum(["create", "update", "duplicate", "delete"]),
  kind: AuthoringKindSchema, contentId: z.string().min(1), sourceContentId: z.string().min(1).optional(),
  expectedRevision: z.string().min(1).optional(), expectedReferenceRevision: z.string().min(1).optional(),
  dependencies: z.array(z.string().min(1)).default([]), document: z.record(z.unknown()).optional(),
  eventGraph: EventGraphAssetSchema.optional()
}).strict().superRefine((value, context) => {
  if ((value.operation === "create" || value.operation === "update") && !value.document && !value.eventGraph)
    context.addIssue({ code: z.ZodIssueCode.custom, message: `${value.operation} requires document or eventGraph.` });
  if (value.operation === "update" && !value.expectedRevision)
    context.addIssue({ code: z.ZodIssueCode.custom, message: "update requires expectedRevision." });
  if (value.operation === "duplicate" && !value.sourceContentId)
    context.addIssue({ code: z.ZodIssueCode.custom, message: "duplicate requires sourceContentId." });
  if (value.operation === "delete" && !value.expectedReferenceRevision)
    context.addIssue({ code: z.ZodIssueCode.custom, message: "delete requires expectedReferenceRevision." });
  if (value.eventGraph && value.kind !== "event")
    context.addIssue({ code: z.ZodIssueCode.custom, message: "eventGraph is only valid for event assets." });
  if (value.operation === "delete" && (value.document || value.eventGraph))
    context.addIssue({ code: z.ZodIssueCode.custom, message: "delete cannot carry document or eventGraph content." });
  if (value.document && value.operation !== "delete") {
    const document = DocumentSchemas[value.kind].safeParse(value.document);
    for (const issue of document.success ? [] : document.error.issues)
      context.addIssue({ code: z.ZodIssueCode.custom, message: `${value.kind} document: ${issue.message}`, path: ["document", ...issue.path] });
    if ((value.document as Record<string, unknown>).contentId !== value.contentId)
      context.addIssue({ code: z.ZodIssueCode.custom, message: "document ContentId must match asset ContentId.", path: ["document", "contentId"] });
  }
});

export const AuthoringAssetBatchSpecSchema = z.object({
  schemaVersion: z.union([z.literal(1), z.literal(2)]), assets: z.array(AuthoringAssetSpecSchema).min(1)
}).strict();

export type AuthoringAssetSpecV1 = z.infer<typeof AuthoringAssetBatchSpecSchema>;

export interface AuthoringCompilerDiagnostic { code: string; severity: "error" | "warning"; message: string; path?: string }
export interface CompiledAuthoringBatchV1 {
  schemaVersion: 1 | 2;
  changes: Array<{ kind: AuthoringKind; contentId: string; expectedRevision: string; snapshot: string }>;
  lifecycle: Array<{ operation: "create" | "duplicate" | "delete"; contentId: string; sourceContentId?: string; resourceType: AuthoringKind; expectedReferenceRevision?: string; initialSnapshot?: string }>;
}
