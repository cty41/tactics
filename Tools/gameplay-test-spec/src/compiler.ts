import type { Adapter, ExecutableScenarioPlan, ProbeRequestSchema, RuntimeTarget, ScenarioAssertion, ScenarioDraft, ScenarioSpec, ScenarioStep } from "./schema.js";
import { ExecutableScenarioPlanSchema, GodotExecutableScenarioPlanSchema, ScenarioDraftSchema, type ExpectationDiagnostic } from "./schema.js";
import { validateScenarioSpec, validateScenarioDraft } from "./validator.js";
import { requiredCapabilities, validateRuntimeCapabilities } from "./capabilities.js";

// Kind -> adapter 映射表，用于 mixed-adapter 场景的正确路由
const setupKindToAdapter: Record<string, Adapter> = {
  loadValidatedCheckpoint: "Map",
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
  equipInventoryEquipmentToRosterCharacter: "Map",
  initializePlayerInput: "PlayerInput"
};

const actionKindToAdapter: Record<string, Adapter> = {
  endTurnOnlyUntilTerminal: "Battle",
  endTurnUntilPresentationNumber: "Battle",
  restartGodotMain: "UI",
  setPresentationPaused: "UI",
  setPresentationSpeed: "UI",
  bindBattleController: "Battle",
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
  resolveNodeEventOption: "Map",
  applyRestNodeTransaction: "Map",
  buyShopGoodTransaction: "Map",
  commitNodeTransaction: "Map",
  reloadPureRunSession: "Map",
  exercisePureRunSummaryAndDefeat: "Map",
  captureActivePureRun: "Map",
  beginBattleNode: "Map",
  commitNaturalBattleVictory: "Map",
  grantPureRunLevel: "Map",
  commitNodeInteraction: "Map",
  beginNodeTransaction: "Map",
  commitNaturalBattleDefeat: "Map",
  commitEventPartyDefeat: "Map",
  useCarriedConsumable: "Battle",
  openUI: "UI",
  closeUI: "UI",
  clickElement: "UI",
  setText: "UI",
  setElementEnabled: "UI",
  hoverElement: "UI",
  rightClickElement: "UI",
  pressKey: "UI",
  configureLevelUpPanel: "UI",
  refreshBattleActions: "UI",
  refreshInventory: "UI",
  waitForElement: "UI",
  waitForMapReady: "UI",
  spawnCorpse: "Battle",
  killUnit: "Battle",
  spawnInteractableCorpse: "Battle",
  consumeInteractableCorpseAt: "Battle",
  setUnitFacing: "Battle",
  initializeInitiativeOrder: "Battle",
  advanceInitiative: "Battle",
  tickUnitTurnStart: "Battle",
  tickUnitTurnEnd: "Battle",
  registerSummon: "Battle",
  beginOrderedTargetSelection: "Battle",
  selectOrderedTarget: "Battle",
  undoOrderedTargetSelection: "Battle",
  commitOrderedTargetSelection: "Battle",
  cancelOrderedTargetSelection: "Battle",
  bindPureRunAbilityToUnit: "Battle",
  dropAmazonSpear: "Battle",
  clickBattleUnit: "Battle",
  spawnBattleUnit: "Battle",
  restartBattle: "Battle",
  waitForBattleEnd: "Battle",
  initializePlayerInput: "PlayerInput",
  movePointerToTarget: "PlayerInput",
  clickPointerTarget: "PlayerInput",
  rightClickPointerTarget: "PlayerInput",
  pressInputKey: "PlayerInput",
  waitForPlayerObservable: "PlayerInput",
  waitForFrames: "PlayerInput",
  playBattleThroughInput: "PlayerInput",
  useBattleSkillThroughInput: "PlayerInput"
};

const assertionKindToAdapter: Record<string, Adapter> = {
  inventoryProjectionEnteredBattle: "Battle",
  terminalSummaryOutcomeEquals: "Map",
  activeRunExistsEquals: "Map",
  presentationNumberEquals: "UI",
  presentationNodeCountEquals: "UI",
  productionSaveUnchanged: "Map",
  checkpointRevisionEquals: "Map",
  runtimeStateHashEquals: "UI",
  demonboundCorruptionEquals: "Battle",
  battleSkillReceiptEquals: "Battle",
  demonboundPossessedEquals: "Battle",
  adventureActorCellEquals: "Map",
  activeAdventureLeaderEquals: "Map",
  runNodeLifecycleEquals: "Map",
  immediateSuccessorNodeIdsEqual: "Map",
  adventureObjectStateEquals: "Map",
  storeOfferCountEquals: "Map",
  storeSoldOfferCountEquals: "Map",
  backpackContainsContentId: "Map",
  eventResolutionEquals: "Map",
  pendingBattleContextKindEquals: "Map",
  escortStateEquals: "Map",
  protectedNpcAliveEquals: "Map",
  runSaveSchemaVersionEquals: "Map",
  pendingPartyOrderEquals: "Map",
  activePartyStartingSkillIdsEqual: "Map",
  partyAllLivingAtFullResourcesEquals: "Map",
  partyResourceSummaryEquals: "Map",
  runtimeHasNoErrors: "UI",
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
  unitFacingEquals: "Battle",
  currentRoundOrderEquals: "Battle",
  unitStatusStacksEquals: "Battle",
  unitStatusRemainingActionsEquals: "Battle",
  summonOrderEquals: "Battle",
  summonCategoryEquals: "Battle",
  abilityAvailabilityEquals: "Battle",
  abilityAvailabilityReasonEquals: "Battle",
  actualSkillLevelEquals: "Battle",
  unitAbilityListEquals: "Battle",
  orderedTargetSelectionEquals: "Battle",
  selectionStageEquals: "Battle",
  spearHolderEquals: "Battle",
  spearCellEquals: "Battle",
  decoyRemainingActionsEquals: "Battle",
  aiTargetEquals: "Battle",
  interactableCorpseExistsAt: "Battle",
  cellOccupiedByInteractable: "Battle",
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
  mysteryEventIdsUnique: "Map",
  nodeEventIdEquals: "Map",
  nodeTransactionPhaseEquals: "Map",
  nodeTransactionRewardAppliedEquals: "Map",
  transactionApplicationCountEquals: "Map",
  nodeIsConsumed: "Map",
  encounterRecipeContract: "Map",
  monsterAiCatalogValid: "Map",
  battleDefeatRewardsAreZero: "Map",
  completedSummaryGoldEquals: "Map",
  completedSummaryContainsItem: "Map",
  completedSummaryOutcomeEquals: "Map",
  completedSummaryNodesVisitedEquals: "Map",
  completedSummaryEventsCompletedEquals: "Map",
  // UI 独占断言
  elementVisible: "UI",
  elementText: "UI",
  elementEnabled: "UI",
  elementExists: "UI",
  elementClassContains: "UI",
  elementClassContainsAny: "UI",
  elementChildOrderEquals: "UI",
  elementRectRelationEquals: "UI",
  abilityCardAvailabilityEquals: "UI",
  targetMarkerOrderEquals: "UI"
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

export interface CompileOptions {
  runtime?: RuntimeTarget;
}

export function compileScenarioSpec(input: unknown, options: CompileOptions = {}): CompileResult {
  const validation = validateScenarioSpec(input);
  if (!validation.valid || !validation.spec) {
    return {
      valid: false,
      diagnostics: validation.diagnostics
    };
  }

  const spec = validation.spec;
  return compileSpecToPlan(spec, validation.diagnostics, options);
}

export function compileScenarioDraft(draft: unknown, options: CompileOptions = {}): CompileResult {
  const validation = validateScenarioDraft(draft);
  if (!validation.valid || !validation.spec) {
    return {
      valid: false,
      diagnostics: validation.diagnostics
    };
  }

  const spec = validation.spec;
  return compileSpecToPlan(spec, validation.diagnostics, options);
}

function compileSpecToPlan(spec: ScenarioSpec, diagnostics: ExpectationDiagnostic[], options: CompileOptions): CompileResult {
  const fallbackAdapter = spec.requiredAdapters[0];
  const probeRequests = deriveProbeRequests(spec, fallbackAdapter);
  const runtime = options.runtime ?? "Unity";

  const runtimeDiagnostics: ExpectationDiagnostic[] = validateRuntimeCapabilities(runtime, spec.setup, spec.actions, spec.assertions);
  if (runtime === "Godot") {
    const adapterDiagnostics = [
      ...validateGodotAdapters(spec.setup, setupKindToAdapter, spec.requiredAdapters),
      ...validateGodotAdapters(spec.actions, actionKindToAdapter, spec.requiredAdapters),
      ...validateGodotAdapters(spec.assertions, assertionKindToAdapter, spec.requiredAdapters)
    ];
    runtimeDiagnostics.push(...adapterDiagnostics);
  }
  if (runtimeDiagnostics.length > 0) return { valid: false, diagnostics: [...diagnostics, ...runtimeDiagnostics] };

  if (runtime === "Godot") {
    const checkpointStep = spec.setup.find(value => value.kind === "loadValidatedCheckpoint");
    const checkpoint = checkpointStep ? {
      id: String(checkpointStep.parameters.id),
      source: "validated_checkpoint" as const,
      semanticHash: String(checkpointStep.parameters.semanticHash),
      path: String(checkpointStep.parameters.path)
    } : undefined;
    const plan = {
      schemaVersion: spec.assertions.some(assertion => assertion.kind === "battleSkillReceiptEquals" ||
        assertion.kind === "demonboundCorruptionEquals" ||
        assertion.kind === "demonboundPossessedEquals" || assertion.kind.startsWith("adventure") ||
        assertion.kind === "activeAdventureLeaderEquals" || assertion.kind === "runNodeLifecycleEquals" ||
        assertion.kind === "immediateSuccessorNodeIdsEqual" || assertion.kind === "storeOfferCountEquals" ||
        assertion.kind === "storeSoldOfferCountEquals" || assertion.kind === "backpackContainsContentId" ||
        assertion.kind === "eventResolutionEquals" || assertion.kind === "pendingBattleContextKindEquals" ||
        assertion.kind === "escortStateEquals" || assertion.kind === "protectedNpcAliveEquals" ||
        assertion.kind === "runSaveSchemaVersionEquals" || assertion.kind === "pendingPartyOrderEquals" ||
        assertion.kind === "activePartyStartingSkillIdsEqual" || assertion.kind === "partyAllLivingAtFullResourcesEquals" ||
        assertion.kind === "partyResourceSummaryEquals") ? 3 as const : 2 as const,
      runtime: "Godot" as const,
      scenarioName: `${spec.feature}.${spec.scenario}`,
      requiredAdapters: spec.requiredAdapters,
      requiredCapabilities: requiredCapabilities(spec.setup, spec.actions, spec.assertions),
      setupActions: spec.setup.map(step => ({ ...step, adapter: resolveAdapter(step, setupKindToAdapter, fallbackAdapter) })),
      runtimeActions: spec.actions.map(step => ({ ...step, adapter: resolveAdapter(step, actionKindToAdapter, fallbackAdapter) })),
      assertionPlans: spec.assertions.map(assertion => ({ ...assertion, adapter: resolveAdapter(assertion, assertionKindToAdapter, fallbackAdapter) })),
      timeoutMs: spec.timeoutMs,
      probeRequests,
      checkpoint,
      saveIsolation: { root: "user://qa-runner", protectProductionSave: true as const },
      watchdog: {
        stepTimeoutMs: Math.min(120000, Math.max(30000, Math.ceil(spec.timeoutMs / 10))),
        battleRoundLimit: 80,
        scenarioTimeoutMs: Math.max(300000, spec.timeoutMs),
        noProgressLimit: 2
      }
    };
    const parsed = GodotExecutableScenarioPlanSchema.safeParse(plan);
    if (!parsed.success) return { valid: false, diagnostics: parsed.error.issues.map(issue => ({
      code: "PlanSchemaValidationFailed", severity: "error", message: issue.message, path: issue.path.join(".")
    })) };
    return { plan: parsed.data, diagnostics, valid: true };
  }

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

function validateGodotAdapters(
  items: Array<ScenarioStep | ScenarioAssertion>,
  kindMap: Record<string, Adapter>,
  requiredAdapters: Adapter[]
): ExpectationDiagnostic[] {
  const result: ExpectationDiagnostic[] = [];
  for (const item of items) {
    const canonical = kindMap[item.kind];
    if (!canonical) continue;
    if (item.adapter && item.adapter !== canonical) result.push({
      code: "RuntimeAdapterMismatch", severity: "error",
      message: `${item.kind} must use the ${canonical} adapter for the Godot runtime.`, path: item.id ?? item.kind
    });
    if (!requiredAdapters.includes(canonical)) result.push({
      code: "MissingRequiredRuntimeAdapter", severity: "error",
      message: `${item.kind} requires ${canonical} in requiredAdapters.`, path: item.id ?? item.kind
    });
  }
  return result;
}

function deriveProbeRequests(spec: ScenarioSpec, fallback: Adapter): ExecutableScenarioPlan["probeRequests"] {
  return spec.assertions.map(assertion => ({
    adapter: assertion.adapter ?? assertionKindToAdapter[assertion.kind] ?? fallback,
    kind: assertion.kind,
    target: assertion.target,
    parameters: assertion.parameters
  }));
}
