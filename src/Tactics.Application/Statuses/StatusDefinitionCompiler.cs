using System.Collections.ObjectModel;
using Tactics.Application.Content;
using Tactics.Core.Content;
using Tactics.Core.Statuses;

namespace Tactics.Application.Statuses;

public sealed record StatusDefinitionDraft
{
    public int SchemaVersion { get; init; } = 1;
    public string ContentId { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public int DefaultDuration { get; init; }
    public bool CanAct { get; init; } = true;
    public string Polarity { get; init; } = string.Empty;
    public string EffectKind { get; init; } = string.Empty;
    public string TriggerTiming { get; init; } = string.Empty;
    public string RefreshStrategy { get; init; } = string.Empty;
    public string CurseCategory { get; init; } = string.Empty;
    public float DamagePerTurn { get; init; }
    public string ElementKind { get; init; } = string.Empty;
    public string DamageCategory { get; init; } = string.Empty;
    public float SpeedModifier { get; init; }
    public float DamageReductionPercent { get; init; }
    public string MeleeRetaliationStatusId { get; init; } = string.Empty;
    public int MeleeRetaliationDuration { get; init; }
    public bool ExternalDependency { get; init; }
}

public sealed record StatusDefinitionCompileResult(
    IReadOnlyDictionary<ContentId, StatusDefinition>? Definitions,
    IReadOnlyList<ContentDraft> ContentDrafts,
    IReadOnlyList<ContentDiagnostic> Diagnostics)
{
    public bool Succeeded => Definitions is not null &&
                             Diagnostics.All(item => item.Severity != ContentDiagnosticSeverity.Error);
}

/// <summary>
/// Compiles frozen Buff DTOs into deterministic Core status and unified content drafts.
/// </summary>
public sealed class StatusDefinitionCompiler
{
    public const int SchemaVersion = 1;

    private readonly bool _requireCompleteBatch;

    public StatusDefinitionCompiler(bool requireCompleteBatch = true)
    {
        _requireCompleteBatch = requireCompleteBatch;
    }

    public StatusDefinitionCompileResult Compile(IEnumerable<StatusDefinitionDraft> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        var diagnostics = new List<ContentDiagnostic>();
        var definitions = new Dictionary<ContentId, StatusDefinition>();
        var contentDrafts = new List<ContentDraft>();

        foreach (StatusDefinitionDraft draft in drafts)
        {
            ContentId? contentId = ParseContentId(draft.ContentId, "status.invalid_content_id", diagnostics);
            if (contentId is null)
                continue;
            if (definitions.ContainsKey(contentId.Value))
            {
                diagnostics.Add(Error("status.duplicate_id", $"Duplicate status '{contentId}'.", contentId));
                continue;
            }

            int before = diagnostics.Count;
            if (draft.SchemaVersion != SchemaVersion)
                diagnostics.Add(Error("status.unsupported_schema", "Status schema must be 1.", contentId));
            if (string.IsNullOrWhiteSpace(draft.SourceId))
                diagnostics.Add(Error("status.invalid_source_id", "SourceId cannot be empty.", contentId));
            if (draft.DefaultDuration <= 0)
                diagnostics.Add(Error("status.invalid_duration", "DefaultDuration must be positive.", contentId));
            StatusPolarity polarity = default;
            StatusEffectKind effectKind = default;
            StatusTriggerTiming trigger = default;
            StatusRefreshStrategy refresh = default;
            StatusElementKind element = default;
            StatusDamageCategory damageCategory = default;
            if (!TryEnum(draft.Polarity, out polarity) ||
                !TryEnum(draft.EffectKind, out effectKind) ||
                !TryEnum(draft.TriggerTiming, out trigger) ||
                !TryEnum(draft.RefreshStrategy, out refresh) ||
                !TryEnum(draft.ElementKind, out element) ||
                !TryEnum(draft.DamageCategory, out damageCategory))
            {
                diagnostics.Add(Error("status.unknown_enum", "Status enum value is not recognized.", contentId));
            }
            if (!float.IsFinite(draft.DamagePerTurn) || draft.DamagePerTurn < 0f ||
                draft.DamagePerTurn != MathF.Truncate(draft.DamagePerTurn) ||
                !float.IsFinite(draft.SpeedModifier) ||
                !float.IsFinite(draft.DamageReductionPercent) ||
                draft.DamageReductionPercent < 0f || draft.DamageReductionPercent > 1f)
            {
                diagnostics.Add(Error("status.invalid_parameter", "Status numeric parameter is invalid.", contentId));
            }

            ContentId? retaliationId = null;
            if (!string.IsNullOrEmpty(draft.MeleeRetaliationStatusId))
                retaliationId = ParseContentId(draft.MeleeRetaliationStatusId, "status.invalid_reference", diagnostics);
            if ((retaliationId is null) != (draft.MeleeRetaliationDuration == 0) || draft.MeleeRetaliationDuration < 0)
            {
                diagnostics.Add(Error(
                    "status.invalid_retaliation",
                    "Retaliation reference and positive duration must be configured together.",
                    contentId));
            }
            if (diagnostics.Count != before)
                continue;

            var definition = new StatusDefinition(
                contentId.Value,
                draft.SourceId,
                draft.DefaultDuration,
                draft.CanAct,
                polarity,
                effectKind,
                trigger,
                refresh,
                draft.CurseCategory,
                draft.DamagePerTurn,
                element,
                damageCategory,
                draft.SpeedModifier,
                draft.DamageReductionPercent,
                retaliationId,
                draft.MeleeRetaliationDuration);
            definitions.Add(contentId.Value, definition);
            contentDrafts.Add(new ContentDraft(
                contentId.Value,
                "buff",
                SchemaVersion,
                retaliationId is null ? null : new[] { retaliationId.Value },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sourceId"] = draft.SourceId,
                    ["effectKind"] = draft.EffectKind,
                    ["externalDependency"] = draft.ExternalDependency ? "true" : "false"
                }));
        }

        if (_requireCompleteBatch)
            ValidateCompleteBatch(definitions.Keys, diagnostics);
        if (diagnostics.Any(item => item.Severity == ContentDiagnosticSeverity.Error))
            return new StatusDefinitionCompileResult(null, contentDrafts.AsReadOnly(), diagnostics);

        return new StatusDefinitionCompileResult(
            new ReadOnlyDictionary<ContentId, StatusDefinition>(definitions
                .OrderBy(item => item.Key.Value, StringComparer.Ordinal)
                .ToDictionary(item => item.Key, item => item.Value)),
            contentDrafts.OrderBy(item => item.ContentId.Value, StringComparer.Ordinal).ToArray(),
            diagnostics);
    }

    private static void ValidateCompleteBatch(
        IEnumerable<ContentId> actual,
        ICollection<ContentDiagnostic> diagnostics)
    {
        var expected = new HashSet<ContentId>(new[]
        {
            "buff.counter", "buff.frozen", "buff.ignite", "buff.mark",
            "buff.curse-damage-amplifier", "buff.event-damage-reduction",
            "buff.event-damage-taken-up", "buff.fear", "buff.ice-armor",
            "buff.ice-armor.lv1", "buff.ice-armor.lv2", "buff.poison", "buff.slow", "buff.stun"
        }.Select(value => new ContentId(value)));
        var actualSet = actual.ToHashSet();
        foreach (ContentId missing in expected.Except(actualSet).OrderBy(id => id.Value, StringComparer.Ordinal))
            diagnostics.Add(Error("status.missing_definition", $"Missing status '{missing}'.", missing));
        foreach (ContentId unexpected in actualSet.Except(expected).OrderBy(id => id.Value, StringComparer.Ordinal))
            diagnostics.Add(Error("status.unexpected_definition", $"Unexpected status '{unexpected}'.", unexpected));
    }

    private static bool TryEnum<T>(string value, out T parsed) where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: false, out parsed) && Enum.IsDefined(parsed);

    private static ContentId? ParseContentId(
        string value,
        string code,
        ICollection<ContentDiagnostic> diagnostics)
    {
        try
        {
            return new ContentId(value);
        }
        catch (ArgumentException)
        {
            diagnostics.Add(new ContentDiagnostic(code, ContentDiagnosticSeverity.Error, $"Invalid ContentId '{value}'."));
            return null;
        }
    }

    private static ContentDiagnostic Error(string code, string message, ContentId? contentId) =>
        new(code, ContentDiagnosticSeverity.Error, message, contentId);
}
