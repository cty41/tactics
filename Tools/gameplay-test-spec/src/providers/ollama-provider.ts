import { invalid, type LlmResult, type StructuredLlmProvider } from "../llm.js";

export interface OllamaProviderOptions {
  host?: string;
  model?: string;
  seed?: number;
  timeoutMs?: number;
  maxTokens?: number;
  fetchImpl?: typeof fetch;
}

export function createOllamaProvider(options: OllamaProviderOptions = {}): StructuredLlmProvider {
  const host = options.host ?? "http://127.0.0.1:11434";
  const model = options.model ?? "qwen3.5:2b";
  return {
    id: "ollama",
    model,
    async complete(messages, schema): Promise<LlmResult<unknown>> {
      const started = Date.now();
      const base = new URL(host);
      if (!(base.hostname === "127.0.0.1" || base.hostname === "localhost" || base.hostname === "::1")) {
        return invalid("OllamaRemoteHostRejected", "Only loopback Ollama hosts are allowed.");
      }
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), options.timeoutMs ?? 120000);
      try {
        const response = await (options.fetchImpl ?? fetch)(new URL("/api/chat", base), {
          method: "POST",
          headers: { "content-type": "application/json" },
          signal: controller.signal,
          body: JSON.stringify({
            model,
            stream: false,
            think: false,
            format: schema,
            messages,
            options: { temperature: 0, seed: options.seed ?? 41, num_predict: options.maxTokens ?? 768 }
          })
        });
        const audit = { provider: "ollama" as const, model, elapsedMs: Date.now() - started };
        if (!response.ok) return invalid("OllamaHttpError", `Ollama returned HTTP ${response.status}.`, audit);
        const payload = await response.json() as { message?: { content?: string; thinking?: string } };
        if (payload.message?.thinking) return invalid("OllamaThinkingLeak", "Ollama returned thinking content while think=false.", audit);
        if (!payload.message?.content) return invalid("OllamaEmptyResponse", "Ollama returned no structured content.", audit);
        try { return { value: JSON.parse(payload.message.content), diagnostics: [], valid: true, audit }; }
        catch { return invalid("OllamaJsonInvalid", "Ollama returned invalid JSON.", audit); }
      } catch (error) {
        const audit = { provider: "ollama" as const, model, elapsedMs: Date.now() - started };
        return invalid(error instanceof Error && error.name === "AbortError" ? "OllamaTimeout" : "OllamaUnavailable", safeError(error), audit);
      } finally {
        clearTimeout(timeout);
      }
    }
  };
}

function safeError(error: unknown): string {
  return error instanceof Error ? error.name : "Unknown provider error";
}
