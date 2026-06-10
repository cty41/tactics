import type { Adapter, ExecutableScenarioPlan, ProbeRequestSchema, ScenarioAssertion, ScenarioSpec, ScenarioStep } from "./schema.js";
import { ExecutableScenarioPlanSchema, type ExpectationDiagnostic } from "./schema.js";
import { validateScenarioSpec } from "./validator.js";

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
  const fallbackAdapter = spec.requiredAdapters.includes("Skill") ? "Skill" : spec.requiredAdapters[0];
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
    diagnostics: validation.diagnostics,
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
