using System;
using Tactics.Runtime.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Cells;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Units;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Generic ability implementation that acts as an event coordinator between
    /// the Grid state system and the data-driven ability effects.
    /// Also handles movement abilities when DisplayName is "Move".
    /// </summary>
    public class GenericAbilityImpl : IAbility, IAiExecutableAbility
    {
        public event Action<IAbility> AbilitySelected;
        public event Action<IAbility> AbilityDeselected;

        private readonly IUnit _owner;
        private readonly AbilityConfig _config;
        private ICell _selectedCell;
        private IEnumerable<IUnit> _pendingTargets;
        private IGridController _gridController;

        // Movement-specific fields
        private HashSet<ICell> _cellsInMovementRange;
        private IEnumerable<ICell> _currentPath;

        // Targeting fields for displaying valid target cells
        private HashSet<ICell> _validTargetCells;

        // Active AoE cells tracked for proper cleanup (may extend beyond _validTargetCells)
        private HashSet<ICell> _activeAoeCells;

        public IUnit UnitReference { get; set; }
        public string DisplayName => _config.DisplayName;
        public Sprite Icon => _config.Icon;
        public int Cost => _config.ManaCost;
        public AbilityConfig Config => _config;

        public GenericAbilityImpl(IUnit owner, AbilityConfig config)
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
            _owner.CachePaths(gridController.CellManager);

            if (DisplayName == "Move")
            {
                bool canMove = !_owner.HasUsedBasicAbilityThisTurn("Move");
                _cellsInMovementRange = canMove
                    ? new HashSet<ICell>(_owner.GetAvailableDestinations(gridController.CellManager.GetCells()))
                    : new HashSet<ICell>();
                _currentPath = Enumerable.Empty<ICell>();
            }
            else if (_config.TargetingStrategy != null)
            {
                // Pre-calculate valid target cells for display
                _validTargetCells = CalculateValidTargetCells();
            }
        }

        public void Display(IGridController gridController)
        {
            if (DisplayName == "Move")
            {
                if (gridController.CellManager is TilemapCellManager tcm)
                {
                    tcm.SetReachableMovementMode(true);
                }
                if (_cellsInMovementRange != null && _cellsInMovementRange.Count > 0)
                {
                    gridController.CellManager.MarkAsReachable(_cellsInMovementRange);
                }
            }
            else
            {
                if (gridController.CellManager is TilemapCellManager tcm)
                {
                    tcm.SetReachableMovementMode(false);
                }
                if (_validTargetCells != null && _validTargetCells.Count > 0)
                {
                    gridController.CellManager.MarkAsReachable(_validTargetCells);
                }
                else if (_config.TargetingStrategy != null)
                {
                    _config.TargetingStrategy.DisplayPreview(gridController);
                }
            }
        }

        public void CleanUp(IGridController gridController)
        {
            if (DisplayName == "Move")
            {
                if (_cellsInMovementRange != null)
                {
                    gridController.CellManager.UnMark(_cellsInMovementRange.Union(_currentPath ?? Enumerable.Empty<ICell>()));
                }
                _cellsInMovementRange = null;
                _currentPath = null;
            }
            else
            {
                if (_activeAoeCells != null && _activeAoeCells.Count > 0)
                {
                    gridController.CellManager.UnMark(_activeAoeCells);
                    _activeAoeCells = null;
                }
                if (_validTargetCells != null)
                {
                    gridController.CellManager.UnMark(_validTargetCells);
                }
                _validTargetCells = null;
                if (_config.TargetingStrategy != null)
                {
                    _config.TargetingStrategy.CleanUpPreview(gridController);
                }
            }
            _selectedCell = null;
            _pendingTargets = null;
        }

        public void OnCellClicked(ICell cell, IGridController gridController)
        {
            if (DisplayName == "Move")
            {
                if (_cellsInMovementRange == null || !_cellsInMovementRange.Contains(cell))
                {
                    gridController.GridState = new GridStateAwaitInput();
                    return;
                }

                var latestPath = _owner.FindPath(cell, gridController.CellManager);
                if (!_owner.IsCellMovableTo(cell) || !latestPath.Any())
                {
                    gridController.GridState = new GridStateAwaitInput();
                    return;
                }

                _currentPath = latestPath;
                _owner.MarkBasicAbilityUsed("Move");
                _owner.HumanExecuteAbility(new MoveCommand(_owner.CurrentCell, cell, _currentPath), gridController);
                return;
            }

            
            if (!IsValidCell(cell, gridController))
            {
                gridController.GridState = new GridStateAwaitInput();
                return;
            }

            _selectedCell = cell;
            _pendingTargets = _config.TargetingStrategy?.GetTargets(_owner, cell, gridController) ?? Enumerable.Empty<IUnit>();
            
            int targetCount = _pendingTargets.Count();
            
            if (targetCount > 0)
            {
                _owner.HumanExecuteAbility(new AbilityCommand(this, _pendingTargets), gridController);
            }
            else
            {
                gridController.GridState = new GridStateAwaitInput();
            }
        }

        public void OnCellHighlighted(ICell cell, IGridController gridController)
        {
            if (DisplayName == "Move")
            {
                if (gridController.CellManager is TilemapCellManager tcm)
                {
                    tcm.SetReachableMovementMode(true);
                }
                if (_cellsInMovementRange != null && _cellsInMovementRange.Contains(cell))
                {
                    _currentPath = _owner.FindPath(cell, gridController.CellManager);
                    gridController.CellManager.MarkAsPath(_currentPath, _owner.CurrentCell);
                }
            }
            else if (IsValidCell(cell, gridController) && _config.TargetingStrategy is AoETargeting aoe)
            {
                if (gridController.CellManager is TilemapCellManager tcm)
                {
                    tcm.SetReachableMovementMode(false);
                }
                var aoeCells = GetAoeCells(cell, aoe, gridController);
                _activeAoeCells = new HashSet<ICell>(aoeCells);
                gridController.CellManager.MarkAsAoE(aoeCells);
            }
        }

        public void OnCellDehighlighted(ICell cell, IGridController gridController)
        {
            if (DisplayName == "Move")
            {
                if (gridController.CellManager is TilemapCellManager tcm)
                {
                    tcm.SetReachableMovementMode(true);
                }
                if (_cellsInMovementRange != null && _cellsInMovementRange.Contains(cell))
                {
                    gridController.CellManager.MarkAsReachable(cell);
                    if (_currentPath != null && _currentPath.Any())
                    {
                        gridController.CellManager.UnMark(_currentPath);
                        gridController.CellManager.MarkAsReachable(_currentPath.Where(c => _cellsInMovementRange.Contains(c)));
                    }
                }
            }
            else if (_config.TargetingStrategy is AoETargeting aoe)
            {
                var aoeCells = GetAoeCells(cell, aoe, gridController);
                _activeAoeCells = null;
                gridController.CellManager.UnMark(aoeCells);
                if (_validTargetCells != null && _validTargetCells.Count > 0)
                {
                    gridController.CellManager.MarkAsReachable(_validTargetCells);
                }
            }
        }

        public void OnUnitClicked(IUnit unit, IGridController gridController)
        {
            bool canPerform = CanPerform(gridController);
            
            if (_config.TargetingStrategy != null && canPerform)
            {
                bool isValidTarget = _config.TargetingStrategy.IsValidTarget(_owner, unit, gridController);
                
                if (isValidTarget)
                {
                    _pendingTargets = new List<IUnit> { unit };
                    _owner.HumanExecuteAbility(new AbilityCommand(this, _pendingTargets), gridController);
                    return;
                }
            }

            // 点击非目标单位或无效目标时，回到默认状态
            gridController.GridState = new GridStateAwaitInput();
        }

        public void OnUnitHighlighted(IUnit unit, IGridController gridController) { }
        public void OnUnitDehighlighted(IUnit unit, IGridController gridController) { }
        public void OnUnitDestroyed(IGridController gridController) { }
        public void OnAbilityDeselected(IGridController gridController) { }
        public void OnTurnStart(IGridController gridController) { }
        public void OnTurnEnd(IGridController gridController) { }

        public bool CanPerform(IGridController gridController)
        {
            if (DisplayName == "Move")
            {
                return !_owner.HasUsedBasicAbilityThisTurn("Move")
                    && _owner.GetAvailableDestinations(gridController.CellManager.GetCells()).Count > 0;
            }

            if (_config.IsBasicAbility)
            {
                return !_owner.HasUsedBasicAbilityThisTurn(_config.DisplayName);
            }
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

        private bool IsValidCell(ICell cell, IGridController gridController)
        {
            if (_config.TargetingStrategy == null) return false;
            if (_config.TargetingStrategy is AoETargeting aoe)
            {
                return cell.GetDistance(_owner.CurrentCell) <= aoe.MaxRange;
            }
            // For single/multi target strategies, check if cell contains valid targets
            if (_validTargetCells != null)
            {
                return _validTargetCells.Contains(cell);
            }
            return true;
        }

        /// <summary>
        /// Calculates all valid target cells for the current ability based on its targeting strategy.
        /// Shows all cells within range, regardless of whether they contain valid targets.
        /// </summary>
        private HashSet<ICell> CalculateValidTargetCells()
        {
            var validCells = new HashSet<ICell>();
            if (_config.TargetingStrategy == null) return validCells;

            var allCells = _gridController.CellManager.GetCells();
            var ownerCell = _owner.CurrentCell;

            // Get max range from targeting strategy
            int maxRange = GetMaxRangeFromStrategy();


            // Add all cells within range using GetNeighbours for proper adjacency
            // For range=1, only show direct neighbours (up/down/left/right)
            if (maxRange <= 1)
            {
                // For melee range, only add direct neighbours (not the owner cell itself)
                var neighbours = ownerCell.GetNeighbours(_gridController.CellManager);
                foreach (var neighbour in neighbours)
                {
                    validCells.Add(neighbour);
                }
            }
            else
            {
                // For ranged abilities, add all cells within maxRange using BFS
                var visited = new HashSet<ICell>();
                var queue = new Queue<(ICell cell, int distance)>();
                queue.Enqueue((ownerCell, 0));
                visited.Add(ownerCell);

                while (queue.Count > 0)
                {
                    var (currentCell, distance) = queue.Dequeue();
                    if (distance <= maxRange)
                    {
                        validCells.Add(currentCell);
                    }

                    if (distance < maxRange)
                    {
                        var neighbours = currentCell.GetNeighbours(_gridController.CellManager);
                        foreach (var neighbour in neighbours)
                        {
                            if (!visited.Contains(neighbour))
                            {
                                visited.Add(neighbour);
                                queue.Enqueue((neighbour, distance + 1));
                            }
                        }
                    }
                }
            }

            return validCells;
        }

        /// <summary>
        /// Gets the max range from the targeting strategy.
        /// </summary>
        private int GetMaxRangeFromStrategy()
        {
            var strategy = _config.TargetingStrategy;
            if (strategy is SingleTargetEnemy single) return single.MaxRange;
            if (strategy is SingleTargetAlly ally) return ally.MaxRange;
            if (strategy is AoETargeting aoe) return aoe.MaxRange;
            if (strategy is MultiTargetEnemy multi) return multi.MaxRange;
            return 1; // Default range
        }

        /// <summary>
        /// Executes the move ability for AI by creating and executing a MoveCommand.
        /// </summary>
        public Task<bool> ExecuteMoveForAI(ICell destination, IEnumerable<ICell> path, IGridController gridController)
        {
            if (!CanPerform(gridController))
            {
                return Task.FromResult(false);
            }

            _owner.MarkBasicAbilityUsed("Move");
            var tcs = new TaskCompletionSource<bool>();
            _owner.AIExecuteAbility(new MoveCommand(_owner.CurrentCell, destination, path), gridController, tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Executes ability effects on the specified targets. Used by AI to execute abilities programmatically.
        /// </summary>
        public async Task ExecuteEffectsAsync(IEnumerable<IUnit> targets, IGridController gridController)
        {
            // Basic abilities don't consume Mana but can only be used once per turn
            if (_config.IsBasicAbility)
            {
                if (_owner.HasUsedBasicAbilityThisTurn(_config.DisplayName))
                {
                    return;
                }
                _owner.MarkBasicAbilityUsed(_config.DisplayName);
            }
            else
            {
                // Non-basic abilities consume Mana
                if (_owner.Mana < _config.ManaCost)
                {
                    return;
                }
                _owner.Mana -= _config.ManaCost;
            }

            try
            {
                // If targets is null (e.g. called from UI click), use the pre-calculated _pendingTargets
                _pendingTargets = targets ?? _pendingTargets;

                foreach (var effect in _config.Effects)
                {
                    await effect.Execute(_owner, _pendingTargets, gridController);
                }

                CleanUp(gridController);
            }
            catch (Exception ex)
            {
                TLog.Error($"[GenericAbilityImpl] Error executing ability {_config.DisplayName}: {ex.Message}");
            }
        }

        private async void ExecuteEffects(IGridController gridController)
        {
            await ExecuteEffectsAsync(null, gridController);
        }

        private HashSet<ICell> GetAoeCells(ICell center, AoETargeting aoe, IGridController gridController)
        {
            var cells = new HashSet<ICell> { center };
            if (aoe.Shape == AoeShape.Cross)
            {
                var neighbours = center.GetNeighbours(gridController.CellManager);
                foreach (var n in neighbours)
                {
                    cells.Add(n);
                }
            }
            else if (aoe.Shape == AoeShape.Circle)
            {
                cells.UnionWith(gridController.CellManager.GetCells().Where(c => c.GetDistance(center) <= aoe.Radius));
            }
            return cells;
        }
    }
}
