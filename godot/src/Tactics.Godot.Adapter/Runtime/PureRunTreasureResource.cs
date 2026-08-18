using Godot;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

[Tool]
[GlobalClass]
public partial class PureRunTreasureResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public int GoldMinimum { get; set; }
    [Export] public int GoldMaximum { get; set; }
    [Export] public string[] EquipmentContentIds { get; set; } = Array.Empty<string>();
    [Export] public int[] EquipmentWeights { get; set; } = Array.Empty<int>();
    [Export] public string[] ConsumableContentIds { get; set; } = Array.Empty<string>();
    [Export] public int[] ConsumableWeights { get; set; } = Array.Empty<int>();
    [Export] public string[] BuffContentIds { get; set; } = Array.Empty<string>();
    [Export] public int[] BuffWeights { get; set; } = Array.Empty<int>();
    [Export(PropertyHint.MultilineText)] public string AuthoringGraphLayoutJsonValue { get; set; } = string.Empty;

    public PureRunTreasureDefinition ToCoreDefinition()
    {
        WeightedContentDefinition[] Build(string[] ids, int[] weights)
        {
            if (ids.Length != weights.Length) throw new InvalidOperationException("Treasure ID/weight lengths differ.");
            return ids.Select((value, index) => new WeightedContentDefinition(new ContentId(value), weights[index])).ToArray();
        }
        var result = new PureRunTreasureDefinition(new ContentId(ContentIdValue), GoldMinimum, GoldMaximum,
            Build(EquipmentContentIds, EquipmentWeights), Build(ConsumableContentIds, ConsumableWeights),
            Build(BuffContentIds, BuffWeights));
        result.Validate();
        return result;
    }
}
