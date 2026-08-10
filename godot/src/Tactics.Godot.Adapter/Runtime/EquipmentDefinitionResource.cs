using Godot;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class EquipmentDefinitionResource : Resource
{
    [Export] public int SchemaVersion { get; set; } = 1;
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public string SourceId { get; set; } = string.Empty;
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public string SlotValue { get; set; } = string.Empty;
    [Export] public string RarityValue { get; set; } = string.Empty;
    [Export] public int Price { get; set; }
    [Export] public int StrengthBonus { get; set; }
    [Export] public int AgilityBonus { get; set; }
    [Export] public int ConstitutionBonus { get; set; }
    [Export] public int IntelligenceBonus { get; set; }
    [Export] public int CharismaBonus { get; set; }
    [Export] public int LuckBonus { get; set; }

    public EquipmentDefinition ToCoreDefinition()
    {
        if (SchemaVersion != 1)
            throw new InvalidOperationException($"Equipment '{ContentIdValue}' has unsupported schema {SchemaVersion}.");
        return new EquipmentDefinition(
            new ContentId(ContentIdValue),
            SourceId,
            DisplayName,
            Parse<EquipmentSlot>(SlotValue),
            Parse<ItemRarity>(RarityValue),
            Price,
            new UnitAttributes(
                StrengthBonus,
                AgilityBonus,
                ConstitutionBonus,
                IntelligenceBonus,
                CharismaBonus,
                LuckBonus));
    }

    private static T Parse<T>(string value) where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: false, out T result) && Enum.IsDefined(result)
            ? result
            : throw new InvalidOperationException($"Unknown {typeof(T).Name} value '{value}'.");
}
