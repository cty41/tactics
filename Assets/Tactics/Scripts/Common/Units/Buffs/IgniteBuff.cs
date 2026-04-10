using Tactics.Common.Controllers;
using Tactics.Runtime.BattleLog;

namespace Tactics.Common.Units.Buffs
{
    /// <summary>
    /// A damage-over-time buff that applies damage each turn for a set duration.
    /// Commonly known as "点燃" (Ignite/Burning).
    /// </summary>
    public class IgniteBuff : Buff
    {
        private readonly float _damagePerTurn;

        /// <summary>
        /// Display name of the buff.
        /// </summary>
        public override string BuffName => "Ignite";

        /// <summary>
        /// Creates a new IgniteBuff.
        /// </summary>
        /// <param name="source">The unit that applied the ignite effect.</param>
        /// <param name="duration">Number of turns the ignite lasts.</param>
        /// <param name="damagePerTurn">Amount of damage dealt each turn.</param>
        public IgniteBuff(IUnit source, int duration, float damagePerTurn)
            : base(source, duration)
        {
            _damagePerTurn = damagePerTurn;
        }

        /// <summary>
        /// Called at the start of the owner's turn. Applies damage and logs the event.
        /// </summary>
        public override void OnTurnStart(IGridController gridController)
        {
            if (Owner == null)
            {
                return;
            }

            Owner.ModifyHealth(-_damagePerTurn, Source);

            string ownerName = Owner is Tactics.Common.Units.INamedUnit named ? named.UnitName : Owner.ToString();

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
