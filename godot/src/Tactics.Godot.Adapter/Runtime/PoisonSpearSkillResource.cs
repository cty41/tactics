using Godot;
using Tactics.Core.Content;
using Tactics.Core.Skills;

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

    /// <summary>Converts the externally owned Poison Spear asset into its canonical Amazon growth branch.</summary>
    public SkillDefinition ToCoreDefinition() => CreateCoreDefinition(ContentId, ManaCost, Range, Damage, PoisonTurns);

    internal static SkillDefinition CreateCoreDefinition(ContentId contentId, int manaCost, int range, int damage,
        int poisonTurns) => new(contentId, "amazon_poison_spear", SkillRole.Amazon, SkillKind.Active, 1,
        manaCost, 1, range, SkillExecutionKind.PoisonSpear, damage, SkillDamageKind.Physical,
        new ContentId("buff.poison"), poisonTurns, externalDependency: true,
        branchId: "amazon.poison-spear");
}
