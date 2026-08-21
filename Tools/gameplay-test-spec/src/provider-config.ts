import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { homedir } from "node:os";
import { join } from "node:path";
import { readFile, stat } from "node:fs/promises";
import { z } from "zod";
import type { ExpectationDiagnostic } from "./schema.js";
import { createOllamaProvider } from "./providers/ollama-provider.js";
import { createOpenCodeGoProvider, OpenCodeGoBaseUrl, OpenCodeGoFlashModel, type OpenCodeGoProviderOptions } from "./providers/opencode-go-provider.js";
import type { LlmProviderId, StructuredLlmProvider } from "./llm.js";

const ProviderConfigSchema = z.object({
  version: z.literal(1),
  defaultProvider: z.enum(["opencode-go", "ollama"]).default("opencode-go"),
  providers: z.object({
    "opencode-go": z.object({
      baseUrl: z.literal(OpenCodeGoBaseUrl).default(OpenCodeGoBaseUrl),
      model: z.literal(OpenCodeGoFlashModel).default(OpenCodeGoFlashModel),
      timeoutMs: z.number().int().positive().max(600000).default(120000),
      maxTokens: z.number().int().positive().max(8192).default(768)
    }).strict().default({}),
    ollama: z.object({
      host: z.string().url().default("http://127.0.0.1:11434"),
      model: z.string().min(1).default("qwen3.5:2b"),
      timeoutMs: z.number().int().positive().max(600000).default(120000),
      maxTokens: z.number().int().positive().max(8192).default(768)
    }).strict().optional()
  }).strict()
}).strict();

const ProviderSecretsSchema = z.object({
  version: z.literal(1),
  providers: z.object({
    "opencode-go": z.object({ apiKey: z.string().min(20) }).strict()
  }).strict()
}).strict();

export type ProviderConfig = z.infer<typeof ProviderConfigSchema>;
export interface ProviderPaths { configPath: string; secretsPath: string; }
export interface LoadedProvider {
  provider?: StructuredLlmProvider;
  providerId?: LlmProviderId;
  diagnostics: ExpectationDiagnostic[];
  paths: ProviderPaths;
  openCodeOptions?: OpenCodeGoProviderOptions;
}

export function parseProviderConfig(value: unknown): ProviderConfig {
  return ProviderConfigSchema.parse(value);
}

export function parseProviderSecrets(value: unknown): { version: 1; providers: { "opencode-go": { apiKey: string } } } {
  return ProviderSecretsSchema.parse(value);
}

export function providerPaths(environment: NodeJS.ProcessEnv = process.env): ProviderPaths {
  const root = environment.LOCALAPPDATA ?? join(homedir(), ".tactics");
  const directory = join(root, "Tactics", "gameplay-test-spec");
  return { configPath: join(directory, "providers.json"), secretsPath: join(directory, "secrets.json") };
}

export async function loadConfiguredProvider(
  providerOverride?: LlmProviderId,
  paths = providerPaths()
): Promise<LoadedProvider> {
  const diagnostics: ExpectationDiagnostic[] = [];
  let config: ProviderConfig;
  try {
    config = parseProviderConfig(JSON.parse(await readFile(paths.configPath, "utf8")));
  } catch (error) {
    diagnostics.push({ code: "ProviderConfigInvalid", severity: "error", message: configError(paths.configPath, error) });
    return { diagnostics, paths };
  }
  const providerId = providerOverride ?? config.defaultProvider;
  if (providerId === "ollama") {
    const ollama = config.providers.ollama;
    if (!ollama) {
      diagnostics.push({ code: "ProviderConfigMissing", severity: "error", message: "Ollama provider configuration is missing." });
      return { providerId, diagnostics, paths };
    }
    return { providerId, provider: createOllamaProvider(ollama), diagnostics, paths };
  }

  const secure = await validateSecretsPermissions(paths.secretsPath);
  if (!secure.valid) {
    diagnostics.push(...secure.diagnostics);
    return { providerId, diagnostics, paths };
  }
  try {
    const secrets = parseProviderSecrets(JSON.parse(await readFile(paths.secretsPath, "utf8")));
    const openCodeOptions = { ...config.providers["opencode-go"], apiKey: secrets.providers["opencode-go"].apiKey };
    return {
      providerId,
      provider: createOpenCodeGoProvider(openCodeOptions),
      openCodeOptions,
      diagnostics,
      paths
    };
  } catch (error) {
    diagnostics.push({ code: "ProviderSecretsInvalid", severity: "error", message: configError(paths.secretsPath, error) });
    return { providerId, diagnostics, paths };
  }
}

export async function validateSecretsPermissions(path: string): Promise<{ valid: boolean; diagnostics: ExpectationDiagnostic[] }> {
  try {
    const file = await stat(path);
    if (!file.isFile()) throw new Error("not a file");
    if (process.platform !== "win32") {
      return (file.mode & 0o077) === 0
        ? { valid: true, diagnostics: [] }
        : denied("ProviderSecretsPermissions", "Provider secrets must use mode 0600.");
    }
    const command = "$u=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value; Write-Output $u; Write-Output (Get-Acl -LiteralPath $env:TACTICS_PROVIDER_SECRET_PATH).Sddl";
    const result = await promisify(execFile)("powershell", ["-NoProfile", "-NonInteractive", "-Command", command], {
      windowsHide: true,
      env: { ...process.env, TACTICS_PROVIDER_SECRET_PATH: path }
    });
    const [userSid, sddl] = result.stdout.trim().split(/\r?\n/, 2);
    if (!userSid || !sddl) return denied("ProviderSecretsPermissions", "Unable to inspect provider secrets ACL.");
    return areAclIdentitiesAllowed(userSid, sddl)
      ? { valid: true, diagnostics: [] }
      : denied("ProviderSecretsPermissions", "Provider secrets ACL grants access beyond the current user, SYSTEM, or Administrators.");
  } catch {
    return denied("ProviderSecretsUnavailable", `Provider secrets file is missing or unreadable: ${path}`);
  }
}

export function areAclIdentitiesAllowed(userSid: string, sddl: string): boolean {
  const allowed = new Set([userSid, "SY", "BA", "S-1-5-18", "S-1-5-32-544"]);
  const granted = [...sddl.matchAll(/\((?!D;)[^)]*;;;([^)]+)\)/g)].map(match => match[1]);
  return granted.length > 0 && granted.every(identity => allowed.has(identity));
}

function configError(path: string, error: unknown): string {
  if (error instanceof z.ZodError) return `Invalid provider file '${path}': ${error.issues.map(issue => issue.message).join("; ")}`;
  return `Provider file is missing or invalid: ${path}`;
}

function denied(code: string, message: string): { valid: false; diagnostics: ExpectationDiagnostic[] } {
  return { valid: false, diagnostics: [{ code, severity: "error", message }] };
}
