import { z } from "zod";

export const AdapterSchema = z.enum(["Battle", "Skill", "Map", "UI", "PlayerInput"]);
export const RuntimeTargetSchema = z.enum(["Unity", "Godot"]);

export const WatchdogSchema = z.object({
  stepTimeoutMs: z.number().int().positive().default(30000),
  battleRoundLimit: z.number().int().positive().default(80),
  scenarioTimeoutMs: z.number().int().positive().default(300000),
  noProgressLimit: z.number().int().positive().default(2)
});

export const CheckpointSchema = z.object({
  id: z.string().min(1),
  source: z.literal("validated_checkpoint"),
  semanticHash: z.string().regex(/^[a-f0-9]{64}$/),
  path: z.string().min(1)
});

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

const godotPlanAdapterByKind: Record<string, z.infer<typeof AdapterSchema>> = {
  loadValidatedCheckpoint: "Map",
  initializePlayerInput: "PlayerInput",
  movePointerToTarget: "PlayerInput",
  clickPointerTarget: "PlayerInput",
  rightClickPointerTarget: "PlayerInput",
  pressInputKey: "PlayerInput",
  waitForPlayerObservable: "PlayerInput",
  waitForFrames: "PlayerInput",
  playBattleThroughInput: "PlayerInput",
  endTurnOnlyUntilTerminal: "Battle",
  endTurnUntilPresentationNumber: "Battle",
  restartGodotMain: "UI",
  setPresentationPaused: "UI",
  setPresentationSpeed: "UI",
  inventoryProjectionEnteredBattle: "Battle",
  terminalSummaryOutcomeEquals: "Map",
  activeRunExistsEquals: "Map",
  presentationNumberEquals: "UI",
  presentationNodeCountEquals: "UI",
  productionSaveUnchanged: "Map",
  checkpointRevisionEquals: "Map",
  runtimeStateHashEquals: "UI",
  runtimeHasNoErrors: "UI"
};

export const GodotExecutableScenarioPlanSchema = z.object({
  schemaVersion: z.literal(2),
  runtime: z.literal("Godot"),
  scenarioName: z.string().min(1),
  requiredAdapters: z.array(AdapterSchema).min(1),
  requiredCapabilities: z.array(z.string().min(1)),
  setupActions: z.array(ExecutableActionSchema),
  runtimeActions: z.array(ExecutableActionSchema),
  assertionPlans: z.array(ExecutableAssertionSchema),
  timeoutMs: z.number().int().positive(),
  probeRequests: z.array(ProbeRequestSchema).default([]),
  checkpoint: CheckpointSchema.optional(),
  saveIsolation: z.object({ root: z.string().min(1), protectProductionSave: z.literal(true) }),
  watchdog: WatchdogSchema
}).superRefine((plan, context) => {
  const phases = [
    ["setup", plan.setupActions] as const,
    ["action", plan.runtimeActions] as const,
    ["assertion", plan.assertionPlans] as const
  ];
  const expectedCapabilities: string[] = [];
  for (const [phase, items] of phases) for (const item of items) {
    const expectedAdapter = godotPlanAdapterByKind[item.kind];
    if (!expectedAdapter) {
      context.addIssue({ code: z.ZodIssueCode.custom, message: `Unsupported Godot ${phase} '${item.kind}'.` });
      continue;
    }
    if (item.adapter !== expectedAdapter) context.addIssue({ code: z.ZodIssueCode.custom, message: `${item.kind} must use ${expectedAdapter}.` });
    if (!plan.requiredAdapters.includes(expectedAdapter)) context.addIssue({ code: z.ZodIssueCode.custom, message: `${item.kind} requires ${expectedAdapter}.` });
    expectedCapabilities.push(`${phase}:${item.kind}`);
  }
  const actual = [...new Set(plan.requiredCapabilities)].sort();
  const expected = [...new Set(expectedCapabilities)].sort();
  if (actual.length !== expected.length || actual.some((value, index) => value !== expected[index])) {
    context.addIssue({ code: z.ZodIssueCode.custom, message: "requiredCapabilities must exactly match the compiled Godot steps." });
  }
  if (plan.probeRequests.length !== plan.assertionPlans.length) {
    context.addIssue({ code: z.ZodIssueCode.custom, message: "probeRequests must correspond one-to-one with assertionPlans." });
  } else {
    plan.probeRequests.forEach((probe, index) => {
      const assertion = plan.assertionPlans[index];
      if (probe.kind !== assertion.kind || probe.adapter !== assertion.adapter || probe.target !== assertion.target ||
          JSON.stringify(probe.parameters) !== JSON.stringify(assertion.parameters)) {
        context.addIssue({ code: z.ZodIssueCode.custom, message: `probeRequests[${index}] does not match its assertion plan.` });
      }
    });
  }
});

export type Adapter = z.infer<typeof AdapterSchema>;
export type RuntimeTarget = z.infer<typeof RuntimeTargetSchema>;
export type ScenarioStep = z.infer<typeof ScenarioStepSchema>;
export type ScenarioAssertion = z.infer<typeof ScenarioAssertionSchema>;
export type ScenarioSpec = z.infer<typeof ScenarioSpecSchema>;
export type ScenarioDraft = z.infer<typeof ScenarioDraftSchema>;
export type UnityExecutableScenarioPlan = z.infer<typeof ExecutableScenarioPlanSchema>;
export type GodotExecutableScenarioPlan = z.infer<typeof GodotExecutableScenarioPlanSchema>;
export type ExecutableScenarioPlan = UnityExecutableScenarioPlan | GodotExecutableScenarioPlan;

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
