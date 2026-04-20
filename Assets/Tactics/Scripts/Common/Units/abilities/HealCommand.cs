using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Controllers;
using Tactics.Runtime.BattleLog;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Command to execute a healing action on a friendly unit.
    /// </summary>
    public readonly struct HealCommand : ICommand
    {
        private readonly IUnit _target;
        private readonly float _healAmount;
        private readonly IUnit _caster;
        private readonly int _actionCost;
        private readonly float _actualHeal;

        public HealCommand(IUnit target, IUnit caster, float healAmount = 3, int actionCost = 1)
        {
            _target = target;
            _caster = caster;
            _healAmount = healAmount;
            _actionCost = actionCost;
            _actualHeal = Math.Min(healAmount, _target.MaxHealth - _target.Health);
        }

        public async Task Execute(IUnit unit, IGridController controller)
        {
            if (_actualHeal <= 0)
            {
                return;
            }

            _target.ModifyHealth(+_actualHeal, _caster);

            string targetName = _target is Tactics.Common.Units.INamedUnit nt ? nt.UnitName : _target.ToString();
            string casterName = _caster is Tactics.Common.Units.INamedUnit nc ? nc.UnitName : _caster.ToString();

            BattleLogger.Log(new HealLogData
            {
                Healer = casterName,
                Target = targetName,
                HealAmount = _actualHeal,
                RemainingHealth = _target.Health
            });

            await Task.WhenAll(
                controller.UnitManager.MarkAsTargetable(new[] { _target })
            );
        }

        public Task Undo(IUnit unit, IGridController controller)
        {
            _target.ModifyHealth(-_actualHeal, _caster);
            return Task.CompletedTask;
        }

        private static class SerializationKeys
        {
            public const string TargetID = "target_id";
            public const string CasterID = "caster_id";
            public const string HealAmount = "heal_amount";
            public const string ActionCost = "action_cost";
        }

        public Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                { SerializationKeys.TargetID, _target.UnitID },
                { SerializationKeys.CasterID, _caster.UnitID },
                { SerializationKeys.HealAmount, _healAmount },
                { SerializationKeys.ActionCost, _actionCost }
            };
        }

        public ICommand Deserialize(Dictionary<string, object> actionParams, IGridController gridController)
        {
            var targetId = Convert.ToInt32(actionParams[SerializationKeys.TargetID]);
            var casterId = Convert.ToInt32(actionParams[SerializationKeys.CasterID]);
            var healAmount = Convert.ToSingle(actionParams[SerializationKeys.HealAmount]);
            var actionCost = Convert.ToInt32(actionParams[SerializationKeys.ActionCost]);

            var units = gridController.UnitManager.GetUnits();
            var target = units.First(u => u.UnitID == targetId);
            var caster = units.First(u => u.UnitID == casterId);

            return new HealCommand(target, caster, healAmount, actionCost);
        }
    }
}
