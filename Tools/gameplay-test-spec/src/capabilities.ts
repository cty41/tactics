import type { Adapter, RuntimeTarget, ScenarioAssertion, ScenarioStep } from "./schema.js";

export interface RuntimeCapabilityManifest {
  runtime: RuntimeTarget;
  setupKinds: ReadonlySet<string>;
  actionKinds: ReadonlySet<string>;
  assertionKinds: ReadonlySet<string>;
}

const godotSetupKinds = new Set([
  "loadValidatedCheckpoint",
  "initializePlayerInput"
]);

const godotActionKinds = new Set([
  "movePointerToTarget",
  "clickPointerTarget",
  "rightClickPointerTarget",
  "pressInputKey",
  "waitForPlayerObservable",
  "waitForFrames",
  "playBattleThroughInput",
  "endTurnOnlyUntilTerminal",
  "restartGodotMain",
  "setPresentationPaused",
  "setPresentationSpeed"
]);

const godotAssertionKinds = new Set([
  "inventoryProjectionEnteredBattle",
  "terminalSummaryOutcomeEquals",
  "activeRunExistsEquals",
  "presentationNumberEquals",
  "presentationNodeCountEquals",
  "productionSaveUnchanged",
  "checkpointRevisionEquals",
  "runtimeStateHashEquals",
  "runtimeHasNoErrors"
]);

export const GodotCapabilityManifest: RuntimeCapabilityManifest = {
  runtime: "Godot",
  setupKinds: godotSetupKinds,
  actionKinds: godotActionKinds,
  assertionKinds: godotAssertionKinds
};

export function validateRuntimeCapabilities(
  runtime: RuntimeTarget,
  setup: ScenarioStep[],
  actions: ScenarioStep[],
  assertions: ScenarioAssertion[]
): Array<{ code: string; severity: "error"; message: string; path: string }> {
  const diagnostics: Array<{ code: string; severity: "error"; message: string; path: string }> = [];
  if (runtime === "Unity") {
    const rejectGodotOnly = (items: Array<ScenarioStep | ScenarioAssertion>, godotOnly: ReadonlySet<string>, phase: string) => {
      for (const item of items) if (godotOnly.has(item.kind)) diagnostics.push({
        code: "UnsupportedRuntimeCapability",
        severity: "error",
        message: `Unity runtime does not support Godot-only ${phase} '${item.kind}'.`,
        path: item.id ?? item.kind
      });
    };
    rejectGodotOnly(setup, new Set(["loadValidatedCheckpoint"]), "setup");
    rejectGodotOnly(actions, new Set(["endTurnOnlyUntilTerminal", "restartGodotMain", "setPresentationPaused", "setPresentationSpeed"]), "action");
    rejectGodotOnly(assertions, godotAssertionKinds, "assertion");
    return diagnostics;
  }
  const check = (items: Array<ScenarioStep | ScenarioAssertion>, supported: ReadonlySet<string>, phase: string) => {
    for (const item of items) if (!supported.has(item.kind)) diagnostics.push({
      code: "UnsupportedRuntimeCapability",
      severity: "error",
      message: `Godot runtime does not support ${phase} '${item.kind}'.`,
      path: item.id ?? item.kind
    });
  };
  check(setup, GodotCapabilityManifest.setupKinds, "setup");
  check(actions, GodotCapabilityManifest.actionKinds, "action");
  check(assertions, GodotCapabilityManifest.assertionKinds, "assertion");
  return diagnostics;
}

export function requiredCapabilities(
  setup: ScenarioStep[], actions: ScenarioStep[], assertions: ScenarioAssertion[]
): string[] {
  return [...new Set([
    ...setup.map(value => `setup:${value.kind}`),
    ...actions.map(value => `action:${value.kind}`),
    ...assertions.map(value => `assertion:${value.kind}`)
  ])].sort((left, right) => left.localeCompare(right));
}
