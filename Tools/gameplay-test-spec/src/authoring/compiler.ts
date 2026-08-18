import { AuthoringAssetBatchSpecSchema, type AuthoringCompilerDiagnostic, type CompiledAuthoringBatchV1 } from "./schema.js";

export interface AuthoringCompileResult { valid: boolean; batch?: CompiledAuthoringBatchV1; diagnostics: AuthoringCompilerDiagnostic[] }

export function compileAuthoringSpec(input: unknown): AuthoringCompileResult {
  const parsed = AuthoringAssetBatchSpecSchema.safeParse(input);
  if (!parsed.success) return { valid: false, diagnostics: parsed.error.issues.map(issue => ({
    code: "AuthoringSpecInvalid", severity: "error", message: issue.message, path: issue.path.join(".")
  })) };
  const ids = new Set<string>(); const diagnostics: AuthoringCompilerDiagnostic[] = [];
  for (const [index, asset] of parsed.data.assets.entries()) {
    if (ids.has(asset.contentId)) diagnostics.push({ code: "DuplicateAssetIdentity", severity: "error", message: `Duplicate asset '${asset.contentId}'.`, path: `assets.${index}.contentId` });
    ids.add(asset.contentId);
    for (const dependency of asset.dependencies) if (!ids.has(dependency) && !parsed.data.assets.some(value => value.contentId === dependency))
      diagnostics.push({ code: "ExternalDependencyDeferred", severity: "warning", message: `Dependency '${dependency}' will be validated by the Editor Catalog.`, path: `assets.${index}.dependencies` });
  }
  if (diagnostics.some(value => value.severity === "error")) return { valid: false, diagnostics };
  let ordered: typeof parsed.data.assets;
  try { ordered = topological(parsed.data.assets); }
  catch (error) { return { valid: false, diagnostics: [...diagnostics, { code: "AuthoringDependencyCycle", severity: "error", message: error instanceof Error ? error.message : String(error), path: "assets" }] }; }
  const batch: CompiledAuthoringBatchV1 = { schemaVersion: 1, changes: [], lifecycle: [] };
  for (const asset of ordered) {
    let document: Record<string, unknown> | undefined;
    try { document = asset.eventGraph ? compileEventGraph(asset.contentId, asset.eventGraph) : asset.document; }
    catch (error) { return { valid: false, diagnostics: [...diagnostics, { code: "AuthoringCompileFailed", severity: "error", message: error instanceof Error ? error.message : String(error), path: `assets.${asset.contentId}` }] }; }
    const snapshot = document ? JSON.stringify(canonical(document)) : undefined;
    if (asset.operation === "update") batch.changes.push({ kind: asset.kind, contentId: asset.contentId, expectedRevision: asset.expectedRevision!, snapshot: snapshot! });
    else batch.lifecycle.push({ operation: asset.operation, contentId: asset.contentId, sourceContentId: asset.sourceContentId,
      resourceType: asset.kind, expectedReferenceRevision: asset.expectedReferenceRevision,
      initialSnapshot: asset.operation === "create" || asset.operation === "duplicate" ? snapshot : undefined });
  }
  return { valid: true, batch, diagnostics };
}

function compileEventGraph(contentId: string, value: any): Record<string, unknown> {
  const optionIds = value.options.map((option: any) => option.id);
  if (new Set(optionIds).size !== optionIds.length) throw new Error("Event option identities must be unique.");
  return { contentId, schemaVersion: 2, sourceId: value.sourceId, title: value.title, description: value.description,
    options: value.options.map((option: any) => ({ id: option.id, text: option.text, attribute: option.check.attribute,
      baseSuccessRate: option.check.baseSuccessRate, success: option.success, failure: option.failure ?? null })),
    graphLayout: value.graphLayout ?? { layoutSchemaVersion: 1, nodes: [] } };
}

function topological<T extends { contentId: string; dependencies: string[] }>(assets: T[]): T[] {
  const byId = new Map(assets.map(value => [value.contentId, value])); const visiting = new Set<string>(); const visited = new Set<string>(); const result: T[] = [];
  function visit(value: T): void {
    if (visited.has(value.contentId)) return; if (visiting.has(value.contentId)) throw new Error(`Authoring dependency cycle at '${value.contentId}'.`);
    visiting.add(value.contentId); for (const id of [...value.dependencies].sort()) { const dependency = byId.get(id); if (dependency) visit(dependency); }
    visiting.delete(value.contentId); visited.add(value.contentId); result.push(value);
  }
  for (const value of [...assets].sort((a, b) => a.contentId.localeCompare(b.contentId))) visit(value); return result;
}

function canonical(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(canonical);
  if (value && typeof value === "object") return Object.fromEntries(Object.entries(value as Record<string, unknown>).sort(([a], [b]) => a.localeCompare(b)).map(([key, item]) => [key, canonical(item)]));
  return value;
}
