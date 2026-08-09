using Godot;
using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class PoisonSpearSkillResource : Resource
{
    [Export] public string ContentIdValue { get; set; } = string.Empty;
    [Export] public int SchemaVersion { get; set; }
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
    [Export] public int Range { get; set; }
    [Export] public int ManaCost { get; set; }
    [Export] public int Damage { get; set; }
    [Export] public int PoisonTurns { get; set; }
    [Export] public int PoisonDamagePerTurn { get; set; }
    [Export] public bool RequiresLineOfSight { get; set; }
    [Export] public float ProjectileSpeed { get; set; }
    [Export] public float ProjectileTravelTime { get; set; }
    [Export] public bool DropOnHit { get; set; } = true;
    [Export] public int DropSearchRadius { get; set; }
    [Export] public bool DropsSpearOnCompletion { get; set; }
    [Export] public PoisonBuffResource? Poison { get; set; }
    [Export] public PoisonSpearPresentationResource? Presentation { get; set; }

    public ContentId ContentId => new(ContentIdValue);
}
