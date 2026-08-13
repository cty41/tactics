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
    public bool IsBasicAbility { get; init; }
    public int MaxUsesPerTurn { get; init; }
    public string BranchId { get; init; } = string.Empty;
    public string PrerequisiteContentId { get; init; } = string.Empty;
    public bool GrowthVisible { get; init; } = true;
    public string RequiredAttribute { get; init; } = string.Empty;
    public int MinimumAttribute { get; init; }
    public string PrerequisiteBranchId { get; init; } = string.Empty;
    public int AreaRadius { get; init; }
    public int OrderedTargetCount { get; init; }
    public string SummonDefinitionId { get; init; } = string.Empty;
    public int SummonCount { get; init; }
    public int SummonLimit { get; init; }
    public string SummonCategory { get; init; } = string.Empty;
    public bool RequiresCorpse { get; init; }
    public bool IgnoreLineOfSight { get; init; }
    public int ShieldMultiplier { get; init; }
    public bool ShieldAbsorbsAllDamage { get; init; }
    public bool CleanseHarmful { get; init; }
    public int SecondaryDamage { get; init; }
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
            ContentId? prerequisiteId = ParseOptionalId(draft.PrerequisiteContentId, "skill.invalid_prerequisite", contentId, diagnostics);
            ContentId? summonId = ParseOptionalId(draft.SummonDefinitionId, "skill.invalid_summon_reference", contentId, diagnostics);
            if (diagnostics.Any(item => item.ContentId == contentId && item.Severity == ContentDiagnosticSeverity.Error)) continue;
            try
            {
                var profile = new SkillExecutionProfile(draft.AreaRadius, draft.OrderedTargetCount, summonId, draft.SummonCount, draft.SummonLimit, draft.SummonCategory, draft.RequiresCorpse, draft.IgnoreLineOfSight, draft.ShieldMultiplier, draft.ShieldAbsorbsAllDamage, draft.CleanseHarmful, draft.SecondaryDamage);
                var definition = new SkillDefinition(contentId, draft.SourceId, role, kind, draft.Level, draft.ManaCost, draft.MinRange, draft.MaxRange, execution, draft.Damage, damageKind, statusId, draft.StatusDuration, draft.Hidden, draft.ExternalDependency, draft.IsBasicAbility, draft.MaxUsesPerTurn, draft.BranchId, prerequisiteId, draft.GrowthVisible, profile, draft.RequiredAttribute, draft.MinimumAttribute, draft.PrerequisiteBranchId);
                definitions.Add(contentId, definition);
                contentDrafts.Add(new ContentDraft(contentId, "skill", 1, statusId is null ? null : new[] { statusId.Value }, new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sourceId"] = draft.SourceId,
                    ["executionKind"] = draft.ExecutionKind,
                    ["externalDependency"] = draft.ExternalDependency ? "true" : "false"
                    ,["isBasicAbility"] = draft.IsBasicAbility ? "true" : "false"
                    ,["maxUsesPerTurn"] = draft.MaxUsesPerTurn.ToString(System.Globalization.CultureInfo.InvariantCulture)
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
    private static ContentId? ParseOptionalId(string value, string code, ContentId owner, List<ContentDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return new ContentId(value); }
        catch (ArgumentException) { diagnostics.Add(Error(code, $"Skill '{owner}' has invalid reference '{value}'.", owner)); return null; }
    }
    private static ContentDiagnostic Error(string code, string message, ContentId? contentId = null) => new(code, ContentDiagnosticSeverity.Error, message, contentId);
}
