using System.Collections.ObjectModel;
using Tactics.Application.Content;
using Tactics.Core.Content;
using Tactics.Core.Skills;

namespace Tactics.Application.Skills;

public sealed record SkillDefinitionDraft
{
    public int SchemaVersion { get; init; } = 1;
    public string ContentId { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public int Level { get; init; }
    public int ManaCost { get; init; }
    public int MinRange { get; init; }
    public int MaxRange { get; init; }
    public string ExecutionKind { get; init; } = string.Empty;
    public int Damage { get; init; }
    public string DamageKind { get; init; } = string.Empty;
    public string StatusContentId { get; init; } = string.Empty;
    public int StatusDuration { get; init; }
    public bool Hidden { get; init; }
    public bool ExternalDependency { get; init; }
}

public sealed record SkillDefinitionCompileResult(
    IReadOnlyDictionary<ContentId, SkillDefinition>? Definitions,
    IReadOnlyList<ContentDraft> ContentDrafts,
    IReadOnlyList<ContentDiagnostic> Diagnostics)
{
    public bool Succeeded => Definitions is not null && Diagnostics.All(item => item.Severity != ContentDiagnosticSeverity.Error);
}

/// <summary>Compiles the frozen starting-skill DTO into typed Core and unified content definitions.</summary>
public sealed class SkillDefinitionCompiler
{
    public SkillDefinitionCompileResult Compile(IEnumerable<SkillDefinitionDraft> drafts, bool requireCompleteBatch = true)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        var diagnostics = new List<ContentDiagnostic>();
        var definitions = new Dictionary<ContentId, SkillDefinition>();
        var contentDrafts = new List<ContentDraft>();
        foreach (SkillDefinitionDraft draft in drafts)
        {
            ContentId contentId;
            try { contentId = new ContentId(draft.ContentId); }
            catch (ArgumentException) { diagnostics.Add(Error("skill.invalid_content_id", $"Invalid ContentId '{draft.ContentId}'.")); continue; }
            if (definitions.ContainsKey(contentId)) { diagnostics.Add(Error("skill.duplicate_id", $"Duplicate skill '{contentId}'.", contentId)); continue; }
            if (draft.SchemaVersion != 1 || string.IsNullOrWhiteSpace(draft.SourceId) ||
                !TryEnum(draft.Role, out SkillRole role) || !TryEnum(draft.Kind, out SkillKind kind) ||
                !TryEnum(draft.ExecutionKind, out SkillExecutionKind execution) || !TryEnum(draft.DamageKind, out SkillDamageKind damageKind))
            { diagnostics.Add(Error("skill.invalid_contract", $"Skill '{contentId}' has an invalid schema, source, or enum.", contentId)); continue; }
            ContentId? statusId = null;
            if (!string.IsNullOrEmpty(draft.StatusContentId))
            {
                try { statusId = new ContentId(draft.StatusContentId); }
                catch (ArgumentException) { diagnostics.Add(Error("skill.invalid_status_reference", $"Skill '{contentId}' has an invalid status reference.", contentId)); continue; }
            }
            try
            {
                var definition = new SkillDefinition(contentId, draft.SourceId, role, kind, draft.Level, draft.ManaCost, draft.MinRange, draft.MaxRange, execution, draft.Damage, damageKind, statusId, draft.StatusDuration, draft.Hidden, draft.ExternalDependency);
                definitions.Add(contentId, definition);
                contentDrafts.Add(new ContentDraft(contentId, "skill", 1, statusId is null ? null : new[] { statusId.Value }, new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sourceId"] = draft.SourceId,
                    ["executionKind"] = draft.ExecutionKind,
                    ["externalDependency"] = draft.ExternalDependency ? "true" : "false"
                }));
            }
            catch (ArgumentException error) { diagnostics.Add(Error("skill.invalid_parameter", error.Message, contentId)); }
        }
        if (requireCompleteBatch)
        {
            if (definitions.Count != 12) diagnostics.Add(Error("skill.incomplete_batch", $"Starting-skill batch must contain 12 definitions, got {definitions.Count}."));
            if (!definitions.TryGetValue(new ContentId("skill.poison-spear.lv1"), out SkillDefinition? poison) || !poison.ExternalDependency)
                diagnostics.Add(Error("skill.poison_ownership", "Poison Spear must remain an external dependency."));
        }
        if (diagnostics.Any(item => item.Severity == ContentDiagnosticSeverity.Error)) return new SkillDefinitionCompileResult(null, contentDrafts, diagnostics);
        return new SkillDefinitionCompileResult(new ReadOnlyDictionary<ContentId, SkillDefinition>(definitions.OrderBy(item => item.Key.Value, StringComparer.Ordinal).ToDictionary()), contentDrafts.OrderBy(item => item.ContentId.Value, StringComparer.Ordinal).ToArray(), diagnostics);
    }

    private static bool TryEnum<T>(string value, out T parsed) where T : struct, Enum => Enum.TryParse(value, false, out parsed) && Enum.IsDefined(parsed);
    private static ContentDiagnostic Error(string code, string message, ContentId? contentId = null) => new(code, ContentDiagnosticSeverity.Error, message, contentId);
}
