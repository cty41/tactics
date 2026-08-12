using Tactics.Core.Content;
using Tactics.Core.Skills;

namespace Tactics.Application.Battle;

/// <summary>Adapter-owned numeric overrides for the playable Godot slice.</summary>
public sealed class PlayableBattleBalanceProfile
{
    private readonly IReadOnlyDictionary<ContentId, (int Mana, int Damage)> _skills;
    private readonly IReadOnlyDictionary<ContentId, (int Physical, int Magical)> _units;

    public PlayableBattleBalanceProfile(
        IReadOnlyDictionary<ContentId, (int Mana, int Damage)> skills,
        IReadOnlyDictionary<ContentId, (int Physical, int Magical)> units)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _units = units ?? throw new ArgumentNullException(nameof(units));
    }

    public SkillDefinition Apply(SkillDefinition source)
    {
        if (!_skills.TryGetValue(source.ContentId, out (int Mana, int Damage) value)) return source;
        return new SkillDefinition(source.ContentId, source.SourceId, source.Role, source.Kind, source.Level,
            value.Mana, source.MinRange, source.MaxRange, source.ExecutionKind, value.Damage,
            source.DamageKind, source.StatusContentId, source.StatusDuration, source.Hidden,
            source.ExternalDependency, source.IsBasicAbility, source.MaxUsesPerTurn, source.BranchId,
            source.PrerequisiteContentId, source.GrowthVisible, source.ExecutionProfile);
    }

    public (int Physical, int Magical) Attacks(ContentId unitId) =>
        _units.TryGetValue(unitId, out (int Physical, int Magical) value) ? value : (2, 2);
}
