import assert from "node:assert/strict";
import test from "node:test";
import { extractContractCandidates } from "../src/llm.js";
import { areAclIdentitiesAllowed, parseProviderConfig, parseProviderSecrets } from "../src/provider-config.js";
import { createOpenCodeGoProvider, doctorOpenCodeGo, OpenCodeGoBaseUrl } from "../src/providers/opencode-go-provider.js";

const source = "毒素每次成功施加增加三个目标行动周期。";
const candidate = {
  candidates: [{
    suggestedId: "BUFF-POISON-DURATION-001",
    statement: source,
    quote: source,
    startLine: 1,
    endLine: 1,
    uncertainties: []
  }]
};

test("OpenCode Go discovers Flash and returns evidence-bound JSON without leaking its key", async () => {
  const key = "secret-that-must-never-appear-in-diagnostics";
  const requests: Array<{ url: string; init?: RequestInit }> = [];
  const fakeFetch: typeof fetch = async (input, init) => {
    const url = String(input);
    requests.push({ url, init });
    if (url.endsWith("/models")) return json({ data: [{ id: "deepseek-v4-flash" }] });
    return json({
      choices: [{ finish_reason: "stop", message: { content: JSON.stringify(candidate) } }],
      usage: { prompt_tokens: 20, completion_tokens: 12 }
    }, { "x-request-id": "req-safe" });
  };
  const result = await extractContractCandidates(source, createOpenCodeGoProvider({ apiKey: key, fetchImpl: fakeFetch }));
  assert.equal(result.valid, true, JSON.stringify(result.diagnostics));
  assert.equal(result.audit?.provider, "opencode-go");
  assert.equal(result.audit?.requestId, "req-safe");
  assert.equal(result.audit?.inputTokens, 20);
  assert.equal(requests[0].url, `${OpenCodeGoBaseUrl}/models`);
  assert.equal(requests[1].url, `${OpenCodeGoBaseUrl}/chat/completions`);
  assert.equal(new Headers(requests[1].init?.headers).get("authorization"), `Bearer ${key}`);
  const body = JSON.parse(String(requests[1].init?.body));
  assert.equal(body.model, "deepseek-v4-flash");
  assert.deepEqual(body.thinking, { type: "disabled" });
  assert.deepEqual(body.response_format, { type: "json_object" });
  assert.equal(JSON.stringify(result).includes(key), false);
});

test("contract extraction preserves the statement key in exact line evidence", async () => {
  const line = "statement: 飞行单位可以越过地面障碍。";
  const provider = createOpenCodeGoProvider({ apiKey: "secret-evidence-key-123456789", fetchImpl: async input =>
    String(input).endsWith("/models") ? json({ data: [{ id: "deepseek-v4-flash" }] }) : json({
      choices: [{ finish_reason: "stop", message: { content: JSON.stringify({ candidates: [{
        suggestedId: "MOVE-AIR-001", statement: "飞行单位可以越过地面障碍。", quote: line,
        startLine: 1, endLine: 1, uncertainties: [] }] }) } }]
    }) });
  const result = await extractContractCandidates(line, provider);
  assert.equal(result.valid, true, JSON.stringify(result.diagnostics));
  assert.equal(result.value?.[0].quote, line);
});

test("OpenCode Go fails closed for missing models, truncation, and HTTP bodies", async () => {
  const missing = createOpenCodeGoProvider({
    apiKey: "secret-model-key-1234567890",
    fetchImpl: async () => json({ data: [{ id: "another-model" }] })
  });
  const missingResult = await extractContractCandidates(source, missing);
  assert.equal(missingResult.valid, false);
  assert.equal(missingResult.diagnostics[0].code, "ProviderModelUnavailable");

  let call = 0;
  const truncated = createOpenCodeGoProvider({
    apiKey: "secret-truncated-key-123456",
    fetchImpl: async () => ++call === 1
      ? json({ data: [{ id: "deepseek-v4-flash" }] })
      : json({ choices: [{ finish_reason: "length", message: { content: "{\"candidates\":" } }] })
  });
  const truncatedResult = await extractContractCandidates(source, truncated);
  assert.equal(truncatedResult.diagnostics[0].code, "ProviderOutputTruncated");

  const secret = "secret-http-key-123456789";
  const httpError = createOpenCodeGoProvider({
    apiKey: secret,
    fetchImpl: async () => new Response(`upstream reflected ${secret}`, { status: 401, headers: { "x-request-id": "req-401" } })
  });
  const httpResult = await extractContractCandidates(source, httpError);
  const serialized = JSON.stringify(httpResult);
  assert.equal(serialized.includes(secret), false);
  assert.match(serialized, /HTTP 401/);
  assert.match(serialized, /req-401/);
});

test("provider doctor sends only a fixed probe and validates its value", async () => {
  let call = 0;
  const result = await doctorOpenCodeGo({
    apiKey: "secret-doctor-key-123456789",
    fetchImpl: async (_input, init) => ++call === 1
      ? json({ data: [{ id: "deepseek-v4-flash" }, { id: "other" }] })
      : (() => {
          const body = JSON.parse(String(init?.body));
          assert.equal(JSON.stringify(body).includes("project"), false);
          return json({ choices: [{ finish_reason: "stop", message: { content: "{\"ok\":true}" } }] });
        })()
  });
  assert.equal(result.valid, true, JSON.stringify(result.diagnostics));
  assert.equal(result.modelCount, 2);
});

test("provider configuration is strict and ACL grants only approved identities", () => {
  const config = parseProviderConfig({ version: 1, providers: { "opencode-go": {} } });
  assert.equal(config.defaultProvider, "opencode-go");
  assert.equal(config.providers["opencode-go"].model, "deepseek-v4-flash");
  assert.throws(() => parseProviderConfig({ version: 1, providers: { "opencode-go": { baseUrl: "https://example.com" } } }));
  assert.throws(() => parseProviderSecrets({ version: 1, providers: { "opencode-go": { apiKey: "short" } } }));
  assert.equal(areAclIdentitiesAllowed("S-1-5-21-123", "D:(A;;FA;;;S-1-5-21-123)(A;;FA;;;SY)(A;;FA;;;BA)"), true);
  assert.equal(areAclIdentitiesAllowed("S-1-5-21-123", "D:(A;;FA;;;S-1-5-21-123)(A;;FR;;;BU)"), false);
});

function json(value: unknown, headers?: Record<string, string>): Response {
  return new Response(JSON.stringify(value), { status: 200, headers: { "content-type": "application/json", ...headers } });
}
