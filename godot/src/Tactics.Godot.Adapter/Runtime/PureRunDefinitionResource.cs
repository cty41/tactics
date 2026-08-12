using Godot;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class PureRunDefinitionResource : Resource
{
    [Export] public int SchemaVersion { get; set; } = 1;
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public string[] EncounterContentIds { get; set; } = Array.Empty<string>();
    [Export] public string[] CharacterIds { get; set; } = Array.Empty<string>();
    [Export] public string[] UnitContentIds { get; set; } = Array.Empty<string>();
    [Export] public string[] StartingSkillContentIds { get; set; } = Array.Empty<string>();
    [Export] public string LayerFourMapContentId { get; set; } = string.Empty;

    public PureRunDefinition ToCoreDefinition()
    {
        if (SchemaVersion != 1 || EncounterContentIds.Length != 3 || CharacterIds.Length != 3 || UnitContentIds.Length != 3 || StartingSkillContentIds.Length != 3)
            throw new InvalidOperationException("Pure Run definition resource shape is invalid.");
        UnitAttributes[] attributes =
        {
            new(5, 5, 5, 6, 5, 5),
            new(5, 5, 5, 5, 6, 5),
            new(5, 6, 5, 5, 5, 5)
        };
        return new PureRunDefinition(new ContentId(ContentIdValue), EncounterContentIds.Select(value => new ContentId(value)),
            Enumerable.Range(0, 3).Select(index => new PureRunPartyTemplate(CharacterIds[index], new ContentId(UnitContentIds[index]), new ContentId(StartingSkillContentIds[index]), attributes[index])),
            string.IsNullOrWhiteSpace(LayerFourMapContentId) ? null : new ContentId(LayerFourMapContentId));
    }
}
