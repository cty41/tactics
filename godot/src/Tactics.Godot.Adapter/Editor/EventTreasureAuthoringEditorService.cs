#if TOOLS
using Tactics.Application.Authoring;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public static class EventTreasureAuthoringEditorService
{
    private static readonly IReadOnlyDictionary<string, string> LegacyContentIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cleansing_potion"] = "item.consumable.cleansing-potion",
            ["Assets/Tactics/ScriptableObjects/Buffs/EventDamageReduction.asset"] = "buff.event-damage-reduction",
            ["Assets/Tactics/ScriptableObjects/Buffs/EventDamageTakenUp.asset"] = "buff.event-damage-taken-up"
        };

    public static EventAuthoringDocument Read(PureRunLayerFourResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (!string.Equals(resource.KindValue, "event", StringComparison.Ordinal))
            throw new InvalidOperationException("Layer-four Resource is not an event.");
        EventAuthoringDocument document = EventAuthoringJson.Deserialize(resource.PayloadJson);
        if (!string.Equals(document.ContentId, resource.ContentIdValue, StringComparison.Ordinal))
            throw new InvalidOperationException("Event payload identity differs from the Resource identity.");
        return NormalizeReferences(document);
    }

    public static void Write(PureRunLayerFourResource resource, EventAuthoringDocument document)
    {
        if (!string.Equals(resource.ContentIdValue, document.ContentId, StringComparison.Ordinal))
            throw new InvalidOperationException("Event document identity differs from the Resource identity.");
        document.Validate();
        resource.KindValue = "event";
        resource.PayloadJson = EventAuthoringJson.SerializePayload(document);
    }

    public static TreasureAuthoringDocument Read(PureRunTreasureResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (resource.EquipmentContentIds.Length != resource.EquipmentWeights.Length ||
            resource.ConsumableContentIds.Length != resource.ConsumableWeights.Length ||
            resource.BuffContentIds.Length != resource.BuffWeights.Length)
            throw new InvalidOperationException("Treasure ID and weight arrays differ in length.");
        return new TreasureAuthoringDocument(resource.ContentIdValue, resource.GoldMinimum, resource.GoldMaximum,
            Build(TreasureEntryKind.Equipment, resource.EquipmentContentIds, resource.EquipmentWeights)
                .Concat(Build(TreasureEntryKind.Consumable, resource.ConsumableContentIds, resource.ConsumableWeights))
                .Concat(Build(TreasureEntryKind.Buff, resource.BuffContentIds, resource.BuffWeights)));
    }

    public static void Write(PureRunTreasureResource resource, TreasureAuthoringDocument document)
    {
        if (!string.Equals(resource.ContentIdValue, document.ContentId, StringComparison.Ordinal))
            throw new InvalidOperationException("Treasure document identity differs from the Resource identity.");
        _ = document.ToCoreDefinition();
        resource.GoldMinimum = document.GoldMinimum;
        resource.GoldMaximum = document.GoldMaximum;
        (resource.EquipmentContentIds, resource.EquipmentWeights) = Assign(TreasureEntryKind.Equipment);
        (resource.ConsumableContentIds, resource.ConsumableWeights) = Assign(TreasureEntryKind.Consumable);
        (resource.BuffContentIds, resource.BuffWeights) = Assign(TreasureEntryKind.Buff);

        (string[] Ids, int[] Weights) Assign(TreasureEntryKind kind)
        {
            TreasureEntryAuthoring[] rows = document.Entries.Where(value => value.Kind == kind).ToArray();
            return (rows.Select(value => value.ContentId).ToArray(), rows.Select(value => value.Weight).ToArray());
        }
    }

    private static IEnumerable<TreasureEntryAuthoring> Build(TreasureEntryKind kind, string[] ids, int[] weights) =>
        ids.Select((value, index) => new TreasureEntryAuthoring(kind, value, weights[index]));

    private static EventAuthoringDocument NormalizeReferences(EventAuthoringDocument document) => new(
        document.ContentId, document.SourceId, document.Title, document.Description,
        document.Options.Select(option => option with
        {
            Success = Normalize(option.Success),
            Failure = option.Failure is null ? null : Normalize(option.Failure)
        }), document.SourcePath, document.SourceSha256);

    private static EventOutcomeAuthoring Normalize(EventOutcomeAuthoring outcome) =>
        outcome.EffectContentId is { } value && LegacyContentIds.TryGetValue(value, out string? canonical)
            ? outcome with { EffectContentId = canonical }
            : outcome;
}
#endif
