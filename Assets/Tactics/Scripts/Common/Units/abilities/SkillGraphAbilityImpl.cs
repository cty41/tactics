using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Skills.Graph;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// SkillGraph 能力实现。
    /// 通过 SkillGraphRunner 执行技能图。
    /// </summary>
    public class SkillGraphAbilityImpl : IAbility
    {
        public event Action<IAbility> AbilitySelected;
        public event Action<IAbility> AbilityDeselected;

        private readonly IUnit _owner;
        private readonly SkillGraphAbilityConfig _config;
        private IGridController _gridController;
        private HashSet<ICell> _validTargetCells;
        private HashSet<ICell> _displayCells;

        public IUnit UnitReference { get; set; }
        public string DisplayName => _config.DisplayName;
        public Sprite Icon => _config.Icon;
        public int Cost => _config.ManaCost;

        public SkillGraphAbilityImpl(IUnit owner, SkillGraphAbilityConfig config)
        {
            _owner = owner;
            _config = config;
            UnitReference = owner;
        }

        public void Initialize(IGridController gridController)
        {
            _gridController = gridController;
        }

        public void OnAbilitySelected(IGridController gridController)
        {
            _gridController = gridController;
            _validTargetCells = CalculateValidTargetCells();
            _displayCells = CalculateDisplayCells();
        }

        public void Display(IGridController gridController)
        {
            if (_displayCells != null && _displayCells.Count > 0)
            {
                gridController.CellManager.MarkAsReachable(_displayCells);
            }
        }

        public void CleanUp(IGridController gridController)
        {
            if (_displayCells != null)
            {
                gridController.CellManager.UnMark(_displayCells);
                _displayCells = null;
            }
            if (_validTargetCells != null)
            {
                _validTargetCells = null;
            }
        }

        public void OnUnitClicked(IUnit unit, IGridController gridController)
        {
            if (!CanPerform(gridController)) return;
            if (unit.CurrentCell == null) return;
            if (_validTargetCells == null || !_validTargetCells.Contains(unit.CurrentCell)) return;

            _ = ExecuteSkillGraph(unit.CurrentCell, gridController);
        }

        public void OnCellClicked(ICell cell, IGridController gridController)
        {
            if (!CanPerform(gridController)) return;
            if (_validTargetCells == null || !_validTargetCells.Contains(cell)) return;

            _ = ExecuteSkillGraph(cell, gridController);
        }

        public void OnUnitHighlighted(IUnit unit, IGridController gridController) { }
        public void OnUnitDehighlighted(IUnit unit, IGridController gridController) { }
        public void OnUnitDestroyed(IGridController gridController) { }
        public void OnCellHighlighted(ICell cell, IGridController gridController) { }
        public void OnCellDehighlighted(ICell cell, IGridController gridController) { }
        public void OnAbilityDeselected(IGridController gridController) { }
        public void OnTurnStart(IGridController gridController) { }
        public void OnTurnEnd(IGridController gridController) { }

        public bool CanPerform(IGridController gridController)
        {
            if (_config.SkillGraph == null) return false;
            if (_config.IsBasicAbility)
                return !_owner.HasUsedBasicAbilityThisTurn(_config.DisplayName);
            return _owner.Mana >= _config.ManaCost;
        }

        public void InvokeAbilitySelected()
        {
            AbilitySelected?.Invoke(this);
        }

        public void InvokeAbilityDeselected()
        {
            AbilityDeselected?.Invoke(this);
        }

        private async Task ExecuteSkillGraph(ICell selectedCell, IGridController gridController)
        {
            var runtimeDef = SkillGraphRuntimeDefinition.FromAsset(_config.SkillGraph);
            var context = new SkillExecutionContext(_owner, _config.SkillGraph, runtimeDef, gridController);

            // Pre-set target from cell click
            var unitsOnCell = selectedCell.CurrentUnits;
            if (unitsOnCell != null && unitsOnCell.Count > 0)
            {
                context.PrimaryTarget = unitsOnCell[0];
            }
            context.TargetPoint = selectedCell;

            var runner = new SkillGraphRunner();
            var result = await runner.Execute(context);

            if (result == SkillGraphExecutionState.Completed)
            {
                TLog.Info($"[SkillGraphAbility] '{DisplayName}' completed successfully.");
            }
            else
            {
                TLog.Warning($"[SkillGraphAbility] '{DisplayName}' ended with state: {result}. Error: {context.LastError}");
            }

            // Mark basic ability used or deduct mana
            if (_config.IsBasicAbility)
                _owner.MarkBasicAbilityUsed(_config.DisplayName);
            else
                _owner.Mana -= _config.ManaCost;

            gridController.GridState = new GridStateAwaitInput();
        }

        private HashSet<ICell> CalculateDisplayCells()
        {
            var displayCells = new HashSet<ICell>();
            if (_gridController == null) return displayCells;

            int maxRange = _config.TargetRange;
            var allCells = _gridController.CellManager.GetCells();
            var ownerCell = _owner.CurrentCell;
            int minRange = GetMinRangeFromGraph();
            bool cardinalOnly = UsesCardinalDash();

            if (FirstSelectionRequiresSelf())
            {
                displayCells.Add(ownerCell);
                return displayCells;
            }

            if (FirstSelectionRequiresMoveDestination())
            {
                _owner.CachePaths(_gridController.CellManager);
                var destinations = _owner.GetAvailableDestinations(_gridController.CellManager.GetCells());
                foreach (var cell in destinations)
                    displayCells.Add(cell);
                return displayCells;
            }

            if (FirstSelectionRequiresAlly())
            {
                int allyRange = GetAllyRangeFromGraph();
                foreach (var cell in allCells)
                {
                    int dist = cell.GetDistance(ownerCell);
                    if (dist > 0 && dist <= allyRange && HasFriendlyUnit(cell))
                        displayCells.Add(cell);
                }
                return displayCells;
            }

            foreach (var cell in allCells)
            {
                int distance = cell.GetDistance(ownerCell);
                if (distance < minRange || distance > maxRange)
                    continue;

                if (cardinalOnly)
                {
                    int dx = cell.GridCoordinates.x - ownerCell.GridCoordinates.x;
                    int dy = cell.GridCoordinates.y - ownerCell.GridCoordinates.y;
                    bool isCardinal = (dx == 0) ^ (dy == 0);
                    if (!isCardinal)
                        continue;
                }

                displayCells.Add(cell);
            }

            return displayCells;
        }

        private int GetMinRangeFromGraph()
        {
            var first = FindFirstSelectionNode();
            if (first is SelectPrimaryTargetNodeRecord select)
                return select.MinRange;
            return 0;
        }

        private HashSet<ICell> CalculateValidTargetCells()
        {
            var validCells = new HashSet<ICell>();
            if (_gridController == null) return validCells;

            int range = _config.TargetRange;
            var allCells = _gridController.CellManager.GetCells();
            var ownerCell = _owner.CurrentCell;
            bool cardinalOnly = UsesCardinalDash();
            bool requiresEnemy = FirstSelectionRequiresEnemy();
            bool requiresSelf = FirstSelectionRequiresSelf();

            if (requiresSelf)
            {
                validCells.Add(ownerCell);
                return validCells;
            }

            if (FirstSelectionRequiresMoveDestination())
            {
                _owner.CachePaths(_gridController.CellManager);
                var destinations = _owner.GetAvailableDestinations(_gridController.CellManager.GetCells());
                foreach (var cell in destinations)
                    validCells.Add(cell);
                return validCells;
            }

            if (FirstSelectionRequiresAlly())
            {
                int allyRange = GetAllyRangeFromGraph();
                foreach (var cell in allCells)
                {
                    int dist = cell.GetDistance(ownerCell);
                    if (dist > 0 && dist <= allyRange && HasFriendlyUnit(cell))
                        validCells.Add(cell);
                }
                return validCells;
            }

            foreach (var cell in allCells)
            {
                int distance = cell.GetDistance(ownerCell);
                if (distance <= 0 || distance > range)
                    continue;

                if (cardinalOnly)
                {
                    int dx = cell.GridCoordinates.x - ownerCell.GridCoordinates.x;
                    int dy = cell.GridCoordinates.y - ownerCell.GridCoordinates.y;
                    bool isCardinal = (dx == 0) ^ (dy == 0);
                    if (!isCardinal)
                        continue;
                }

                if (requiresEnemy && !HasEnemyUnit(cell))
                    continue;

                if (distance <= range)
                {
                    validCells.Add(cell);
                }
            }

            return validCells;
        }

        private bool HasEnemyUnit(ICell cell)
        {
            if (_gridController == null || cell == null) return false;
            foreach (var unit in cell.CurrentUnits)
            {
                if (unit != null && unit.PlayerNumber != _owner.PlayerNumber)
                    return true;
            }
            return false;
        }

        private SkillGraphNodeRecord FindFirstSelectionNode()
        {
            if (_config?.SkillGraph == null) return null;
            var nodes = _config.SkillGraph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node is StartNodeRecord) continue;
                return node;
            }
            return null;
        }

        private bool FirstSelectionRequiresEnemy()
        {
            var first = FindFirstSelectionNode();
            if (first == null) return false;

            if (first is SelectPrimaryTargetNodeRecord)
            {
                return !GraphContainsDashToTarget();
            }

            return false;
        }

        private bool FirstSelectionRequiresSelf()
        {
            var first = FindFirstSelectionNode();
            return first is SelectSelfNodeRecord;
        }

        private bool FirstSelectionRequiresAlly()
        {
            var first = FindFirstSelectionNode();
            return first is SelectAllyNodeRecord;
        }

        private bool FirstSelectionRequiresMoveDestination()
        {
            var first = FindFirstSelectionNode();
            return first is SelectMoveDestinationNodeRecord;
        }

        private int GetAllyRangeFromGraph()
        {
            var first = FindFirstSelectionNode();
            if (first is SelectAllyNodeRecord select)
                return select.MaxRange;
            return 1;
        }

        private bool HasFriendlyUnit(ICell cell)
        {
            if (_gridController == null || cell == null) return false;
            foreach (var unit in cell.CurrentUnits)
            {
                if (unit != null && unit.PlayerNumber == _owner.PlayerNumber && !ReferenceEquals(unit, _owner))
                    return true;
            }
            return false;
        }

        private bool GraphContainsDashToTarget()
        {
            if (_config?.SkillGraph == null) return false;
            var nodes = _config.SkillGraph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is DashToTargetNodeRecord)
                    return true;
            }
            return false;
        }

        private bool UsesCardinalDash()
        {
            if (_config?.SkillGraph == null)
                return false;

            var nodes = _config.SkillGraph.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is DashToTargetNodeRecord || nodes[i] is DashToAllyNodeRecord)
                    return true;
            }

            return false;
        }
    }
}
