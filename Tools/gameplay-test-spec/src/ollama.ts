import type { ScenarioDraft } from "./schema.js";
import { extractContractCandidates as extractWithProvider, generateScenarioDraft as generateWithProvider,
  type ContractCandidate, type LlmResult } from "./llm.js";
import { createOllamaProvider, type OllamaProviderOptions } from "./providers/ollama-provider.js";

export type OllamaOptions = OllamaProviderOptions;
export type OllamaResult<T> = LlmResult<T>;
export type { ContractCandidate };

export async function extractContractCandidates(markdown: string, options: OllamaOptions = {}): Promise<OllamaResult<ContractCandidate[]>> {
  return extractWithProvider(markdown, createOllamaProvider(options));
}

export async function generateScenarioDraft(contractText: string, contractId: string, options: OllamaOptions = {}): Promise<OllamaResult<ScenarioDraft>> {
  return generateWithProvider(contractText, contractId, createOllamaProvider(options));
}
