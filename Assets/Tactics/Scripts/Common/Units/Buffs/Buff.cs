using System.Collections.Generic;
using Tactics.Common.Controllers;

namespace Tactics.Common.Units.Buffs
{
    public class Buff
    {
        private readonly BuffConfig _config;
        private readonly IReadOnlyList<BuffBehavior> _behaviors;

        public string BuffName => _config.BuffName;
        public IUnit Owner { get; internal set; }
        public IUnit Source { get; }
        public int RemainingTurns { get; set; }
        public bool IsExpired => RemainingTurns <= 0;
        public BuffConfig Config => _config;

        public Buff(BuffConfig config, IUnit source, int duration)
        {
            _config = config;
            _behaviors = config.Behaviors;
            Source = source;
            RemainingTurns = duration;
        }

        public virtual void OnApplied()
        {
            foreach (var b in _behaviors)
                b.OnApplied(this);
        }

        public virtual void OnTurnStart(IGridController gridController)
        {
            foreach (var b in _behaviors)
                b.OnTurnStart(this, gridController);
        }

        public virtual void OnTurnEnd(IGridController gridController)
        {
            RemainingTurns--;
            foreach (var b in _behaviors)
                b.OnTurnEnd(this, gridController);
        }

        public virtual void OnRemoved()
        {
            foreach (var b in _behaviors)
                b.OnRemoved(this);
        }

        public virtual void OnBeforeAttacked(IUnit attacker, ref float damage, ref bool isCritical)
        {
            foreach (var b in _behaviors)
                b.OnBeforeAttacked(this, attacker, ref damage, ref isCritical);
        }

        public virtual void OnDamageTaken(IUnit attacker, float damage)
        {
            foreach (var b in _behaviors)
                b.OnDamageTaken(this, attacker, damage);
        }

        public bool CanAct
        {
            get
            {
                if (!_config.CanAct) return false;
                foreach (var b in _behaviors)
                    if (!b.CanAct) return false;
                return true;
            }
        }
    }
}
