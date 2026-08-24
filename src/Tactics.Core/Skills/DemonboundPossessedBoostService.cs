using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Units;

namespace Tactics.Core.Skills;

/// <summary>
/// Applies the one-time possessed-form attribute boost (六维 +5) to a battle unit.
/// Derived combat values (hit, dodge, crit) are computed live from
/// <see cref="UnitState.EffectiveAttributes"/> by <see cref="UnitCombatStatRules"/>, so only the
/// attribute projection itself and the explicitly serialized derived values
/// (max HP/MP, move range, initiative) need to be rewritten here.
/// </summary>
public static class DemonboundPossessedBoostService
{
    public const string ContractId = "DEMONBOUND-POSSESSED-BOOST-001";

    /// <summary>Fixed attribute boost applied to every dimension on form entry.</summary>
    public const int AttributeBoost = 5;

    /// <summary>
    /// Returns the unit with its possessed-form attribute boost applied exactly once.
    /// Idempotent: repeated entry events, saves and reloads must never stack the boost.
    /// </summary>
    public static BattleUnitState Apply(BattleUnitState unit)
    {
        DemonboundBattleState? state = unit.DemonboundState;
        if (state is null || !state.IsPossessed || state.PossessedBoostApplied)
            return unit;

        UnitAttributes original = unit.Unit.EffectiveAttributes;
        var boosted = new UnitAttributes(
            original.Strength + AttributeBoost,
            original.Agility + AttributeBoost,
            original.Constitution + AttributeBoost,
            original.Intelligence + AttributeBoost,
            original.Charisma + AttributeBoost,
            original.Luck + AttributeBoost);

        UnitDerivedStats originalDerived = UnitDerivedStatRules.Calculate(original);
        UnitDerivedStats boostedDerived = UnitDerivedStatRules.Calculate(boosted);
        int moveDelta = boostedDerived.MoveRange - originalDerived.MoveRange;
        int moveRange = Math.Clamp(checked(unit.Unit.MoveRange + moveDelta), 2, 5);
        int baseMoveRange = Math.Clamp(checked(unit.Unit.BaseMoveRange + moveDelta), 2, 5);
        float initiative = checked(unit.Unit.Initiative + (boostedDerived.Initiative - originalDerived.Initiative));
        float baseInitiative = checked(unit.Unit.BaseInitiative + (boostedDerived.Initiative - originalDerived.Initiative));

        int newMaxHealth = checked(unit.MaxHealth + (boostedDerived.MaxHealth - originalDerived.MaxHealth));
        int newMaxMana = checked(unit.MaxMana + (boostedDerived.MaxMana - originalDerived.MaxMana));
        int newHealth = Scale(unit.CurrentHealth, unit.MaxHealth, newMaxHealth);
        int newMana = Scale(unit.CurrentMana, unit.MaxMana, newMaxMana);

        var boostedUnit = new UnitState(
            unit.Unit.InstanceId,
            unit.Unit.DefinitionId,
            unit.Unit.Position,
            moveRange,
            initiative,
            unit.Unit.PlayerNumber,
            unit.Unit.SpawnOrdinal,
            unit.Unit.IsAlive,
            unit.Unit.MovementKind,
            boosted,
            baseMoveRange,
            baseInitiative,
            unit.Unit.CombatRole);

        return unit
            .WithUnitFacts(boostedUnit)
            .WithHealthAndMana(newMaxHealth, newHealth, newMaxMana, newMana)
            .WithDemonboundState(state.WithPossessedBoostApplied());
    }

    /// <summary>Scales a current value to a new maximum, preserving the ratio (floor).</summary>
    private static int Scale(int current, int oldMaximum, int newMaximum)
    {
        if (oldMaximum <= 0) return current;
        if (oldMaximum == newMaximum) return current;
        return (int)Math.Floor((long)current * newMaximum / (double)oldMaximum);
    }
}