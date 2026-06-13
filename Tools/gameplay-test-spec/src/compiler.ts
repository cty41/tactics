import type { Adapter, ExecutableScenarioPlan, ProbeRequestSchema, ScenarioAssertion, ScenarioDraft, ScenarioSpec, ScenarioStep } from "./schema.js";
import { ExecutableScenarioPlanSchema, ScenarioDraftSchema, type ExpectationDiagnostic } from "./schema.js";
import { validateScenarioSpec, validateScenarioDraft } from "./validator.js";

function resolveAdapter(step: ScenarioStep | ScenarioAssertion, fallback: Adapter): Adapter {
  return step.adapter ?? fallback;
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
    setupActions: spec.setup.map(step => ({ ...step, adapter: resolveAdapter(step, fallbackAdapter) })),
    runtimeActions: spec.actions.map(step => ({ ...step, adapter: resolveAdapter(step, fallbackAdapter) })),
    assertionPlans: spec.assertions.map(assertion => ({ ...assertion, adapter: resolveAdapter(assertion, fallbackAdapter) })),
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

function deriveProbeRequests(spec: ScenarioSpec, adapter: Adapter): ExecutableScenarioPlan["probeRequests"] {
  return spec.assertions.map(assertion => ({
    adapter: assertion.adapter ?? adapter,
    kind: assertion.kind,
    target: assertion.target,
    parameters: assertion.parameters
  }));
}
