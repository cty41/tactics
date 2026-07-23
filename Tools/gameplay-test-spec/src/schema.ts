import { z } from "zod";

export const AdapterSchema = z.enum(["Battle", "Skill", "Map", "UI", "PlayerInput"]);

const JsonValueSchema: z.ZodType<unknown> = z.lazy(() =>
  z.union([
    z.string(),
    z.number(),
    z.boolean(),
    z.null(),
    z.array(JsonValueSchema),
    z.record(JsonValueSchema)
  ])
);

export const ScenarioStepSchema = z.object({
  id: z.string().optional(),
  adapter: AdapterSchema.optional(),
  kind: z.string().min(1),
  target: z.string().optional(),
  parameters: z.record(JsonValueSchema).default({})
});

export const ScenarioAssertionSchema = z.object({
  id: z.string().optional(),
  adapter: AdapterSchema.optional(),
  kind: z.string().min(1),
  target: z.string().optional(),
  expected: JsonValueSchema.optional(),
  parameters: z.record(JsonValueSchema).default({})
});

export const ScenarioSpecSchema = z.object({
  feature: z.string().min(1),
  scenario: z.string().min(1),
  tags: z.array(z.string()).default([]),
  requiredAdapters: z.array(AdapterSchema).min(1),
  setup: z.array(ScenarioStepSchema).default([]),
  actions: z.array(ScenarioStepSchema).min(1),
  assertions: z.array(ScenarioAssertionSchema).min(1),
  timeoutMs: z.number().int().positive().default(10000)
});

export const ScenarioDraftSetupSchema = z.object({
  kind: z.string().min(1),
  parameters: z.record(JsonValueSchema).default({})
});

export const ScenarioDraftActionSchema = z.object({
  kind: z.string().min(1),
  target: z.string().optional(),
  parameters: z.record(JsonValueSchema).default({})
});

export const ScenarioDraftAssertionSchema = z.object({
  kind: z.string().min(1),
  target: z.string().optional(),
  expected: JsonValueSchema.optional(),
  parameters: z.record(JsonValueSchema).default({})
});

export const ScenarioDraftSchema = z.object({
  feature: z.string().min(1),
  scenario: z.string().min(1),
  tags: z.array(z.string()).default([]),
  requiredAdapters: z.array(AdapterSchema).min(1),
  setup: z.array(ScenarioDraftSetupSchema).default([]),
  actions: z.array(ScenarioDraftActionSchema).min(1),
  assertions: z.array(ScenarioDraftAssertionSchema).min(1),
  timeoutMs: z.number().int().positive().default(10000)
});

export const ExecutableActionSchema = ScenarioStepSchema.extend({
  adapter: AdapterSchema
});

export const ExecutableAssertionSchema = ScenarioAssertionSchema.extend({
  adapter: AdapterSchema
});

export const ProbeRequestSchema = z.object({
  adapter: AdapterSchema,
  kind: z.string().min(1),
  target: z.string().optional(),
  parameters: z.record(JsonValueSchema).default({})
});

export const ExecutableScenarioPlanSchema = z.object({
  schemaVersion: z.literal(1),
  scenarioName: z.string().min(1),
  requiredAdapters: z.array(AdapterSchema).min(1),
  setupActions: z.array(ExecutableActionSchema),
  runtimeActions: z.array(ExecutableActionSchema),
  assertionPlans: z.array(ExecutableAssertionSchema),
  timeoutMs: z.number().int().positive(),
  probeRequests: z.array(ProbeRequestSchema).default([])
});

export type Adapter = z.infer<typeof AdapterSchema>;
export type ScenarioStep = z.infer<typeof ScenarioStepSchema>;
export type ScenarioAssertion = z.infer<typeof ScenarioAssertionSchema>;
export type ScenarioSpec = z.infer<typeof ScenarioSpecSchema>;
export type ScenarioDraft = z.infer<typeof ScenarioDraftSchema>;
export type ExecutableScenarioPlan = z.infer<typeof ExecutableScenarioPlanSchema>;

export type DiagnosticSeverity = "error" | "warning";

export interface ExpectationDiagnostic {
  code: string;
  severity: DiagnosticSeverity;
  message: string;
  path?: string;
}

export interface GenerationResult {
  spec?: ScenarioSpec;
  diagnostics: ExpectationDiagnostic[];
  needsClarification: boolean;
  missingFields: string[];
  ambiguousFields: string[];
}
