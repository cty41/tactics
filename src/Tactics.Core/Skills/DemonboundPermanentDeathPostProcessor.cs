using Tactics.Core.Battle;
using Tactics.Core.Randomness;
using Tactics.Core.Statuses;

namespace Tactics.Core.Skills;

/// <summary>
/// Unified permanent-death settlement for friendly lethal hits caused by a possessed demonbound.
/// Every damage source submits its defeat fact to <see cref="BattleDefeatResolver"/>, then this
/// service decides once whether the defeated ally is permanently removed from the run.
/// </summary>
public static class DemonboundPermanentDeathPostProcessor
{
    public const string ContractId = "DEMONBOUND-PERMADEATH-LUCK-001";

    /// <summary>Baseline chance without any luck correction.</summary>
    public const int BaseChancePercent = 25;

    /// <summary>Luck below or equal to this threshold is not corrected.</summary>
    public const int LuckThreshold = 5;

    /// <summary>Each luck point above the threshold reduces the chance by this many percent points.</summary>
    public const int LuckReductionPerPoint = 2;

    /// <summary>
    /// Assesses the permanent-death outcome after a possessed demonbound defeats a friendly unit.
    /// Consumes exactly one deterministic random draw per survival-to-defeat transition and never
    /// rolls twice for the same source hit (the target already carries the permanent-death status).
    /// </summary>
    public static BattleState Apply(BattleState state, BattleUnitState actor, BattleUnitState previous,
        BattleUnitState updated, ICollection<BattleEvent> events)
    {
        if (actor.DemonboundState?.IsPossessed != true) return state;
        if (!previous.IsAlive || updated.IsAlive) return state;
        if (updated.Unit.PlayerNumber != actor.Unit.PlayerNumber) return state;
        // Permanent death only applies to formal party members, never to the acting
        // possessed unit itself nor to friendly summons/decoys (contract excludes
        // non-formal members from formal roster settlement).
        if (updated.Unit.InstanceId == actor.Unit.InstanceId) return state;
        if (updated.SummonOwnerId is not null) return state;
        if (updated.Statuses.ContainsKey(SkillRuntimeService.RunPermanentDeathStatusId)) return state;

        int chance = ChancePercent(updated);
        var random = new DeterministicRandom(state.RandomState);
        int roll = random.NextInt(100);
        bool permanent = roll < chance;
        BattleState next = state.WithRandomState(random.State);
        if (permanent)
        {
            updated = updated.WithStatus(new BattleStatusState(SkillRuntimeService.RunPermanentDeathStatusId,
                actor.Unit.InstanceId, int.MaxValue, 0, polarity: StatusPolarity.Harmful));
            next = next.WithUnit(updated);
        }
        events.Add(new RunPermanentDeathRolledEvent(actor.Unit.InstanceId, updated.Unit.InstanceId,
            roll, permanent, random.State));
        return next;
    }

    /// <summary>Chance = max(0, 25 - max(0, targetLuck - 5) * 2).</summary>
    public static int ChancePercent(BattleUnitState target)
    {
        int luck = target.Unit.EffectiveAttributes.Luck;
        return Math.Max(0, BaseChancePercent - Math.Max(0, luck - LuckThreshold) * LuckReductionPerPoint);
    }
}