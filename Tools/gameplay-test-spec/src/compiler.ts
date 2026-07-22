import type { Adapter, ExecutableScenarioPlan, ProbeRequestSchema, ScenarioAssertion, ScenarioDraft, ScenarioSpec, ScenarioStep } from "./schema.js";
import { ExecutableScenarioPlanSchema, ScenarioDraftSchema, type ExpectationDiagnostic } from "./schema.js";
import { validateScenarioSpec, validateScenarioDraft } from "./validator.js";

// Kind -> adapter 映射表，用于 mixed-adapter 场景的正确路由
const setupKindToAdapter: Record<string, Adapter> = {
  createSkillTestWorld: "Skill",
  createSkillGraph: "Skill",
  createCell: "Skill",
  createUnit: "Skill",
  createSkillAbilityConfig: "Skill",
  createSkillAbility: "Skill",
  setTurnContext: "Skill",
  selectAbility: "Skill",
  bindBattleController: "Battle",
  createAiBrain: "Battle",
  useRealAssets: "Battle",
  loadSkillGraphAsset: "Skill",
  setRunSeed: "Map",
  loadRoguelikeMap: "Map",
  loadPureRunMap: "Map",
  loadTestPartyConfig: "Skill",
  loadTestEncounterConfig: "Skill",
  setAdventureGold: "Map",
  setRosterCharacterState: "Map",
  addInventoryItem: "Map",
  equipInventoryEquipmentToRosterCharacter: "Map"
};

const actionKindToAdapter: Record<string, Adapter> = {
  executeSkillGraph: "Skill",
  executeAbilityOnTarget: "Skill",
  executeAbilityOnCell: "Skill",
  advanceTurn: "Battle",
  endBattleWithResult: "Battle",
  executeBattleSkillGraph: "Battle",
  moveUnit: "Battle",
  setUnitState: "Battle",
  addBuff: "Battle",
  executeAI: "Battle",
  executeAbility: "Battle",
  setRunSeed: "Map",
  enterNode: "Map",
  triggerEvent: "Map",
  completeNode: "Map",
  setAdventureGold: "Map",
  setRosterCharacterState: "Map",
  addInventoryItem: "Map",
  equipInventoryEquipmentToRosterCharacter: "Map",
  applyRestSiteResult: "Map",
  buyShopEquipment: "Map",
  addConsumableInstance: "Map",
  carryConsumableToRosterCharacter: "Map",
  unloadRosterCharacterConsumable: "Map",
  buyShopGood: "Map",
  applyEventResult: "Map",
  useCarriedConsumable: "Battle",
  openUI: "UI",
  closeUI: "UI",
  clickElement: "UI",
  setText: "UI",
  setElementEnabled: "UI"
};

const assertionKindToAdapter: Record<string, Adapter> = {
  // Skill 独占断言
  executionStateEquals: "Skill",
  validationErrorCodeIncludes: "Skill",
  lastErrorContains: "Skill",
  stepMessageContains: "Skill",
  projectileLaunched: "Skill",
  projectileHitTarget: "Skill",
  projectileCompleted: "Skill",
  multiStageStateEquals: "Skill",
  // Battle 独占断言
  battleIsActive: "Battle",
  currentRoundEquals: "Battle",
  battleResultEquals: "Battle",
  playerNumberEquals: "Battle",
  unitCountEquals: "Battle",
  unitCanAct: "Battle",
  unitCanReceiveHealingEquals: "Battle",
  unitDoesNotHaveBuff: "Battle",
  aiSelectedIntentTypeEquals: "Battle",
  aiCandidateCountEquals: "Battle",
  aiRuleFilteredCountEquals: "Battle",
  aiTurnSucceededEquals: "Battle",
  aiTurnAbilityEquals: "Battle",
  aiTurnDestinationEquals: "Battle",
  aiTurnTargetPointEquals: "Battle",
  aiTurnTargetCountEquals: "Battle",
  aiTurnUsedFallbackEquals: "Battle",
  aiTurnPatternStepEquals: "Battle",
  // Map 独占断言
  currentNodeEquals: "Map",
  mapIsActive: "Map",
  visitedNodeCountEquals: "Map",
  battleVictoryCountEquals: "Map",
  nodeTypeEquals: "Map",
  nodeIsReachable: "Map",
  nodeIsVisited: "Map",
  runGoldEquals: "Map",
  rosterCharacterHpEquals: "Map",
  rosterCharacterMpEquals: "Map",
  rosterCharacterDeadEquals: "Map",
  rosterCharacterExperienceEquals: "Map",
  rosterCharacterLevelEquals: "Map",
  rosterCharacterHasSkillId: "Map",
  rosterCharacterSkillLevelEquals: "Map",
  pureRunSkillChoiceContains: "Map",
  pureRunSkillChoicesAreMixed: "Map",
  rosterCharacterEquipmentEquals: "Map",
  rosterCharacterTotalAttributeEquals: "Map",
  runtimeRosterCharacterHasPendingBuff: "Map",
  rosterCharacterHasPendingBuff: "Map",
  rosterCharacterPendingBuffHasIcon: "Map",
  inventoryContains: "Map",
  consumableCountEquals: "Map",
  rosterCharacterCarriedConsumableEquals: "Map",
  backpackConsumableCountEquals: "Map",
  consumableInstanceExists: "Map",
  shopGoodCountEquals: "Map",
  shopConsumableCountAtLeast: "Map",
  shopConsumableIdsUnique: "Map",
  // UI 独占断言
  elementVisible: "UI",
  elementText: "UI",
  elementEnabled: "UI",
  elementExists: "UI"
  // 注意：unitHealthEquals / unitManaEquals / unitAliveEquals / unitPositionEquals 等
  // 共享断言不在此映射中，会回退到 requiredAdapters[0]，由 spec 上下文决定
};

function resolveAdapter(step: ScenarioStep | ScenarioAssertion, kindMap: Record<string, Adapter>, fallback: Adapter): Adapter {
  // 优先使用显式指定的 adapter
  if (step.adapter) return step.adapter;
  // 其次使用 kind 映射表
  if (kindMap[step.kind]) return kindMap[step.kind];
  // 最后使用 fallback
  return fallback;
}

export interface CompileResult {
  plan?: ExecutableScenarioPlan;
  diagnostics: ExpectationDiagnostic[];
  valid: boolean;
}

export function compileScenarioSpec(input: unknown): CompileResult {
  const validation = validateScenarioSpec(input);
  if (!validation.valid || !validation.spec) {
    return {
      valid: false,
      diagnostics: validation.diagnostics
    };
  }

  const spec = validation.spec;
  return compileSpecToPlan(spec, validation.diagnostics);
}

export function compileScenarioDraft(draft: unknown): CompileResult {
  const validation = validateScenarioDraft(draft);
  if (!validation.valid || !validation.spec) {
    return {
      valid: false,
      diagnostics: validation.diagnostics
    };
  }

  const spec = validation.spec;
  return compileSpecToPlan(spec, validation.diagnostics);
}

function compileSpecToPlan(spec: ScenarioSpec, diagnostics: ExpectationDiagnostic[]): CompileResult {
  const fallbackAdapter = spec.requiredAdapters[0];
  const probeRequests = deriveProbeRequests(spec, fallbackAdapter);

  const plan: ExecutableScenarioPlan = {
    schemaVersion: 1,
    scenarioName: `${spec.feature}.${spec.scenario}`,
    requiredAdapters: spec.requiredAdapters,
    setupActions: spec.setup.map(step => ({ ...step, adapter: resolveAdapter(step, setupKindToAdapter, fallbackAdapter) })),
    runtimeActions: spec.actions.map(step => ({ ...step, adapter: resolveAdapter(step, actionKindToAdapter, fallbackAdapter) })),
    assertionPlans: spec.assertions.map(assertion => ({ ...assertion, adapter: resolveAdapter(assertion, assertionKindToAdapter, fallbackAdapter) })),
    timeoutMs: spec.timeoutMs,
    probeRequests
  };

  const parsed = ExecutableScenarioPlanSchema.safeParse(plan);
  if (!parsed.success) {
    return {
      valid: false,
      diagnostics: parsed.error.issues.map(issue => ({
        code: "PlanSchemaValidationFailed",
        severity: "error",
        message: issue.message,
        path: issue.path.join(".")
      }))
    };
  }

  return {
    plan: parsed.data,
    diagnostics,
    valid: true
  };
}

function deriveProbeRequests(spec: ScenarioSpec, fallback: Adapter): ExecutableScenarioPlan["probeRequests"] {
  return spec.assertions.map(assertion => ({
    adapter: assertion.adapter ?? assertionKindToAdapter[assertion.kind] ?? fallback,
    kind: assertion.kind,
    target: assertion.target,
    parameters: assertion.parameters
  }));
}
