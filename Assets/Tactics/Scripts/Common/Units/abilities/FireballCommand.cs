using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units.Buffs;
using Tactics.Runtime.BattleLog;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Command to execute a fireball attack: AOE damage + ignite buff on all units in target area.
    /// </summary>
    public readonly struct FireballCommand : ICommand
    {
        private readonly ICell _targetCell;
        private readonly IUnit _caster;
        private readonly List<ICell> _aoeCells;
        private readonly float _damage;
        private readonly int _actionCost;
        private readonly int _manaCost;
        private readonly int _igniteDuration;
        private readonly float _igniteDamage;

        private readonly BuffConfig _igniteBuffConfig;

        public FireballCommand(ICell targetCell, IUnit caster, IEnumerable<ICell> aoeCells, float damage,
            int actionCost = 1, int manaCost = 3, int igniteDuration = 3, float igniteDamage = 1, BuffConfig igniteBuffConfig = null)
        {
            _targetCell = targetCell;
            _caster = caster;
            _aoeCells = new List<ICell>(aoeCells);
            _damage = damage;
            _actionCost = actionCost;
            _manaCost = manaCost;
            _igniteDuration = igniteDuration;
            _igniteDamage = igniteDamage;
            _igniteBuffConfig = igniteBuffConfig;
        }

        public async Task Execute(IUnit unit, IGridController controller)
        {
            var hitUnits = new List<IUnit>();
            foreach (var cell in _aoeCells)
            {
                foreach (var hitUnit in cell.CurrentUnits)
                {
                    if (hitUnit == _caster) continue;

                    hitUnit.ModifyHealth(-_damage, _caster);
                    if (_igniteBuffConfig != null)
                        hitUnit.AddBuff(new Buff(_igniteBuffConfig, _caster, _igniteDuration));
                    hitUnits.Add(hitUnit);
                }
            }

            _caster.Mana -= _manaCost;

            string casterName = _caster is Tactics.Common.Units.INamedUnit nc ? nc.UnitName : _caster.ToString();
            string primaryTarget = hitUnits.FirstOrDefault() is Tactics.Common.Units.INamedUnit nt ? nt.UnitName : "None";

            TBattleLog.Log(new SkillLogData
            {
                Source = casterName,
                SkillName = "Fireball",
                Target = primaryTarget
            });

            await Task.WhenAll(
                controller.UnitManager.MarkAsTargetable(hitUnits)
            );
        }

        public Task Undo(IUnit unit, IGridController controller)
        {
            var caster = _caster;
            var damage = _damage;
            var manaCost = _manaCost;
            var actionCost = _actionCost;
            var aoeCells = _aoeCells;
            foreach (var cell in aoeCells)
            {
                foreach (var hitUnit in cell.CurrentUnits)
                {
                    if (hitUnit == caster) continue;

                    hitUnit.ModifyHealth(+damage, caster);

                    var igniteBuffs = hitUnit.GetActiveBuffs()
                        .Where(b => b.Source != null && ReferenceEquals(b.Source, caster))
                        .ToList();
                    foreach (var buff in igniteBuffs)
                    {
                        hitUnit.RemoveBuff(buff);
                    }
                }
            }

            caster.Mana += manaCost;
            return Task.CompletedTask;
        }

        private static class SerializationKeys
        {
            public const string CasterID = "caster_id";
            public const string TargetCellX = "target_cell_x";
            public const string TargetCellY = "target_cell_y";
            public const string Damage = "damage";
            public const string ActionCost = "action_cost";
            public const string ManaCost = "mana_cost";
        }

        public Dictionary<string, object> Serialize()
        {
            var cellX = _targetCell.GridCoordinates.x;
            var cellY = _targetCell.GridCoordinates.y;
            return new Dictionary<string, object>
            {
                { SerializationKeys.CasterID, _caster.UnitID },
                { SerializationKeys.TargetCellX, cellX },
                { SerializationKeys.TargetCellY, cellY },
                { SerializationKeys.Damage, _damage },
                { SerializationKeys.ActionCost, _actionCost },
                { SerializationKeys.ManaCost, _manaCost }
            };
        }

        public ICommand Deserialize(Dictionary<string, object> actionParams, IGridController gridController)
        {
            var casterId = Convert.ToInt32(actionParams[SerializationKeys.CasterID]);
            var cellX = Convert.ToInt32(actionParams[SerializationKeys.TargetCellX]);
            var cellY = Convert.ToInt32(actionParams[SerializationKeys.TargetCellY]);
            var damage = Convert.ToSingle(actionParams[SerializationKeys.Damage]);
            var actionCost = Convert.ToInt32(actionParams[SerializationKeys.ActionCost]);
            var manaCost = Convert.ToInt32(actionParams[SerializationKeys.ManaCost]);

            var caster = gridController.UnitManager.GetUnits().First(u => u.UnitID == casterId);

            ICell targetCell = null;
            var allCells = gridController.CellManager.GetCells();
            foreach (var c in allCells)
            {
                if (c.GridCoordinates.x == cellX && c.GridCoordinates.y == cellY)
                {
                    targetCell = c;
                    break;
                }
            }

            var aoeCells = targetCell?.GetNeighbours(gridController.CellManager).ToList() ?? new List<ICell>();
            if (targetCell != null)
            {
                aoeCells.Insert(0, targetCell);
            }

            return new FireballCommand(targetCell, caster, aoeCells, damage, actionCost, manaCost);
        }
    }
}
