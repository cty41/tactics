#if TOOLS
using Godot;
using Tactics.Application.Authoring;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public sealed record AuthoringCatalogAuditRow(string ContentId, string Type, string Path, string Uid,
    string? Revision, AuthoringResourceOwnership Ownership, IReadOnlyList<string> ForwardReferences, IReadOnlyList<string> ReverseReferences,
    IReadOnlyList<AuthoringDiagnostic> Diagnostics);

public static class AuthoringCatalogAuditService
{
    public static IReadOnlyList<AuthoringCatalogAuditRow> Audit(GodotResourceCatalog catalog)
    {
        AuthoringResourceHandlerRegistry handlers = AuthoringResourceHandlerRegistry.CreateDefault();
        var lifecycle = new AuthoringResourceLifecycleService();
        catalog.Validate(); var ids = catalog.Entries.Select(value => value.ContentIdValue).ToHashSet(StringComparer.Ordinal);
        var forward = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var revisions = new Dictionary<string, string?>(StringComparer.Ordinal);
        var diagnostics = new Dictionary<string, List<AuthoringDiagnostic>>(StringComparer.Ordinal);
        foreach (GodotResourceEntry entry in catalog.Entries)
        {
            diagnostics[entry.ContentIdValue] = [];
            try
            {
                Resource? resource = ResourceLoader.Load(entry.DiagnosticPathValue, string.Empty, ResourceLoader.CacheMode.Ignore);
                if (resource is null) throw new InvalidOperationException("Resource cannot be loaded.");
                IAuthoringDocument? document = handlers.TryGet(entry.ResourceTypeIdValue, out IAuthoringResourceHandler? handler) && handler!.CanHandle(resource) ? handler.Read(resource) : null;
                IReadOnlyList<string> references = document?.Dependencies ?? entry.ReferenceContentIds;
                forward[entry.ContentIdValue] = references; revisions[entry.ContentIdValue] = document is null ? null : AuthoringRevision.Compute(document);
                if (document is not null && handler is not null) diagnostics[entry.ContentIdValue].AddRange(handler.Validate(document));
                foreach (string missing in references.Where(value => !ids.Contains(value))) diagnostics[entry.ContentIdValue].Add(new AuthoringDiagnostic("catalog.reference_missing", AuthoringDiagnosticSeverity.Error, $"Missing ContentId '{missing}'.", missing));
            }
            catch (Exception error)
            {
                forward[entry.ContentIdValue] = entry.ReferenceContentIds; revisions[entry.ContentIdValue] = null;
                diagnostics[entry.ContentIdValue].Add(new AuthoringDiagnostic("catalog.resource_invalid", AuthoringDiagnosticSeverity.Error, error.Message, entry.DiagnosticPathValue));
            }
        }
        return Array.AsReadOnly(catalog.Entries.Select(entry => new AuthoringCatalogAuditRow(entry.ContentIdValue, entry.ResourceTypeIdValue,
            entry.DiagnosticPathValue, entry.ResourceUidValue, revisions[entry.ContentIdValue], lifecycle.GetOwnership(entry.ContentIdValue), forward[entry.ContentIdValue],
            forward.Where(pair => pair.Value.Contains(entry.ContentIdValue, StringComparer.Ordinal)).Select(pair => pair.Key).Order(StringComparer.Ordinal).ToArray(), diagnostics[entry.ContentIdValue])).ToArray());
    }

}
#endif
