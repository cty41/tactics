using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Controllers;
using Tactics.Runtime.BattleLog;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Unified command to execute an attack action by a unit on a target unit.
    /// Replaces MeleeAttackCommand and RangedAttackCommand.
    /// </summary>
    public readonly struct AttackCommand : ICommand
    {
        private readonly IUnit _target;
        private readonly float _damage;
        private readonly bool _isRanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="AttackCommand"/> struct.
        /// </summary>
        /// <param name="target">The unit to be attacked.</param>
        /// <param name="damage">The damage value to be inflicted on the target.</param>
        /// <param name="isRanged">Whether this is a ranged attack (affects damage calculation).</param>
        public AttackCommand(IUnit target, float damage, bool isRanged = false)
        {
            _target = target;
            _damage = damage;
            _isRanged = isRanged;
        }

        public async Task Execute(IUnit unit, IGridController controller)
        {
            _target.ModifyHealth(-_damage, unit);
            _target.InvokeAttacked(new UnitAttackedEventArgs(_target, unit, _damage));

            TBattleLog.Log(new AttackLogData
            {
                Attacker = unit is Tactics.Common.Units.INamedUnit na ? na.UnitName : unit.ToString(),
                Target = _target is Tactics.Common.Units.INamedUnit nt ? nt.UnitName : _target.ToString(),
                Damage = _damage
            });

            await Task.WhenAll(
                controller.UnitManager.MarkAsAttacking(unit as Units.Unit, _target as Units.Unit),
                controller.UnitManager.MarkAsDefending(_target as Units.Unit, unit as Units.Unit)
            );
        }

        public Task Undo(IUnit unit, IGridController controller)
        {
            _target?.ModifyHealth(+_damage, unit);
            return Task.CompletedTask;
        }

        private static class SerializationKeys
        {
            public const string TargetID = "target_id";
            public const string Damage = "damage";
            public const string IsRanged = "is_ranged";
        }

        public Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                { SerializationKeys.TargetID, _target.UnitID },
                { SerializationKeys.Damage, _damage },
                { SerializationKeys.IsRanged, _isRanged }
            };
        }

        public ICommand Deserialize(Dictionary<string, object> actionParams, IGridController gridController)
        {
            var targetId = Convert.ToInt32(actionParams[SerializationKeys.TargetID]);
            var damage = Convert.ToSingle(actionParams[SerializationKeys.Damage]);
            var isRanged = actionParams.TryGetValue(SerializationKeys.IsRanged, out var rangedValue) 
                && Convert.ToBoolean(rangedValue);

            var target = gridController.UnitManager.GetUnits()
                .First(u => u.UnitID == targetId);

            return new AttackCommand(target, damage, isRanged);
        }
    }
}
