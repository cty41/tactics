using Godot;
using Tactics.Core.Content;
using Tactics.Core.Items;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class ConsumableDefinitionResource : Resource
{
    [Export] public int SchemaVersion { get; set; } = 1;
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public string SourceId { get; set; } = string.Empty;
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
    [Export] public string RarityValue { get; set; } = string.Empty;
    [Export] public int Price { get; set; }
    [Export] public int MaxCharges { get; set; }
    [Export] public string EffectKindValue { get; set; } = string.Empty;
    [Export] public int Magnitude { get; set; }
    [Export] public int MaxRange { get; set; }
    [Export] public string TargetModeValue { get; set; } = string.Empty;

    public ConsumableDefinition ToCoreDefinition()
    {
        if (SchemaVersion != 1)
            throw new InvalidOperationException($"Consumable '{ContentIdValue}' has unsupported schema {SchemaVersion}.");
        return new ConsumableDefinition(
            new ContentId(ContentIdValue),
            SourceId,
            DisplayName,
            Description,
            Parse<ItemRarity>(RarityValue),
            Price,
            MaxCharges,
            Parse<ConsumableEffectKind>(EffectKindValue),
            Magnitude,
            MaxRange,
            Parse<ConsumableTargetMode>(TargetModeValue));
    }

    private static T Parse<T>(string value) where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: false, out T result) && Enum.IsDefined(result)
            ? result
            : throw new InvalidOperationException($"Unknown {typeof(T).Name} value '{value}'.");
}
