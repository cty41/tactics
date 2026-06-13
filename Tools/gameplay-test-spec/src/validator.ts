import { z } from "zod";
import { ScenarioSpecSchema, ScenarioDraftSchema, type ExpectationDiagnostic, type ScenarioAssertion, type ScenarioSpec, type ScenarioStep, type ScenarioDraft } from "./schema.js";

const supportedSetupKinds = new Set([
  "createSkillTestWorld",
  "createSkillGraph",
  "createCell",
  "createUnit",
  "createSkillAbilityConfig",
  "createSkillAbility",
  "setTurnContext",
  "selectAbility"
]);

const supportedActionKinds = new Set([
  "executeSkillGraph",
  "executeAbilityOnTarget",
  "executeAbilityOnCell"
]);

const supportedGraphKinds = new Set([
  "selfHeal",
  "singleTargetDamage",
  "invalidSelfHeal",
  "areaDamage",
  "knockback",
  "allyHeal",
  "applyBuff",
  "charge"
]);

const supportedAssertionKinds = new Set([
  "executionStateEquals",
  "validationErrorCodeIncludes",
  "unitHealthEquals",
  "unitManaEquals",
  "unitHasBuff",
  "unitBuffDurationEquals",
  "unitBuffCountEquals",
  "unitBuffIsUnique",
  "unitCellEquals",
  "unitCountInArea",
  "lastErrorContains",
  "stepMessageContains",
  "projectileLaunched",
  "projectileHitTarget",
  "projectileCompleted",
  "multiStageStateEquals"
]);

interface AliasState {
  graphs: Set<string>;
  cells: Set<string>;
  units: Set<string>;
  abilityConfigs: Set<string>;
  abilities: Set<string>;
}

export interface ValidationResult {
  spec?: ScenarioSpec;
  diagnostics: ExpectationDiagnostic[];
  valid: boolean;
}

export function validateScenarioSpec(input: unknown): ValidationResult {
  const parsed = ScenarioSpecSchema.safeParse(input);
  if (!parsed.success) {
    return {
      valid: false,
      diagnostics: parsed.error.issues.map(issue => ({
        code: "SchemaValidationFailed",
        severity: "error",
        message: issue.message,
        path: issue.path.join(".")
      }))
    };
  }

  const spec = parsed.data;
  const diagnostics: ExpectationDiagnostic[] = [];
  const state = createAliasState();

  for (const step of spec.setup) {
    validateStepKind(step, supportedSetupKinds, "UnsupportedSetupKind", diagnostics);
    validateSetupStep(step, state, diagnostics);
  }

  for (const action of spec.actions) {
    validateStepKind(action, supportedActionKinds, "UnsupportedActionKind", diagnostics);
    validateActionStep(action, state, diagnostics);
  }

  for (const assertion of spec.assertions) {
    validateAssertion(assertion, state, diagnostics);
  }

  if (!spec.requiredAdapters.includes("Skill")) {
    diagnostics.push({
      code: "MissingSkillAdapter",
      severity: "error",
      message: "MVP scenarios must include the Skill adapter."
    });
  }

  validateSemanticRules(spec, state, diagnostics);

  return {
    spec,
    diagnostics,
    valid: diagnostics.every(diagnostic => diagnostic.severity !== "error")
  };
}

export function validateScenarioDraft(draft: unknown): ValidationResult {
  const parsed = ScenarioDraftSchema.safeParse(draft);
  if (!parsed.success) {
    return {
      valid: false,
      diagnostics: parsed.error.issues.map(issue => ({
        code: "DraftSchemaValidationFailed",
        severity: "error",
        message: issue.message,
        path: issue.path.join(".")
      }))
    };
  }

  const draftData = parsed.data;
  const spec: ScenarioSpec = {
    feature: draftData.feature,
    scenario: draftData.scenario,
    tags: draftData.tags,
    requiredAdapters: draftData.requiredAdapters.length > 0 ? draftData.requiredAdapters : ["Skill"],
    setup: draftData.setup.map(s => ({ kind: s.kind, parameters: s.parameters })),
    actions: draftData.actions.map(a => ({ kind: a.kind, target: a.target, parameters: a.parameters })),
    assertions: draftData.assertions.map(a => ({ kind: a.kind, target: a.target, expected: a.expected, parameters: a.parameters })),
    timeoutMs: draftData.timeoutMs
  };

  return validateScenarioSpec(spec);
}

function createAliasState(): AliasState {
  return {
    graphs: new Set<string>(),
    cells: new Set<string>(),
    units: new Set<string>(),
    abilityConfigs: new Set<string>(),
    abilities: new Set<string>()
  };
}

function validateStepKind(
  step: ScenarioStep,
  supportedKinds: Set<string>,
  code: string,
  diagnostics: ExpectationDiagnostic[]
): void {
  if (supportedKinds.has(step.kind)) {
    return;
  }

  diagnostics.push({
    code,
    severity: "error",
    message: `Unsupported ${step.id ? "step" : "item"} kind '${step.kind}'.`,
    path: step.id ?? step.kind
  });
}

function validateSetupStep(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  switch (step.kind) {
    case "createSkillTestWorld":
      resetAliasState(state);
      break;
    case "createSkillGraph":
      validateGraphStep(step, state, diagnostics);
      break;
    case "createCell":
      registerAlias(step, "alias", step.parameters.alias, state.cells, diagnostics, "MissingCellAlias");
      break;
    case "createUnit":
      validateUnitSetup(step, state, diagnostics);
      break;
    case "createSkillAbilityConfig":
      validateAbilityConfigSetup(step, state, diagnostics);
      break;
    case "createSkillAbility":
      validateAbilitySetup(step, state, diagnostics);
      break;
    case "setTurnContext":
      validateTurnContextSetup(step, state, diagnostics);
      break;
    case "selectAbility":
      validateSelectAbilitySetup(step, state, diagnostics);
      break;
    default:
      break;
  }
}

function validateActionStep(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  switch (step.kind) {
    case "executeSkillGraph":
      validateExecuteSkillGraph(step, state, diagnostics);
      break;
    case "executeAbilityOnTarget":
      validateExecuteAbilityOnTarget(step, state, diagnostics);
      break;
    case "executeAbilityOnCell":
      validateExecuteAbilityOnCell(step, state, diagnostics);
      break;
    default:
      break;
  }
}

function validateAssertion(assertion: ScenarioAssertion, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  if (!supportedAssertionKinds.has(assertion.kind)) {
    diagnostics.push({
      code: "UnsupportedAssertionKind",
      severity: "error",
      message: `Unsupported assertion kind '${assertion.kind}'.`,
      path: assertion.id ?? assertion.kind
    });
    return;
  }

  switch (assertion.kind) {
    case "executionStateEquals":
    case "validationErrorCodeIncludes":
    case "lastErrorContains":
    case "stepMessageContains":
      requireStringExpected(assertion, diagnostics, "InvalidAssertionExpectedType");
      break;
    case "unitHealthEquals":
    case "unitManaEquals":
      requireNumberExpected(assertion, diagnostics, "InvalidAssertionExpectedType");
      requireKnownUnit(assertion, state, diagnostics);
      break;
    case "unitHasBuff":
      requireKnownUnit(assertion, state, diagnostics);
      requireBuffName(assertion, diagnostics, "InvalidAssertionExpectedType");
      break;
    case "unitBuffDurationEquals":
      requireKnownUnit(assertion, state, diagnostics);
      requireBuffName(assertion, diagnostics, "InvalidAssertionExpectedType");
      requireIntegerExpected(assertion, diagnostics, "InvalidAssertionExpectedType");
      break;
    case "unitCellEquals":
      requireKnownUnit(assertion, state, diagnostics);
      requireCellCoordinatesExpected(assertion, diagnostics, "InvalidAssertionExpectedType");
      break;
  }
}

function validateGraphStep(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const alias = getString(step.parameters.alias) ?? "graph";
  if (!alias) {
    diagnostics.push({
      code: "MissingGraphAlias",
      severity: "error",
      message: "createSkillGraph requires an alias.",
      path: step.id ?? step.kind
    });
    return;
  }

  const graphKind = getString(step.parameters.graphKind) ?? "";
  if (!supportedGraphKinds.has(graphKind)) {
    diagnostics.push({
      code: "UnsupportedGraphKind",
      severity: "error",
      message: `Unsupported skill graph kind '${graphKind}'.`,
      path: step.id ?? step.kind
    });
  }

  state.graphs.add(alias);
}

function validateUnitSetup(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const alias = getString(step.parameters.alias);
  if (!alias) {
    diagnostics.push({
      code: "MissingUnitAlias",
      severity: "error",
      message: "createUnit requires an alias.",
      path: step.id ?? step.kind
    });
    return;
  }

  const cellAlias = getString(step.parameters.cellAlias);
  if (cellAlias && !state.cells.has(cellAlias)) {
    diagnostics.push({
      code: "UnknownCellAlias",
      severity: "error",
      message: `Cell alias '${cellAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  state.units.add(alias);
}

function validateAbilityConfigSetup(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const alias = getString(step.parameters.alias);
  if (!alias) {
    diagnostics.push({
      code: "MissingAbilityConfigAlias",
      severity: "error",
      message: "createSkillAbilityConfig requires an alias.",
      path: step.id ?? step.kind
    });
    return;
  }

  const graphAlias = getString(step.parameters.graphAlias);
  if (!graphAlias) {
    diagnostics.push({
      code: "MissingGraphAlias",
      severity: "error",
      message: "createSkillAbilityConfig requires a graphAlias.",
      path: step.id ?? step.kind
    });
  }
  else if (!state.graphs.has(graphAlias)) {
    diagnostics.push({
      code: "UnknownGraphAlias",
      severity: "error",
      message: `Skill graph alias '${graphAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  state.abilityConfigs.add(alias);
}

function validateAbilitySetup(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const alias = getString(step.parameters.alias);
  if (!alias) {
    diagnostics.push({
      code: "MissingAbilityAlias",
      severity: "error",
      message: "createSkillAbility requires an alias.",
      path: step.id ?? step.kind
    });
    return;
  }

  const configAlias = getString(step.parameters.configAlias);
  if (!configAlias) {
    diagnostics.push({
      code: "MissingAbilityConfigAlias",
      severity: "error",
      message: "createSkillAbility requires a configAlias.",
      path: step.id ?? step.kind
    });
  }
  else if (!state.abilityConfigs.has(configAlias)) {
    diagnostics.push({
      code: "UnknownAbilityConfigAlias",
      severity: "error",
      message: `Ability config alias '${configAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  const ownerAlias = getString(step.parameters.ownerAlias);
  if (!ownerAlias) {
    diagnostics.push({
      code: "MissingUnitAlias",
      severity: "error",
      message: "createSkillAbility requires an ownerAlias.",
      path: step.id ?? step.kind
    });
  }
  else if (!state.units.has(ownerAlias)) {
    diagnostics.push({
      code: "UnknownUnitAlias",
      severity: "error",
      message: `Unit alias '${ownerAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  state.abilities.add(alias);
}

function validateTurnContextSetup(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const aliases = Array.isArray(step.parameters.playableUnitAliases)
    ? step.parameters.playableUnitAliases
    : [];

  for (const alias of aliases) {
    if (typeof alias !== "string" || !alias) {
      diagnostics.push({
        code: "InvalidPlayableUnitAlias",
        severity: "error",
        message: "playableUnitAliases must only contain unit alias strings.",
        path: step.id ?? step.kind
      });
      continue;
    }

    if (!state.units.has(alias)) {
      diagnostics.push({
        code: "UnknownUnitAlias",
        severity: "error",
        message: `Playable unit alias '${alias}' does not exist.`,
        path: step.id ?? step.kind
      });
    }
  }
}

function validateSelectAbilitySetup(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const abilityAlias = getString(step.parameters.abilityAlias);
  if (!abilityAlias) {
    diagnostics.push({
      code: "MissingAbilityAlias",
      severity: "error",
      message: "selectAbility requires an abilityAlias.",
      path: step.id ?? step.kind
    });
    return;
  }

  if (!state.abilities.has(abilityAlias)) {
    diagnostics.push({
      code: "UnknownAbilityAlias",
      severity: "error",
      message: `Ability alias '${abilityAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }
}

function validateExecuteSkillGraph(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const graphAlias = getString(step.parameters.graphAlias) ?? "graph";
  if (!state.graphs.has(graphAlias)) {
    diagnostics.push({
      code: "UnknownGraphAlias",
      severity: "error",
      message: `Skill graph alias '${graphAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  const casterAlias = getString(step.parameters.casterAlias);
  if (!casterAlias) {
    diagnostics.push({
      code: "MissingUnitAlias",
      severity: "error",
      message: "executeSkillGraph requires a casterAlias.",
      path: step.id ?? step.kind
    });
  }
  else if (!state.units.has(casterAlias)) {
    diagnostics.push({
      code: "UnknownUnitAlias",
      severity: "error",
      message: `Caster alias '${casterAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  const primaryTargetAlias = getString(step.parameters.primaryTargetAlias);
  if (primaryTargetAlias && !state.units.has(primaryTargetAlias)) {
    diagnostics.push({
      code: "UnknownUnitAlias",
      severity: "error",
      message: `Primary target alias '${primaryTargetAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  const targetPointAlias = getString(step.parameters.targetPointAlias);
  if (targetPointAlias && !state.cells.has(targetPointAlias) && !state.units.has(targetPointAlias)) {
    diagnostics.push({
      code: "UnknownCellAlias",
      severity: "error",
      message: `Target point alias '${targetPointAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }
}

function validateExecuteAbilityOnTarget(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const abilityAlias = getString(step.parameters.abilityAlias);
  if (!abilityAlias) {
    diagnostics.push({
      code: "MissingAbilityAlias",
      severity: "error",
      message: "executeAbilityOnTarget requires an abilityAlias.",
      path: step.id ?? step.kind
    });
  }
  else if (!state.abilities.has(abilityAlias)) {
    diagnostics.push({
      code: "UnknownAbilityAlias",
      severity: "error",
      message: `Ability alias '${abilityAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  const targetAlias = getString(step.target) ?? getString(step.parameters.targetAlias);
  if (!targetAlias) {
    diagnostics.push({
      code: "MissingUnitAlias",
      severity: "error",
      message: "executeAbilityOnTarget requires a target alias.",
      path: step.id ?? step.kind
    });
  }
  else if (!state.units.has(targetAlias)) {
    diagnostics.push({
      code: "UnknownUnitAlias",
      severity: "error",
      message: `Target alias '${targetAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }
}

function validateExecuteAbilityOnCell(step: ScenarioStep, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const abilityAlias = getString(step.parameters.abilityAlias);
  if (!abilityAlias) {
    diagnostics.push({
      code: "MissingAbilityAlias",
      severity: "error",
      message: "executeAbilityOnCell requires an abilityAlias.",
      path: step.id ?? step.kind
    });
  }
  else if (!state.abilities.has(abilityAlias)) {
    diagnostics.push({
      code: "UnknownAbilityAlias",
      severity: "error",
      message: `Ability alias '${abilityAlias}' does not exist.`,
      path: step.id ?? step.kind
    });
  }

  const cellAlias = getString(step.target) ?? getString(step.parameters.cellAlias);
  if (cellAlias) {
    if (!state.cells.has(cellAlias)) {
      diagnostics.push({
        code: "UnknownCellAlias",
        severity: "error",
        message: `Cell alias '${cellAlias}' does not exist.`,
        path: step.id ?? step.kind
      });
    }
  }
}

function requireKnownUnit(assertion: ScenarioAssertion, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  if (!assertion.target) {
    diagnostics.push({
      code: "MissingUnitAlias",
      severity: "error",
      message: `${assertion.kind} requires a target unit alias.`,
      path: assertion.id ?? assertion.kind
    });
    return;
  }

  if (!state.units.has(assertion.target)) {
    diagnostics.push({
      code: "UnknownUnitAlias",
      severity: "error",
      message: `Unit alias '${assertion.target}' does not exist.`,
      path: assertion.id ?? assertion.kind
    });
  }
}

function requireStringExpected(assertion: ScenarioAssertion, diagnostics: ExpectationDiagnostic[], code: string): void {
  if (typeof assertion.expected !== "string" || !assertion.expected.trim()) {
    diagnostics.push({
      code,
      severity: "error",
      message: `${assertion.kind} requires a string expected value.`,
      path: assertion.id ?? assertion.kind
    });
  }
}

function requireNumberExpected(assertion: ScenarioAssertion, diagnostics: ExpectationDiagnostic[], code: string): void {
  if (typeof assertion.expected !== "number" || Number.isNaN(assertion.expected)) {
    diagnostics.push({
      code,
      severity: "error",
      message: `${assertion.kind} requires a numeric expected value.`,
      path: assertion.id ?? assertion.kind
    });
  }
}

function requireIntegerExpected(assertion: ScenarioAssertion, diagnostics: ExpectationDiagnostic[], code: string): void {
  if (typeof assertion.expected !== "number" || !Number.isInteger(assertion.expected)) {
    diagnostics.push({
      code,
      severity: "error",
      message: `${assertion.kind} requires an integer expected value.`,
      path: assertion.id ?? assertion.kind
    });
  }
}

function requireBuffName(assertion: ScenarioAssertion, diagnostics: ExpectationDiagnostic[], code: string): void {
  const buffName = getString(assertion.parameters.buffName) ?? (typeof assertion.expected === "string" ? assertion.expected : "");
  if (!buffName) {
    diagnostics.push({
      code,
      severity: "error",
      message: `${assertion.kind} requires a buffName string.`,
      path: assertion.id ?? assertion.kind
    });
  }
}

function requireCellCoordinatesExpected(assertion: ScenarioAssertion, diagnostics: ExpectationDiagnostic[], code: string): void {
  const expected = assertion.expected;
  if (expected == null || typeof expected !== "object" || Array.isArray(expected)) {
    diagnostics.push({
      code,
      severity: "error",
      message: `${assertion.kind} requires { x, y } coordinates as expected value.`,
      path: assertion.id ?? assertion.kind
    });
    return;
  }

  const obj = expected as Record<string, unknown>;
  if (typeof obj.x !== "number" || typeof obj.y !== "number") {
    diagnostics.push({
      code,
      severity: "error",
      message: `${assertion.kind} requires numeric x/y coordinates.`,
      path: assertion.id ?? assertion.kind
    });
  }
}

function getString(value: unknown): string | undefined {
  return typeof value === "string" ? value : undefined;
}

function registerAlias(
  step: ScenarioStep,
  parameterName: string,
  value: unknown,
  aliasSet: Set<string>,
  diagnostics: ExpectationDiagnostic[],
  missingCode: string
): void {
  const alias = getString(value);
  if (!alias) {
    diagnostics.push({
      code: missingCode,
      severity: "error",
      message: `${step.kind} requires a ${parameterName}.`,
      path: step.id ?? step.kind
    });
    return;
  }

  aliasSet.add(alias);
}

function resetAliasState(state: AliasState): void {
  state.graphs.clear();
  state.cells.clear();
  state.units.clear();
  state.abilityConfigs.clear();
  state.abilities.clear();
}

function validateSemanticRules(spec: ScenarioSpec, state: AliasState, diagnostics: ExpectationDiagnostic[]): void {
  const graphKinds = new Map<string, string>();
  for (const step of spec.setup) {
    if (step.kind === "createSkillGraph") {
      const alias = getString(step.parameters.alias) ?? "graph";
      const graphKind = getString(step.parameters.graphKind) ?? "";
      graphKinds.set(alias, graphKind);
    }
  }

  for (const action of spec.actions) {
    if (action.kind === "executeSkillGraph") {
      const graphAlias = getString(action.parameters.graphAlias) ?? "graph";
      const graphKind = graphKinds.get(graphAlias);

      if (graphKind === "areaDamage") {
        const targetPointAlias = getString(action.parameters.targetPointAlias);
        if (!targetPointAlias) {
          diagnostics.push({
            code: "MissingTargetPoint",
            severity: "error",
            message: "areaDamage graph requires a targetPointAlias in executeSkillGraph action.",
            path: action.id ?? action.kind
          });
        }
      }

      if (graphKind === "applyBuff") {
        const graphSetup = spec.setup.find(s =>
          s.kind === "createSkillGraph" &&
          (getString(s.parameters.alias) ?? "graph") === graphAlias
        );
        if (graphSetup) {
          const buffName = getString(graphSetup.parameters.buffName);
          const duration = graphSetup.parameters.duration;
          const selectionKind = getString(graphSetup.parameters.selectionKind);
          if (!buffName) {
            diagnostics.push({
              code: "MissingBuffName",
              severity: "warning",
              message: "applyBuff graph should specify a buffName parameter.",
              path: graphSetup.id ?? graphSetup.kind
            });
          }
          if (duration == null || (typeof duration === "number" && duration <= 0)) {
            diagnostics.push({
              code: "InvalidBuffDuration",
              severity: "warning",
              message: "applyBuff graph should specify a positive duration.",
              path: graphSetup.id ?? graphSetup.kind
            });
          }
          if (!selectionKind) {
            diagnostics.push({
              code: "MissingSelectionKind",
              severity: "warning",
              message: "applyBuff graph should specify a selectionKind (self/enemy/ally).",
              path: graphSetup.id ?? graphSetup.kind
            });
          }
        }
      }
    }
  }

  for (const assertion of spec.assertions) {
    if (assertion.kind === "unitBuffIsUnique" || assertion.kind === "unitBuffCountEquals") {
      if (!assertion.target) {
        diagnostics.push({
          code: "MissingUnitAlias",
          severity: "error",
          message: `${assertion.kind} requires a target unit alias.`,
          path: assertion.id ?? assertion.kind
        });
      }
      const buffName = getString(assertion.parameters.buffName);
      if (!buffName && typeof assertion.expected !== "string") {
        diagnostics.push({
          code: "MissingBuffName",
          severity: "error",
          message: `${assertion.kind} requires a buffName parameter.`,
          path: assertion.id ?? assertion.kind
        });
      }
    }

    if (assertion.kind === "unitCountInArea") {
      const centerAlias = getString(assertion.parameters.centerAlias);
      const radius = assertion.parameters.radius;
      if (!centerAlias) {
        diagnostics.push({
          code: "MissingCenterAlias",
          severity: "error",
          message: "unitCountInArea requires a centerAlias parameter.",
          path: assertion.id ?? assertion.kind
        });
      }
      if (radius == null || typeof radius !== "number" || radius <= 0) {
        diagnostics.push({
          code: "InvalidRadius",
          severity: "error",
          message: "unitCountInArea requires a positive radius parameter.",
          path: assertion.id ?? assertion.kind
        });
      }
    }

    if (assertion.kind.startsWith("projectile")) {
      if (!assertion.target) {
        diagnostics.push({
          code: "MissingProjectileTarget",
          severity: "error",
          message: `${assertion.kind} requires a target unit alias.`,
          path: assertion.id ?? assertion.kind
        });
      }
    }

    if (assertion.kind === "multiStageStateEquals") {
      const stageIndex = assertion.parameters.stageIndex;
      if (stageIndex == null || typeof stageIndex !== "number" || stageIndex < 0) {
        diagnostics.push({
          code: "InvalidStageIndex",
          severity: "error",
          message: "multiStageStateEquals requires a non-negative stageIndex parameter.",
          path: assertion.id ?? assertion.kind
        });
      }
    }
  }
}

export function formatZodError(error: z.ZodError): ExpectationDiagnostic[] {
  return error.issues.map(issue => ({
    code: "SchemaValidationFailed",
    severity: "error",
    message: issue.message,
    path: issue.path.join(".")
  }));
}
