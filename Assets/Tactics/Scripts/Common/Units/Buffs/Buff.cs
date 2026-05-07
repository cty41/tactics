using Tactics.Common.Controllers;

namespace Tactics.Common.Units.Buffs
{
    public class Buff
    {
        private readonly BuffConfig _config;
        private readonly BuffBehavior _behavior;

        public string BuffName => _config.BuffName;
        public IUnit Owner { get; internal set; }
        public IUnit Source { get; }
        public int RemainingTurns { get; set; }
        public bool IsExpired => RemainingTurns <= 0;
        public BuffConfig Config => _config;

        public Buff(BuffConfig config, IUnit source, int duration)
        {
            _config = config;
            _behavior = new BuffBehavior(config);
            Source = source;
            RemainingTurns = duration;
        }

        public virtual void OnApplied()
        {
            _behavior.OnApplied(this);
        }

        public virtual void OnTurnStart(IGridController gridController)
        {
            _behavior.OnTurnStart(this, gridController);
        }

        public virtual void OnTurnEnd(IGridController gridController)
        {
            RemainingTurns--;
            _behavior.OnTurnEnd(this, gridController);
        }

        public virtual void OnRemoved()
        {
            _behavior.OnRemoved(this);
        }

        public virtual void OnBeforeAttacked(IUnit attacker, ref float damage, ref bool isCritical)
        {
            _behavior.OnBeforeAttacked(this, attacker, ref damage, ref isCritical);
        }

        public virtual void OnDamageTaken(IUnit attacker, float damage)
        {
            _behavior.OnDamageTaken(this, attacker, damage);
        }

        public bool CanAct
        {
            get
            {
                if (!_config.CanAct) return false;
                return _behavior.CanAct;
            }
        }
    }
}
