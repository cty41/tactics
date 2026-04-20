using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Controllers;
using Tactics.Runtime.BattleLog;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Command to execute a ranged attack action by a unit on a target unit.
    /// </summary>
    public readonly struct RangedAttackCommand : ICommand
    {
        private readonly IUnit _target;
        private readonly float _damage;
        private readonly int _actionCost;

        public RangedAttackCommand(IUnit target, float damage, int actionCost = 1)
        {
            _target = target;
            _damage = damage;
            _actionCost = actionCost;
        }

        public async Task Execute(IUnit unit, IGridController controller)
        {
            _target.ModifyHealth(-_damage, unit);
            _target.InvokeAttacked(new UnitAttackedEventArgs(_target, unit, _damage));

            BattleLogger.Log(new AttackLogData
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
            _target.ModifyHealth(+_damage, unit);
            return Task.CompletedTask;
        }

        private static class SerializationKeys
        {
            public const string TargetID = "target_id";
            public const string Damage = "damage";
            public const string ActionCost = "action_cost";
        }

        public Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                { SerializationKeys.TargetID, _target.UnitID },
                { SerializationKeys.Damage, _damage },
                { SerializationKeys.ActionCost, _actionCost }
            };
        }

        public ICommand Deserialize(Dictionary<string, object> actionParams, IGridController gridController)
        {
            var targetId = Convert.ToInt32(actionParams[SerializationKeys.TargetID]);
            var damage = Convert.ToSingle(actionParams[SerializationKeys.Damage]);
            var actionCost = Convert.ToInt32(actionParams[SerializationKeys.ActionCost]);

            var target = gridController.UnitManager.GetUnits()
                .First(u => u.UnitID == targetId);

            return new RangedAttackCommand(target, damage, actionCost);
        }
    }
}
