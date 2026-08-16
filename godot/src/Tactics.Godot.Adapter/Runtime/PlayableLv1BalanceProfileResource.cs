using Godot;
using Tactics.Application.Battle;
using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Runtime;

[GlobalClass]
public partial class PlayableLv1BalanceProfileResource : Resource
{
    [Export] public string ContractId { get; set; } = "godot-playable-lv1-balance-v1";
    [Export] public string[] SkillContentIds { get; set; } = Array.Empty<string>();
    [Export] public int[] SkillManaCosts { get; set; } = Array.Empty<int>();
    [Export] public int[] SkillDamages { get; set; } = Array.Empty<int>();
    [Export] public string[] UnitContentIds { get; set; } = Array.Empty<string>();
    [Export] public int[] UnitPhysicalAttacks { get; set; } = Array.Empty<int>();
    [Export] public int[] UnitMagicalAttacks { get; set; } = Array.Empty<int>();

    public PlayableBattleBalanceProfile ToCoreProfile()
    {
        if (ContractId != "godot-playable-lv1-balance-v1" ||
            SkillContentIds.Length != SkillManaCosts.Length || SkillContentIds.Length != SkillDamages.Length ||
            UnitContentIds.Length != UnitPhysicalAttacks.Length || UnitContentIds.Length != UnitMagicalAttacks.Length)
            throw new InvalidOperationException("Playable Lv1 balance profile is invalid.");
        return new PlayableBattleBalanceProfile(
            Enumerable.Range(0, SkillContentIds.Length).ToDictionary(index => new ContentId(SkillContentIds[index]), index => (SkillManaCosts[index], SkillDamages[index])),
            Enumerable.Range(0, UnitContentIds.Length).ToDictionary(index => new ContentId(UnitContentIds[index]), index => (UnitPhysicalAttacks[index], UnitMagicalAttacks[index])));
    }
}
