import { z } from "zod";
import { ScenarioDraftSchema, type ExpectationDiagnostic, type ScenarioDraft } from "./schema.js";

export type LlmProviderId = "opencode-go" | "ollama";
export interface LlmAudit {
  provider: LlmProviderId;
  model: string;
  elapsedMs: number;
  requestId?: string;
  inputTokens?: number;
  outputTokens?: number;
}

export interface LlmResult<T> {
  value?: T;
  diagnostics: ExpectationDiagnostic[];
  valid: boolean;
  audit?: LlmAudit;
}

export interface StructuredLlmProvider {
  readonly id: LlmProviderId;
  readonly model: string;
  complete(messages: Array<{ role: "system" | "user"; content: string }>, schema: object): Promise<LlmResult<unknown>>;
}

const CandidateSchema = z.object({
  candidates: z.array(z.object({
    suggestedId: z.string(),
    statement: z.string().min(1),
    quote: z.string().min(1),
    startLine: z.number().int().positive(),
    endLine: z.number().int().positive(),
    uncertainties: z.array(z.string())
  }).strict()).max(3)
}).strict();

export type ContractCandidate = z.infer<typeof CandidateSchema>["candidates"][number];

const candidateJsonSchema = {
  type: "object",
  additionalProperties: false,
  properties: {
    candidates: {
      type: "array",
      maxItems: 3,
      items: {
        type: "object",
        additionalProperties: false,
        properties: {
          suggestedId: { type: "string" },
          statement: { type: "string" },
          quote: { type: "string" },
          startLine: { type: "integer" },
          endLine: { type: "integer" },
          uncertainties: { type: "array", items: { type: "string" } }
        },
        required: ["suggestedId", "statement", "quote", "startLine", "endLine", "uncertainties"]
      }
    }
  },
  required: ["candidates"]
};

const scenarioDraftJsonSchema = {
  type: "object",
  additionalProperties: false,
  properties: {
    feature: { type: "string" },
    scenario: { type: "string" },
    contractIds: { type: "array", items: { type: "string" } },
    tags: { type: "array", items: { type: "string" } },
    requiredAdapters: { type: "array", minItems: 3, maxItems: 3, items: { type: "string", enum: ["Map", "Battle", "UI"] } },
    setup: { type: "array", minItems: 1, maxItems: 1, items: { type: "object", additionalProperties: false,
      properties: { kind: { type: "string", enum: ["loadValidatedCheckpoint"] }, parameters: { type: "object", additionalProperties: false,
        properties: { id: { type: "string" }, path: { type: "string" }, semanticHash: { type: "string", pattern: "^[a-f0-9]{64}$" } }, required: ["id", "path", "semanticHash"] } }, required: ["kind", "parameters"] } },
    actions: { type: "array", minItems: 1, maxItems: 1, items: { type: "object", additionalProperties: false,
      properties: { kind: { type: "string", enum: ["endTurnOnlyUntilTerminal"] }, parameters: { type: "object", additionalProperties: true } }, required: ["kind", "parameters"] } },
    assertions: { type: "array", minItems: 1, maxItems: 1, items: { type: "object", additionalProperties: false,
      properties: { kind: { type: "string", enum: ["runtimeHasNoErrors"] }, expected: { type: "boolean", enum: [true] }, parameters: { type: "object", additionalProperties: true } }, required: ["kind", "expected", "parameters"] } },
    timeoutMs: { type: "integer" }
  },
  required: ["feature", "scenario", "contractIds", "tags", "requiredAdapters", "setup", "actions", "assertions", "timeoutMs"]
};

export async function extractContractCandidates(
  markdown: string,
  provider: StructuredLlmProvider
): Promise<LlmResult<ContractCandidate[]>> {
  const numbered = markdown.split(/\r?\n/).map((line, index) => `${index + 1}: ${line}`).join("\n");
  const response = await provider.complete([
    {
      role: "system",
      content: "Return JSON only. Extract at most three explicit gameplay rules. Never infer missing behavior. For each candidate, quote exact contiguous source text character-for-character and provide exact one-based line numbers. Preserve the complete original line, including a YAML key such as 'statement:' when present; omit only the added '<line-number>: ' numbering prefix. Preserve all whitespace. Suggested IDs are non-authoritative."
    },
    { role: "user", content: numbered }
  ], candidateJsonSchema);
  if (!response.valid || !response.value) return response as LlmResult<ContractCandidate[]>;
  const parsed = CandidateSchema.safeParse(response.value);
  if (!parsed.success) return invalid("LlmSchemaInvalid", parsed.error.message, response.audit);
  const diagnostics: ExpectationDiagnostic[] = [];
  const lines = markdown.split(/\r?\n/);
  for (const candidate of parsed.data.candidates) {
    const actual = lines.slice(candidate.startLine - 1, candidate.endLine).join("\n");
    if (candidate.endLine < candidate.startLine || actual !== candidate.quote) diagnostics.push({
      code: "LlmEvidenceMismatch",
      severity: "error",
      message: `Candidate evidence does not match lines ${candidate.startLine}-${candidate.endLine}.`
    });
  }
  return { value: parsed.data.candidates, diagnostics, valid: diagnostics.length === 0, audit: response.audit };
}

export async function generateScenarioDraft(
  contractText: string,
  contractId: string,
  provider: StructuredLlmProvider
): Promise<LlmResult<ScenarioDraft>> {
  const response = await provider.complete([
    {
      role: "system",
      content: `Return JSON only. Propose exactly one ScenarioDraft using only this compact DSL catalog:
requiredAdapters must be exactly ["Map","Battle","UI"] in that order. Never output Unit, Gameplay, Core, Terrain, AI, or any other adapter.
setupKinds=[bindBattleController,createSkillTestWorld,createCell,createUnit,createAiBrain,useRealAssets]
actionKinds=[moveUnit,setUnitState,executeAbility,executeAI,endTurnOnlyUntilTerminal]
assertionKinds=[unitHealthEquals,unitCellEquals,unitAliveEquals,aiTargetEquals,aiSelectedIntentTypeEquals,runtimeHasNoErrors]
Every setup/action/assertion item must contain a non-empty kind and a parameters object. Adapter names belong only in requiredAdapters, never in an item. Do not output an adapter named gameplay. Do not invent kinds. The deterministic validator is authoritative.
Use this exact structural skeleton and only customize feature, scenario, tags, and checkpoint id. Keep the placeholder checkpoint path and hash unchanged; candidates are reviewed before promotion: {"feature":"example","scenario":"example","contractIds":["AAA-BBB-001"],"tags":[],"requiredAdapters":["Map","Battle","UI"],"setup":[{"kind":"loadValidatedCheckpoint","parameters":{"id":"candidate","path":"Tests/checkpoints/candidate.json","semanticHash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}}],"actions":[{"kind":"endTurnOnlyUntilTerminal","parameters":{}}],"assertions":[{"kind":"runtimeHasNoErrors","expected":true,"parameters":{}}],"timeoutMs":10000}`
    },
    { role: "user", content: `Contract ${contractId}: ${contractText}` }
  ], scenarioDraftJsonSchema);
  if (!response.valid || !response.value) return response as LlmResult<ScenarioDraft>;
  const parsed = ScenarioDraftSchema.safeParse(response.value);
  if (!parsed.success) return invalid("LlmDraftInvalid", parsed.error.message, response.audit);
  if (!parsed.data.contractIds?.includes(contractId)) {
    return invalid("LlmContractIdMissing", `Draft must reference '${contractId}'.`, response.audit);
  }
  return { value: parsed.data, diagnostics: [], valid: true, audit: response.audit };
}

export function invalid<T>(code: string, message: string, audit?: LlmAudit): LlmResult<T> {
  return { valid: false, diagnostics: [{ code, severity: "error", message }], audit };
}
