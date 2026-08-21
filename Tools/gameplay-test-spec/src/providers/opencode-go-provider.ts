import { invalid, type LlmAudit, type LlmResult, type StructuredLlmProvider } from "../llm.js";

export const OpenCodeGoBaseUrl = "https://opencode.ai/zen/go/v1";
export const OpenCodeGoFlashModel = "deepseek-v4-flash";

export interface OpenCodeGoProviderOptions {
  apiKey: string;
  baseUrl?: string;
  model?: string;
  timeoutMs?: number;
  maxTokens?: number;
  fetchImpl?: typeof fetch;
}

export interface ProviderDoctorResult {
  valid: boolean;
  diagnostics: Array<{ code: string; severity: "error"; message: string }>;
  provider: "opencode-go";
  model: string;
  modelCount?: number;
  probe?: LlmAudit;
}

export function createOpenCodeGoProvider(options: OpenCodeGoProviderOptions): StructuredLlmProvider {
  const baseUrl = validateBaseUrl(options.baseUrl ?? OpenCodeGoBaseUrl);
  const model = options.model ?? OpenCodeGoFlashModel;
  return {
    id: "opencode-go",
    model,
    async complete(messages, schema) {
      const discovery = await discoverModels(options, baseUrl);
      if (!discovery.valid || !discovery.value?.includes(model)) {
        return discovery.valid
          ? invalid("ProviderModelUnavailable", `OpenCode Go model '${model}' is not available.`)
          : discovery as LlmResult<unknown>;
      }
      return callChatCompletion(messages, schema, options, baseUrl, model);
    }
  };
}

export async function doctorOpenCodeGo(options: OpenCodeGoProviderOptions): Promise<ProviderDoctorResult> {
  const baseUrl = validateBaseUrl(options.baseUrl ?? OpenCodeGoBaseUrl);
  const model = options.model ?? OpenCodeGoFlashModel;
  const discovery = await discoverModels(options, baseUrl);
  if (!discovery.valid || !discovery.value) return {
    valid: false,
    diagnostics: discovery.diagnostics as ProviderDoctorResult["diagnostics"],
    provider: "opencode-go",
    model
  };
  if (!discovery.value.includes(model)) return {
    valid: false,
    diagnostics: [{ code: "ProviderModelUnavailable", severity: "error", message: `OpenCode Go model '${model}' is not available.` }],
    provider: "opencode-go",
    model,
    modelCount: discovery.value.length
  };
  const probe = await callChatCompletion([
    { role: "system", content: "Return JSON only." },
    { role: "user", content: "Return {\"ok\":true}." }
  ], {
    type: "object",
    additionalProperties: false,
    properties: { ok: { type: "boolean" } },
    required: ["ok"]
  }, options, baseUrl, model);
  const validProbe = probe.valid && (probe.value as { ok?: unknown } | undefined)?.ok === true;
  return {
    valid: validProbe,
    diagnostics: validProbe ? [] : probe.valid
      ? [{ code: "ProviderProbeInvalid", severity: "error", message: "OpenCode Go JSON probe returned an unexpected value." }]
      : probe.diagnostics as ProviderDoctorResult["diagnostics"],
    provider: "opencode-go",
    model,
    modelCount: discovery.value.length,
    probe: probe.audit
  };
}

async function discoverModels(options: OpenCodeGoProviderOptions, baseUrl: string): Promise<LlmResult<string[]>> {
  const started = Date.now();
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), options.timeoutMs ?? 120000);
  try {
    const response = await (options.fetchImpl ?? fetch)(`${baseUrl}/models`, {
      headers: { authorization: `Bearer ${options.apiKey}`, accept: "application/json" },
      signal: controller.signal
    });
    const audit = auditFrom(response, started, options.model ?? OpenCodeGoFlashModel);
    if (!response.ok) return invalid("ProviderDiscoveryHttpError", httpMessage("model discovery", response, audit), audit);
    const payload = await response.json() as { data?: Array<{ id?: unknown }> };
    if (!Array.isArray(payload.data)) return invalid("ProviderDiscoveryInvalid", "OpenCode Go model discovery returned an invalid payload.", audit);
    return {
      value: payload.data.flatMap(item => typeof item.id === "string" ? [item.id] : []),
      diagnostics: [],
      valid: true,
      audit
    };
  } catch (error) {
    const audit = { provider: "opencode-go" as const, model: options.model ?? OpenCodeGoFlashModel, elapsedMs: Date.now() - started };
    return invalid(error instanceof Error && error.name === "AbortError" ? "ProviderDiscoveryTimeout" : "ProviderDiscoveryUnavailable", safeError(error), audit);
  } finally {
    clearTimeout(timeout);
  }
}

async function callChatCompletion(
  messages: Array<{ role: "system" | "user"; content: string }>,
  schema: object,
  options: OpenCodeGoProviderOptions,
  baseUrl: string,
  model: string
): Promise<LlmResult<unknown>> {
  const started = Date.now();
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), options.timeoutMs ?? 120000);
  try {
    const schemaInstruction = `Return one JSON object matching this schema: ${JSON.stringify(schema)}`;
    const response = await (options.fetchImpl ?? fetch)(`${baseUrl}/chat/completions`, {
      method: "POST",
      headers: {
        authorization: `Bearer ${options.apiKey}`,
        "content-type": "application/json",
        accept: "application/json"
      },
      signal: controller.signal,
      body: JSON.stringify({
        model,
        messages: [{ role: "system", content: schemaInstruction }, ...messages],
        thinking: { type: "disabled" },
        temperature: 0,
        max_tokens: options.maxTokens ?? 768,
        response_format: { type: "json_object" },
        stream: false
      })
    });
    const audit = auditFrom(response, started, model);
    if (!response.ok) return invalid("ProviderHttpError", httpMessage("chat completion", response, audit), audit);
    const payload = await response.json() as {
      choices?: Array<{ finish_reason?: string; message?: { content?: string | null } }>;
      usage?: { prompt_tokens?: number; completion_tokens?: number };
    };
    audit.inputTokens = payload.usage?.prompt_tokens;
    audit.outputTokens = payload.usage?.completion_tokens;
    const choice = payload.choices?.[0];
    if (choice?.finish_reason === "length") return invalid("ProviderOutputTruncated", "OpenCode Go output reached max_tokens before completing JSON.", audit);
    if (!choice?.message?.content) return invalid("ProviderEmptyResponse", "OpenCode Go returned no structured content.", audit);
    try { return { value: JSON.parse(choice.message.content), diagnostics: [], valid: true, audit }; }
    catch { return invalid("ProviderJsonInvalid", "OpenCode Go returned invalid JSON.", audit); }
  } catch (error) {
    const audit = { provider: "opencode-go" as const, model, elapsedMs: Date.now() - started };
    return invalid(error instanceof Error && error.name === "AbortError" ? "ProviderTimeout" : "ProviderUnavailable", safeError(error), audit);
  } finally {
    clearTimeout(timeout);
  }
}

function validateBaseUrl(value: string): string {
  const normalized = value.replace(/\/$/, "");
  if (normalized !== OpenCodeGoBaseUrl) throw new Error(`OpenCode Go base URL must be '${OpenCodeGoBaseUrl}'.`);
  return normalized;
}

function auditFrom(response: Response, started: number, model: string): LlmAudit {
  return {
    provider: "opencode-go",
    model,
    elapsedMs: Date.now() - started,
    requestId: response.headers.get("x-request-id") ?? undefined
  };
}

function httpMessage(operation: string, response: Response, audit: LlmAudit): string {
  const request = audit.requestId ? ` Request ID: ${audit.requestId}.` : "";
  return `OpenCode Go ${operation} returned HTTP ${response.status}.${request}`;
}

function safeError(error: unknown): string {
  return error instanceof Error ? error.name : "Unknown provider error";
}
