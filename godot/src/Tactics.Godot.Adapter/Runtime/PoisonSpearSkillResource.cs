using Godot;
using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class PoisonSpearSkillResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = "skill.poison-spear.lv1";
    [Export] public int Range { get; set; } = 6;
    [Export] public int MoveCost { get; set; } = 6;
    [Export] public int Damage { get; set; } = 8;
    [Export] public int PoisonTurns { get; set; } = 3;
    [Export] public PoisonSpearPresentationResource? Presentation { get; set; }

    public ContentId ContentId => new(ContentIdValue);
}
