namespace Tactics.Core.Battle;

/// <summary>Applies the one-time defeated-unit and corpse transaction shared by every damage path.</summary>
public static class BattleDefeatResolver
{
    public static BattleState Apply(BattleState state, BattleUnitState previous, BattleUnitState updated, ICollection<BattleEvent> events)
    {
        if (!previous.IsAlive || updated.IsAlive) return state;
        events.Add(new UnitDefeatedEvent(updated.Unit.InstanceId));
        if (!updated.CanProduceCorpse || state.Corpses.Contains(updated.Unit.Position)) return state;
        events.Add(new CorpseCreatedEvent(updated.Unit.Position, updated.Unit.InstanceId));
        return state.WithCorpse(updated.Unit.Position);
    }
}
