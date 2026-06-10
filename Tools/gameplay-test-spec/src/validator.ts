import { z } from "zod";
import { ScenarioSpecSchema, type ExpectationDiagnostic, type ScenarioSpec } from "./schema.js";

const supportedSetupKinds = new Set([
  "createSkillTestWorld",
  "createSkillGraph",
  "createUnit",
  "setTurnContext"
]);

const supportedActionKinds = new Set([
  "executeSkillGraph"
]);

const supportedAssertionKinds = new Set([
  "executionStateEquals",
  "validationErrorCodeIncludes",
  "unitHealthEquals"
]);

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

  for (const step of spec.setup) {
    if (!supportedSetupKinds.has(step.kind)) {
      diagnostics.push({
        code: "UnsupportedSetupKind",
        severity: "error",
        message: `Unsupported setup kind '${step.kind}'.`,
        path: step.id ?? step.kind
      });
    }
  }

  for (const action of spec.actions) {
    if (!supportedActionKinds.has(action.kind)) {
      diagnostics.push({
        code: "UnsupportedActionKind",
        severity: "error",
        message: `Unsupported action kind '${action.kind}'.`,
        path: action.id ?? action.kind
      });
    }
  }

  for (const assertion of spec.assertions) {
    if (!supportedAssertionKinds.has(assertion.kind)) {
      diagnostics.push({
        code: "UnsupportedAssertionKind",
        severity: "error",
        message: `Unsupported assertion kind '${assertion.kind}'.`,
        path: assertion.id ?? assertion.kind
      });
    }
  }

  if (!spec.requiredAdapters.includes("Skill")) {
    diagnostics.push({
      code: "MissingSkillAdapter",
      severity: "error",
      message: "MVP scenarios must include the Skill adapter."
    });
  }

  return {
    spec,
    diagnostics,
    valid: diagnostics.every(diagnostic => diagnostic.severity !== "error")
  };
}

export function formatZodError(error: z.ZodError): ExpectationDiagnostic[] {
  return error.issues.map(issue => ({
    code: "SchemaValidationFailed",
    severity: "error",
    message: issue.message,
    path: issue.path.join(".")
  }));
}
