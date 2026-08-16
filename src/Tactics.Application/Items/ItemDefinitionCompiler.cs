using System.Collections.ObjectModel;
using Tactics.Application.Content;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Units;

namespace Tactics.Application.Items;

public sealed record ConsumableDefinitionDraft
{
    public int SchemaVersion { get; init; } = 1;
    public string ContentId { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Rarity { get; init; } = string.Empty;
    public int Price { get; init; }
    public int MaxCharges { get; init; }
    public string EffectKind { get; init; } = string.Empty;
    public float Magnitude { get; init; }
    public int MaxRange { get; init; }
    public string TargetMode { get; init; } = string.Empty;
}

public sealed record EquipmentDefinitionDraft
{
    public int SchemaVersion { get; init; } = 1;
    public string ContentId { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Slot { get; init; } = string.Empty;
    public string Rarity { get; init; } = string.Empty;
    public int Price { get; init; }
    public int StrengthBonus { get; init; }
    public int AgilityBonus { get; init; }
    public int ConstitutionBonus { get; init; }
    public int IntelligenceBonus { get; init; }
    public int CharismaBonus { get; init; }
    public int LuckBonus { get; init; }
}

public sealed record ItemDefinitionCompileResult(
    IReadOnlyDictionary<ContentId, ConsumableDefinition>? Consumables,
    IReadOnlyDictionary<ContentId, EquipmentDefinition>? Equipment,
    IReadOnlyList<ContentDraft> ContentDrafts,
    IReadOnlyList<ContentDiagnostic> Diagnostics)
{
    public bool Succeeded => Consumables is not null && Equipment is not null &&
                             Diagnostics.All(item => item.Severity != ContentDiagnosticSeverity.Error);
}

/// <summary>
/// Compiles the frozen JSON item definitions without coupling Core to persistence or UI.
/// </summary>
public sealed class ItemDefinitionCompiler
{
    public const int SchemaVersion = 1;

    public ItemDefinitionCompileResult Compile(
        IEnumerable<ConsumableDefinitionDraft> consumableDrafts,
        IEnumerable<EquipmentDefinitionDraft> equipmentDrafts)
    {
        ArgumentNullException.ThrowIfNull(consumableDrafts);
        ArgumentNullException.ThrowIfNull(equipmentDrafts);
        var diagnostics = new List<ContentDiagnostic>();
        var consumables = new Dictionary<ContentId, ConsumableDefinition>();
        var equipment = new Dictionary<ContentId, EquipmentDefinition>();
        var contentDrafts = new List<ContentDraft>();

        foreach (ConsumableDefinitionDraft draft in consumableDrafts)
        {
            ContentId? contentId = ParseContentId(draft.ContentId, diagnostics);
            if (contentId is null)
                continue;
            int before = diagnostics.Count;
            if (consumables.ContainsKey(contentId.Value) || equipment.ContainsKey(contentId.Value))
                diagnostics.Add(Error("item.duplicate_id", $"Duplicate item '{contentId}'.", contentId));
            if (draft.SchemaVersion != SchemaVersion || string.IsNullOrWhiteSpace(draft.SourceId) ||
                string.IsNullOrWhiteSpace(draft.DisplayName))
            {
                diagnostics.Add(Error("item.invalid_identity", "Item identity or schema is invalid.", contentId));
            }
            ItemRarity rarity = default;
            ConsumableEffectKind effectKind = default;
            ConsumableTargetMode targetMode = default;
            if (!TryEnum(draft.Rarity, out rarity) ||
                !TryEnum(draft.EffectKind, out effectKind) ||
                !TryEnum(draft.TargetMode, out targetMode))
            {
                diagnostics.Add(Error("item.unknown_enum", "Consumable enum value is not recognized.", contentId));
            }
            if (draft.Price < 0 || draft.MaxCharges <= 0 || draft.MaxRange < 0 ||
                !float.IsFinite(draft.Magnitude) || draft.Magnitude < 0 ||
                draft.Magnitude != MathF.Truncate(draft.Magnitude))
            {
                diagnostics.Add(Error("item.invalid_parameter", "Consumable numeric parameter is invalid.", contentId));
            }
            if (diagnostics.Count != before)
                continue;

            consumables.Add(contentId.Value, new ConsumableDefinition(
                contentId.Value,
                draft.SourceId,
                draft.DisplayName,
                draft.Description,
                rarity,
                draft.Price,
                draft.MaxCharges,
                effectKind,
                checked((int)draft.Magnitude),
                draft.MaxRange,
                targetMode));
            contentDrafts.Add(CreateContentDraft(contentId.Value, draft.SourceId, "consumable"));
        }

        foreach (EquipmentDefinitionDraft draft in equipmentDrafts)
        {
            ContentId? contentId = ParseContentId(draft.ContentId, diagnostics);
            if (contentId is null)
                continue;
            int before = diagnostics.Count;
            if (consumables.ContainsKey(contentId.Value) || equipment.ContainsKey(contentId.Value))
                diagnostics.Add(Error("item.duplicate_id", $"Duplicate item '{contentId}'.", contentId));
            if (draft.SchemaVersion != SchemaVersion || string.IsNullOrWhiteSpace(draft.SourceId) ||
                string.IsNullOrWhiteSpace(draft.DisplayName))
            {
                diagnostics.Add(Error("item.invalid_identity", "Item identity or schema is invalid.", contentId));
            }
            ItemRarity rarity = default;
            EquipmentSlot slot = default;
            if (!TryEnum(draft.Rarity, out rarity) || !TryEnum(draft.Slot, out slot))
                diagnostics.Add(Error("item.unknown_enum", "Equipment enum value is not recognized.", contentId));
            int[] bonuses =
            {
                draft.StrengthBonus, draft.AgilityBonus, draft.ConstitutionBonus,
                draft.IntelligenceBonus, draft.CharismaBonus, draft.LuckBonus
            };
            if (draft.Price < 0 || bonuses.Any(value => value < 0))
                diagnostics.Add(Error("item.invalid_parameter", "Equipment price and bonuses cannot be negative.", contentId));
            if (diagnostics.Count != before)
                continue;

            equipment.Add(contentId.Value, new EquipmentDefinition(
                contentId.Value,
                draft.SourceId,
                draft.DisplayName,
                slot,
                rarity,
                draft.Price,
                new UnitAttributes(
                    draft.StrengthBonus,
                    draft.AgilityBonus,
                    draft.ConstitutionBonus,
                    draft.IntelligenceBonus,
                    draft.CharismaBonus,
                    draft.LuckBonus)));
            contentDrafts.Add(CreateContentDraft(contentId.Value, draft.SourceId, "equipment"));
        }

        ValidateExpectedSet(
            consumables.Keys,
            new[]
            {
                "item.consumable.life-potion",
                "item.consumable.mana-potion",
                "item.consumable.cleansing-potion"
            },
            "consumable",
            diagnostics);
        ValidateExpectedSet(
            equipment.Keys,
            new[]
            {
                "item.equipment.sword-01", "item.equipment.leather-armor-01",
                "item.equipment.iron-helmet-01", "item.equipment.leather-boots-01",
                "item.equipment.lucky-ring-01", "item.equipment.staff-01",
                "item.equipment.bow-01", "item.equipment.shield-01",
                "item.equipment.wizard-hat-01", "item.equipment.silver-ring-01",
                "item.equipment.steel-sword-01", "item.equipment.shadow-cloak-01"
            },
            "equipment",
            diagnostics);
        if (diagnostics.Any(item => item.Severity == ContentDiagnosticSeverity.Error))
            return new ItemDefinitionCompileResult(null, null, contentDrafts.AsReadOnly(), diagnostics);

        return new ItemDefinitionCompileResult(
            Ordered(consumables),
            Ordered(equipment),
            contentDrafts.OrderBy(item => item.ContentId.Value, StringComparer.Ordinal).ToArray(),
            diagnostics);
    }

    private static ReadOnlyDictionary<ContentId, T> Ordered<T>(Dictionary<ContentId, T> values) => new(
        values.OrderBy(item => item.Key.Value, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value));

    private static void ValidateExpectedSet(
        IEnumerable<ContentId> actual,
        IEnumerable<string> expectedValues,
        string kind,
        ICollection<ContentDiagnostic> diagnostics)
    {
        var actualSet = actual.ToHashSet();
        var expected = expectedValues.Select(value => new ContentId(value)).ToHashSet();
        foreach (ContentId missing in expected.Except(actualSet).OrderBy(id => id.Value, StringComparer.Ordinal))
            diagnostics.Add(Error("item.missing_definition", $"Missing {kind} '{missing}'.", missing));
        foreach (ContentId unexpected in actualSet.Except(expected).OrderBy(id => id.Value, StringComparer.Ordinal))
            diagnostics.Add(Error("item.unexpected_definition", $"Unexpected {kind} '{unexpected}'.", unexpected));
    }

    private static ContentDraft CreateContentDraft(ContentId id, string sourceId, string kind) => new(
        id,
        "item",
        SchemaVersion,
        properties: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sourceId"] = sourceId,
            ["kind"] = kind
        });

    private static bool TryEnum<T>(string value, out T parsed) where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: false, out parsed) && Enum.IsDefined(parsed);

    private static ContentId? ParseContentId(string value, ICollection<ContentDiagnostic> diagnostics)
    {
        try
        {
            return new ContentId(value);
        }
        catch (ArgumentException)
        {
            diagnostics.Add(new ContentDiagnostic(
                "item.invalid_content_id",
                ContentDiagnosticSeverity.Error,
                $"Invalid item ContentId '{value}'."));
            return null;
        }
    }

    private static ContentDiagnostic Error(string code, string message, ContentId? contentId) =>
        new(code, ContentDiagnosticSeverity.Error, message, contentId);
}
