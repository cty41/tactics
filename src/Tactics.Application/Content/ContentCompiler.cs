using Tactics.Core.Content;

namespace Tactics.Application.Content;

public sealed record ContentCompileResult(
    ContentSnapshot? Snapshot,
    IReadOnlyList<ContentDiagnostic> Diagnostics)
{
    public bool Succeeded => Snapshot is not null && Diagnostics.All(item => item.Severity != ContentDiagnosticSeverity.Error);
}

public sealed class ContentCompiler
{
    private readonly ContentSchemaCatalog _schemaCatalog;

    public ContentCompiler(ContentSchemaCatalog? schemaCatalog = null)
    {
        _schemaCatalog = schemaCatalog ?? ContentSchemaCatalog.RuntimeV1;
    }

    public ContentCompileResult Compile(IEnumerable<ContentDraft> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        ContentDraft[] materialized = drafts.ToArray();
        var diagnostics = new List<ContentDiagnostic>();
        var unique = new Dictionary<ContentId, ContentDraft>();

        foreach (ContentDraft draft in materialized)
        {
            if (!unique.TryAdd(draft.ContentId, draft))
            {
                diagnostics.Add(Error("content.duplicate_id", $"Duplicate ContentId '{draft.ContentId}'.", draft.ContentId));
            }

            if (draft.SchemaVersion < 1)
            {
                diagnostics.Add(Error("content.invalid_schema", "SchemaVersion must be at least 1.", draft.ContentId));
            }

            if (string.IsNullOrWhiteSpace(draft.ResourceTypeId))
            {
                diagnostics.Add(Error("content.empty_resource_type", "ResourceTypeId cannot be empty.", draft.ContentId));
            }
            else if (!_schemaCatalog.TryGet(draft.ResourceTypeId, out ContentSchemaDefinition? definition))
            {
                diagnostics.Add(Error(
                    "content.unknown_resource_type",
                    $"ResourceTypeId '{draft.ResourceTypeId}' is not registered.",
                    draft.ContentId));
            }
            else if (draft.SchemaVersion < definition.MinimumSupportedVersion ||
                     draft.SchemaVersion > definition.CurrentVersion)
            {
                diagnostics.Add(Error(
                    "content.unsupported_schema",
                    $"SchemaVersion {draft.SchemaVersion} for '{draft.ResourceTypeId}' is outside supported range " +
                    $"{definition.MinimumSupportedVersion}..{definition.CurrentVersion}.",
                    draft.ContentId));
            }
        }

        foreach (ContentDraft draft in unique.Values)
        {
            foreach (ContentId reference in draft.References.Distinct())
            {
                if (!unique.ContainsKey(reference))
                {
                    diagnostics.Add(Error(
                        "content.missing_reference",
                        $"Content '{draft.ContentId}' references missing content '{reference}'.",
                        draft.ContentId));
                }
            }
        }

        if (diagnostics.Any(item => item.Severity == ContentDiagnosticSeverity.Error))
            return new ContentCompileResult(null, diagnostics);

        ContentSnapshot snapshot = new(unique.Values
            .OrderBy(draft => draft.ContentId.Value, StringComparer.Ordinal)
            .Select(draft => new ContentSnapshotEntry(
                draft.ContentId,
                draft.ResourceTypeId,
                draft.SchemaVersion,
                draft.References.ToArray(),
                new Dictionary<string, string>(draft.Properties, StringComparer.Ordinal))));
        return new ContentCompileResult(snapshot, diagnostics);
    }

    private static ContentDiagnostic Error(string code, string message, ContentId contentId) =>
        new(code, ContentDiagnosticSeverity.Error, message, contentId);
}
