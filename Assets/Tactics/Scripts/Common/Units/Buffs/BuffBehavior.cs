using System;
using Tactics.Common.Controllers;

namespace Tactics.Common.Units.Buffs
{
    [Serializable]
    public abstract class BuffBehavior
    {
        public virtual void OnApplied(Buff buff) { }
        public virtual void OnBeforeAttacked(Buff buff, IUnit attacker, ref float damage, ref bool isCritical) { }
        public virtual void OnDamageTaken(Buff buff, IUnit attacker, float damage) { }
        public virtual void OnTurnStart(Buff buff, IGridController gridController) { }
        public virtual void OnTurnEnd(Buff buff, IGridController gridController) { }
        public virtual void OnRemoved(Buff buff) { }
        public virtual bool CanAct => true;
    }
}
