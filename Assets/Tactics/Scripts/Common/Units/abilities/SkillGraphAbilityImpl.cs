using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Interactables;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Runtime.BattleLog;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// SkillGraph 能力实现。
    /// 通过 SkillGraphRunner 执行技能图。
    /// </summary>
    public class SkillGraphAbilityImpl : IAbility, IAiExecutableAbility
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
        public SkillGraphAsset SkillGraphAsset => _config.SkillGraph;
        public int TargetRange => _config.TargetRange;

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

            _ = ExecuteSkillGraphAsync(unit.CurrentCell, gridController);
        }

        public void OnCellClicked(ICell cell, IGridController gridController)
        {
            if (!CanPerform(gridController)) return;
            if (_validTargetCells == null || !_validTargetCells.Contains(cell)) return;

            _ = ExecuteSkillGraphAsync(cell, gridController);
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

        public async Task ExecuteEffectsAsync(IEnumerable<IUnit> targets, IGridController gridController)
        {
            if (targets == null) return;

            _gridController = gridController;
            _validTargetCells = CalculateValidTargetCells();

            foreach (var target in targets)
            {
                if (target?.CurrentCell == null) continue;
                if (_validTargetCells == null || !_validTargetCells.Contains(target.CurrentCell))
                {
                    TLog.Warning($"[SkillGraphAbilityImpl] AI target {target.UnitID} out of range for '{DisplayName}'.");
                    continue;
                }

                await ExecuteSkillGraphAsync(target.CurrentCell, gridController);
            }
        }

        public async Task<bool> ExecuteMoveForAI(ICell destination, IEnumerable<ICell> path, IGridController gridController)
        {
            if (destination == null || path == null) return false;
            if (destination.Equals(_owner.CurrentCell)) return true;

            var pathList = path.ToList();
            if (pathList.Count == 0) return false;

            var tcs = new TaskCompletionSource<bool>();
            _owner.AIExecuteAbility(new MoveCommand(_owner.CurrentCell, destination, pathList), gridController, tcs);

            var timeoutTask = Task.Delay(2000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            if (completedTask == timeoutTask)
            {
                TLog.Info("[SkillGraphAbilityImpl] ExecuteMoveForAI timed out (2s), assuming executed.");
            }

            return true;
        }

        public async Task<SkillGraphRuntimeTestResult> ExecuteForTestAsync(ICell selectedCell, IGridController gridController)
        {
            if (gridController == null)
                throw new ArgumentNullException(nameof(gridController));

            OnAbilitySelected(gridController);

            var result = CreateTestResult();
            result.Caster = SkillGraphTestUnitSnapshot.Capture(_owner);
            result.PrimaryTarget = CaptureTargetSnapshot(selectedCell);

            if (!CanPerform(gridController))
            {
                result.ExecutionState = SkillGraphExecutionState.Failed;
                result.LastError = "Not enough mana.";
                result.StepCount = 0;
                return result;
            }

            if (selectedCell == null)
            {
                result.ExecutionState = SkillGraphExecutionState.Failed;
                result.LastError = "No target selected.";
                result.StepCount = 0;
                return result;
            }

            bool hasUnitsOnCell = selectedCell.CurrentUnits != null && selectedCell.CurrentUnits.Count > 0;
            if (_validTargetCells == null || _validTargetCells.Count == 0)
            {
                result.ExecutionState = SkillGraphExecutionState.Failed;
                result.LastError = hasUnitsOnCell ? "Target out of range." : "No valid target in range.";
                result.StepCount = 0;
                return result;
            }

            if (!_validTargetCells.Contains(selectedCell))
            {
                result.ExecutionState = SkillGraphExecutionState.Failed;
                result.LastError = hasUnitsOnCell ? "Target out of range." : "No valid target in range.";
                result.StepCount = 0;
                return result;
            }

            return await ExecuteSkillGraphAsync(selectedCell, gridController);
        }

        public void InvokeAbilitySelected()
        {
            AbilitySelected?.Invoke(this);
        }

        public void InvokeAbilityDeselected()
        {
            AbilityDeselected?.Invoke(this);
        }

        private async Task<SkillGraphRuntimeTestResult> ExecuteSkillGraphAsync(ICell selectedCell, IGridController gridController)
        {
            var runtimeDef = SkillGraphRuntimeDefinition.FromAsset(_config.SkillGraph);
            var context = new SkillExecutionContext(_owner, _config.SkillGraph, runtimeDef, gridController);
            var testResult = CreateTestResult();

            // Pre-set target from cell click
            var unitsOnCell = selectedCell.CurrentUnits;
            if (unitsOnCell != null && unitsOnCell.Count > 0)
            {
                context.PrimaryTarget = unitsOnCell[0];
            }
            context.TargetPoint = selectedCell;
            testResult.PrimaryTarget = SkillGraphTestUnitSnapshot.Capture(context.PrimaryTarget);

            LogSkillUse(context.PrimaryTarget);

            var runner = new SkillGraphRunner();
            var executionState = await runner.Execute(context);

            if (executionState == SkillGraphExecutionState.Completed)
            {
                TLog.Info($"[SkillGraphAbility] '{DisplayName}' completed successfully.");
            }
            else
            {
                TLog.Warning($"[SkillGraphAbility] '{DisplayName}' ended with state: {executionState}. Error: {context.LastError}");
            }

            // Mark basic ability used or deduct mana
            if (_config.IsBasicAbility)
                _owner.MarkBasicAbilityUsed(_config.DisplayName);
            else
                _owner.Mana -= _config.ManaCost;

            gridController.GridState = new GridStateAwaitInput();

            testResult.ExecutionState = executionState;
            testResult.LastError = context.LastError;
            testResult.StepCount = context.StepCount;
            testResult.Caster = SkillGraphTestUnitSnapshot.Capture(_owner);
            testResult.PrimaryTarget = SkillGraphTestUnitSnapshot.Capture(context.PrimaryTarget);
            return testResult;
        }

        private void LogSkillUse(IUnit target)
        {
            if (!TBattleLog.IsBattleActive)
                return;

            TBattleLog.Log(new SkillLogData
            {
                Source = GetUnitName(_owner),
                SkillName = DisplayName,
                Target = GetUnitName(target)
            });
        }

        private static string GetUnitName(IUnit unit)
        {
            if (unit is INamedUnit named && !string.IsNullOrWhiteSpace(named.UnitName))
                return named.UnitName;

            return unit == null ? null : $"Unit_{unit.UnitID}";
        }

        private SkillGraphRuntimeTestResult CreateTestResult()
        {
            return new SkillGraphRuntimeTestResult
            {
                Name = DisplayName,
                GraphName = _config.SkillGraph?.DisplayName ?? DisplayName
            };
        }

        private static SkillGraphTestUnitSnapshot CaptureTargetSnapshot(ICell selectedCell)
        {
            if (selectedCell?.CurrentUnits == null || selectedCell.CurrentUnits.Count == 0)
                return null;

            return SkillGraphTestUnitSnapshot.Capture(selectedCell.CurrentUnits[0]);
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

            if (FirstSelectionRequiresTeleport())
            {
                foreach (var cell in allCells)
                {
                    int distance = cell.GetDistance(ownerCell);
                    if (distance > 0 && distance <= _config.TargetRange && !cell.IsTaken)
                        displayCells.Add(cell);
                }
                return displayCells;
            }

            if (FirstSelectionRequiresCorpse())
            {
                foreach (var cell in allCells)
                {
                    if (HasCorpseInteractable(cell))
                        displayCells.Add(cell);
                }
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

            if (FirstSelectionRequiresTeleport())
            {
                foreach (var cell in allCells)
                {
                    int distance = cell.GetDistance(ownerCell);
                    if (distance > 0 && distance <= _config.TargetRange && !cell.IsTaken)
                        validCells.Add(cell);
                }
                return validCells;
            }

            if (FirstSelectionRequiresCorpse())
            {
                foreach (var cell in allCells)
                {
                    if (HasCorpseInteractable(cell))
                        validCells.Add(cell);
                }
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

        public SkillGraphNodeRecord FindFirstSelectionNode()
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

        private bool FirstSelectionRequiresTeleport()
        {
            var first = FindFirstSelectionNode();
            return first is TeleportNodeRecord;
        }

        private bool FirstSelectionRequiresCorpse()
        {
            var first = FindFirstSelectionNode();
            return first is SelectCorpseTargetNodeRecord;
        }

        private bool HasCorpseInteractable(ICell cell)
        {
            if (cell == null) return false;
            foreach (var interactable in cell.CurrentInteractables)
            {
                if (interactable is Corpse corpse && !corpse.IsDestroyed)
                    return true;
            }
            return false;
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
