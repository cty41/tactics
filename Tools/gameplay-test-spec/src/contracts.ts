import yaml from "js-yaml";
import { z } from "zod";
import { ContractIdSchema, type ExpectationDiagnostic } from "./schema.js";

export const ContractStatusSchema = z.enum(["verified_current", "approved_target"]);
export const ContractDslSupportSchema = z.enum(["supported", "partial", "unsupported"]);
const VerificationSchema = z.object({
  layer: z.enum(["core_test", "application_test", "gameplay_spec", "godot_test", "manual_qa"]),
  path: z.string().min(1)
}).strict();

export const GameplayContractSchema = z.object({
  id: ContractIdSchema,
  status: ContractStatusSchema,
  statement: z.string().min(1),
  verification: z.array(VerificationSchema).min(1),
  dsl_support: ContractDslSupportSchema,
  supersedes: z.array(ContractIdSchema).optional(),
  superseded_by: ContractIdSchema.optional()
}).strict().superRefine((contract, context) => {
  if (contract.supersedes?.includes(contract.id) || contract.superseded_by === contract.id) {
    context.addIssue({ code: z.ZodIssueCode.custom, message: "A contract cannot supersede itself." });
  }
});

export type GameplayContract = z.infer<typeof GameplayContractSchema>;
export interface ParsedGameplayContract extends GameplayContract {
  sourcePath: string;
  startLine: number;
  endLine: number;
}

export interface ContractValidationResult {
  contracts: ParsedGameplayContract[];
  diagnostics: ExpectationDiagnostic[];
  valid: boolean;
}

const blockPattern = /```gameplay-contract\s*\r?\n([\s\S]*?)\r?\n```/g;

export function parseGameplayContracts(markdown: string, sourcePath: string): ContractValidationResult {
  const contracts: ParsedGameplayContract[] = [];
  const diagnostics: ExpectationDiagnostic[] = [];
  const seen = new Set<string>();
  let match: RegExpExecArray | null;

  while ((match = blockPattern.exec(markdown)) !== null) {
    const startLine = lineNumberAt(markdown, match.index);
    const endLine = startLine + match[0].split(/\r?\n/).length - 1;
    let raw: unknown;
    try {
      raw = yaml.load(match[1]);
    } catch (error) {
      diagnostics.push({ code: "ContractYamlInvalid", severity: "error", message: String(error), path: `${sourcePath}:${startLine}` });
      continue;
    }
    const parsed = GameplayContractSchema.safeParse(raw);
    if (!parsed.success) {
      for (const issue of parsed.error.issues) diagnostics.push({
        code: "ContractSchemaInvalid", severity: "error", message: issue.message,
        path: `${sourcePath}:${startLine}:${issue.path.join(".")}`
      });
      continue;
    }
    if (seen.has(parsed.data.id)) {
      diagnostics.push({ code: "ContractIdDuplicate", severity: "error", message: `Duplicate contract ID '${parsed.data.id}'.`, path: `${sourcePath}:${startLine}` });
      continue;
    }
    seen.add(parsed.data.id);
    contracts.push({ ...parsed.data, sourcePath, startLine, endLine });
  }

  return { contracts, diagnostics, valid: diagnostics.every(value => value.severity !== "error") };
}

export function validateContractRegistry(documents: Array<{ path: string; markdown: string }>): ContractValidationResult {
  const diagnostics: ExpectationDiagnostic[] = [];
  const contracts: ParsedGameplayContract[] = [];
  const byId = new Map<string, ParsedGameplayContract>();
  for (const document of documents) {
    const result = parseGameplayContracts(document.markdown, document.path);
    diagnostics.push(...result.diagnostics);
    for (const contract of result.contracts) {
      const existing = byId.get(contract.id);
      if (existing) diagnostics.push({
        code: "ContractIdDuplicate", severity: "error",
        message: `Contract ID '${contract.id}' is also declared at ${existing.sourcePath}:${existing.startLine}.`,
        path: `${contract.sourcePath}:${contract.startLine}`
      });
      else { byId.set(contract.id, contract); contracts.push(contract); }
    }
  }
  for (const contract of contracts) {
    for (const target of [...(contract.supersedes ?? []), ...(contract.superseded_by ? [contract.superseded_by] : [])]) {
      if (!byId.has(target)) diagnostics.push({
        code: "ContractReferenceMissing", severity: "error",
        message: `Contract '${contract.id}' references missing contract '${target}'.`,
        path: `${contract.sourcePath}:${contract.startLine}`
      });
    }
  }
  detectSupersedeCycles(contracts, byId, diagnostics);
  return { contracts, diagnostics, valid: diagnostics.every(value => value.severity !== "error") };
}

export function contractCoverage(contractIds: string[], contracts: ParsedGameplayContract[]): Array<{ id: string; status: string }> {
  const byId = new Map(contracts.map(contract => [contract.id, contract]));
  return contractIds.map(id => {
    const contract = byId.get(id);
    if (!contract) return { id, status: "missing-spec" };
    if (contract.dsl_support === "unsupported") return { id, status: "unsupported" };
    return { id, status: "covered" };
  });
}

function lineNumberAt(text: string, index: number): number {
  return text.slice(0, index).split(/\r?\n/).length;
}

function detectSupersedeCycles(
  contracts: ParsedGameplayContract[], byId: Map<string, ParsedGameplayContract>, diagnostics: ExpectationDiagnostic[]
): void {
  for (const start of contracts) {
    const visited = new Set<string>();
    let current: ParsedGameplayContract | undefined = start;
    while (current?.superseded_by) {
      if (visited.has(current.id)) {
        diagnostics.push({ code: "ContractSupersedeCycle", severity: "error", message: `Supersede cycle includes '${current.id}'.`, path: `${start.sourcePath}:${start.startLine}` });
        break;
      }
      visited.add(current.id);
      current = byId.get(current.superseded_by);
    }
  }
}
