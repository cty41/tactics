using Tactics.Common.Controllers;
using Tactics.Runtime.BattleLog;

namespace Tactics.Common.Units.Buffs
{
    public class IgniteBuff : Buff
    {
        private readonly float _damagePerTurn;

        public IgniteBuff(BuffConfig config, IUnit source, int duration, float damagePerTurn)
            : base(config, source, duration)
        {
            _damagePerTurn = damagePerTurn;
        }

        public override void OnTurnStart(IGridController gridController)
        {
            base.OnTurnStart(gridController);

            if (Owner == null) return;

            Owner.ModifyHealth(-_damagePerTurn, Source);

            string ownerName = Owner is INamedUnit named ? named.UnitName : Owner.ToString();

            BattleLogger.Log(new DamageLogData
            {
                Source = BuffName,
                Target = ownerName,
                Damage = _damagePerTurn,
                RemainingHealth = Owner.Health
            });
        }
    }
}
