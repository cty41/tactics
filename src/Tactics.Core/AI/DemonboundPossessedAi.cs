using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Content;
using Tactics.Core.Skills;

namespace Tactics.Core.AI;

/// <summary>
/// Formal possessed-demonbound AI configuration. The possessed AI always uses the unified
/// target relationship strategy (enemy/ally/summons share one candidate pool) and drives its
/// active, non-meditation learned skills; the skill list itself is dynamic because the possessed
/// form projects learned skills to their highest branch level.
/// </summary>
public static class DemonboundPossessedAi
{
    public const string ContractId = "DEMONBOUND-POSSESSED-TARGET-POOL-001";

    /// <summary>Stable content identity shared by the runtime AI and any future authored resource.</summary>
    public static readonly ContentId ContentId = new("ai.demonbound.possessed");

    public const AiArchetype Archetype = AiArchetype.Charger;

    /// <summary>Distance / damage / target-count / harmful-status weights used by the charger profile.</summary>
    public static readonly AiProfileDefinition Profile = new(5, 3, 2, 1);

    /// <summary>
    /// Builds the possessed AI definition with the projected active skill identities.
    /// Passives and meditation are never AI actions.
    /// </summary>
    public static AiDefinition For(IReadOnlyList<SkillDefinition> skillsFor) => new(
        ContentId,
        Archetype,
        Profile,
        skillsFor.Where(skill => !skill.IsPassive &&
            skill.ExecutionKind != SkillExecutionKind.Meditation).Select(skill => skill.ContentId).ToArray(),
        Array.Empty<ContentId>());
}