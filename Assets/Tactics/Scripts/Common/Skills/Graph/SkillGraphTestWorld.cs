using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Controllers.TurnResolvers;
using Tactics.Common.Players;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using UnityEngine;

namespace Tactics.Common.Skills.Graph.Testing
{
    /// <summary>
    /// 用于技能图运行时测试的轻量世界。
    /// 提供真实 Unit / Cell 对象，但不依赖完整战斗流。
    /// </summary>
    public sealed class SkillGraphTestWorld : IDisposable
    {
        private readonly List<GameObject> _spawnedObjects = new();

        public SkillGraphTestGridController GridController { get; }
        public SkillGraphTestCellManager CellManager { get; }
        public SkillGraphTestUnitManager UnitManager { get; }
        public SkillGraphTestPlayerManager PlayerManager { get; }
        public HumanPlayer PlayerOne { get; }
        public HumanPlayer PlayerTwo { get; }

        public SkillGraphTestWorld()
        {
            GridController = new SkillGraphTestGridController();
            CellManager = new SkillGraphTestCellManager();
            UnitManager = new SkillGraphTestUnitManager();
            PlayerManager = new SkillGraphTestPlayerManager();

            GridController.CellManager = CellManager;
            GridController.UnitManager = UnitManager;
            GridController.PlayerManager = PlayerManager;
            GridController.GridState = new GridStateBlockInput();

            PlayerOne = new HumanPlayer { PlayerNumber = 0 };
            PlayerTwo = new HumanPlayer { PlayerNumber = 1 };
            PlayerManager.AddPlayer(PlayerOne);
            PlayerManager.AddPlayer(PlayerTwo);
        }

        public ICell CreateSquareCell(string name, int x, int y, float movementCost = 1f)
        {
            var go = new GameObject(name);
            _spawnedObjects.Add(go);

            var cell = go.AddComponent<Square>();
            cell.GridCoordinates = new Vector2IntImpl(x, y);
            cell.WorldPosition = new Vector3Impl(x, y, 0f);
            cell.MovementCost = movementCost;

            CellManager.AddCell(cell);
            return cell;
        }

        public Unit CreateUnit(string name, int playerNumber, ICell cell = null)
        {
            var go = new GameObject(name);
            _spawnedObjects.Add(go);

            var unit = go.AddComponent<Unit>();
            unit.PlayerNumber = playerNumber;
            unit.Initialize(GridController);

            UnitManager.AddUnit(unit);

            if (cell != null)
            {
                PlaceUnit(unit, cell);
            }

            return unit;
        }

        public void PlaceUnit(IUnit unit, ICell cell)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            if (cell == null)
                throw new ArgumentNullException(nameof(cell));

            if (unit.CurrentCell != null)
            {
                unit.CurrentCell.CurrentUnits.Remove(unit);
                unit.CurrentCell.IsTaken = unit.CurrentCell.CurrentUnits.Count > 0;
            }

            unit.CurrentCell = cell;
            if (!cell.CurrentUnits.Contains(unit))
                cell.CurrentUnits.Add(unit);
            cell.IsTaken = cell.CurrentUnits.Count > 0;
            unit.WorldPosition = cell.WorldPosition;
        }

        public void PlaceUnit(IUnit unit, int x, int y)
        {
            var cell = CellManager.GetCellAt(new Vector2IntImpl(x, y));
            if (cell == null)
                throw new InvalidOperationException($"No cell at ({x}, {y}).");
            PlaceUnit(unit, cell);
        }

        public void SetTurnContext(IPlayer currentPlayer, IEnumerable<IUnit> playableUnits)
        {
            GridController.SetTurnContext(currentPlayer, playableUnits);
        }

        public void Dispose()
        {
            // 先清空管理器（移除引用，避免悬空引用窗口）
            CellManager.Clear();
            UnitManager.Clear();
            PlayerManager.Clear();
            GridController.ClearTurnContext();

            // 再销毁 GameObjects
            for (int i = _spawnedObjects.Count - 1; i >= 0; i--)
            {
                var go = _spawnedObjects[i];
                if (go == null)
                    continue;

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(go);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            _spawnedObjects.Clear();
        }
    }

    /// <summary>
    /// 技能图测试用 IGridController 实现。
    /// </summary>
    public sealed class SkillGraphTestGridController : IGridController
    {
        public ICellManager CellManager { get; set; }
        public IUnitManager UnitManager { get; set; }
        public IPlayerManager PlayerManager { get; set; }
        public ITurnResolver TurnResolver { get; set; }
        public TurnContext TurnContext { get; private set; }
        public int CurrentRound { get; private set; } = 1;
        public GridState GridState { get; set; } = new GridStateBlockInput();

        public event Action GameStarted;
        public event Action GameInitialized;
        public event Action<GameResult> GameEnded;
        public event Action<TurnTransitionParams> TurnStarted;
        public event Action<TurnTransitionParams> TurnEnded;

        public void SetTurnContext(IPlayer currentPlayer, IEnumerable<IUnit> playableUnits)
        {
            TurnContext = new TurnContext(currentPlayer, playableUnits);
        }

        public void ClearTurnContext()
        {
            TurnContext = default;
        }

        public void InitializeGame(bool isNetworkInvoked = false)
        {
        }

        public void StartGame(bool isNetworkInvoked = false)
        {
        }

        public void InitializeAndStart(bool isNetworkInvoked = false)
        {
        }

        public void EndTurn(bool isNetworkInvoked = false)
        {
        }

        public void MakeTurnTransition(bool isNetworkInvoked = false)
        {
        }

        public void InvokeGameEnded(GameResult gameResult)
        {
            GameEnded?.Invoke(gameResult);
        }
    }

    /// <summary>
    /// 技能图测试用 CellManager 实现。
    /// </summary>
    public sealed class SkillGraphTestCellManager : ICellManager
    {
        private readonly List<ICell> _cells = new();
        private readonly Dictionary<Vector2IntImpl, ICell> _cellsByCoord = new();

        public event Action<ICell> CellAdded;
        public event Action<ICell> CellRemoved;

        public void AddCell(ICell cell)
        {
            if (cell == null)
                return;

            _cells.Add(cell);
            _cellsByCoord[cell.GridCoordinates] = cell;
            CellAdded?.Invoke(cell);
        }

        public void Clear()
        {
            _cells.Clear();
            _cellsByCoord.Clear();
        }

        public void Initialize(IGridController gridController)
        {
        }

        public IEnumerable<ICell> GetCells()
        {
            return _cells;
        }

        public ICell GetCellAt(Vector2IntImpl gridCoordinates)
        {
            _cellsByCoord.TryGetValue(gridCoordinates, out var cell);
            return cell;
        }

        public Task UnMark(IEnumerable<ICell> cells) => Task.CompletedTask;
        public Task UnMark(ICell cell) => Task.CompletedTask;
        public Task MarkAsHighlighted(ICell cell) => Task.CompletedTask;
        public Task UnMarkAsHighlighted(ICell cell) => Task.CompletedTask;
        public Task MarkAsReachable(IEnumerable<ICell> cells) => Task.CompletedTask;
        public Task MarkAsReachable(ICell cell) => Task.CompletedTask;
        public Task MarkAsPath(IEnumerable<ICell> cells, ICell originCell) => Task.CompletedTask;
        public Task MarkAsAoE(IEnumerable<ICell> cells) => Task.CompletedTask;
        public void SetColor(ICell cell, float r, float g, float b, float a) { }
        public bool IsCellWalkable(ICell cell) => cell != null && !cell.IsTaken;
    }

    /// <summary>
    /// 技能图测试用 UnitManager 实现。
    /// </summary>
    public sealed class SkillGraphTestUnitManager : IUnitManager
    {
        private readonly List<IUnit> _units = new();

        public event Action<IUnit> UnitAdded;
        public event Action<IUnit> UnitRemoved;

        public void AddUnit(IUnit unit)
        {
            if (unit == null)
                return;

            if (!_units.Contains(unit))
                _units.Add(unit);

            UnitAdded?.Invoke(unit);
        }

        public void RemoveUnit(IUnit unit)
        {
            if (unit == null)
                return;

            _units.Remove(unit);
            UnitRemoved?.Invoke(unit);
        }

        public void Clear()
        {
            _units.Clear();
        }

        public void Initialize(IGridController gridController)
        {
        }

        public IEnumerable<IUnit> GetUnits() => _units;

        public IEnumerable<IUnit> GetFriendlyUnits(IPlayer player)
        {
            if (player == null)
                return Array.Empty<IUnit>();

            return _units.Where(unit => unit != null && unit.PlayerNumber == player.PlayerNumber);
        }

        public IEnumerable<IUnit> GetFriendlyUnits(int playerNumber)
        {
            return _units.Where(unit => unit != null && unit.PlayerNumber == playerNumber);
        }

        public IEnumerable<IUnit> GetEnemyUnits(IPlayer player)
        {
            if (player == null)
                return Array.Empty<IUnit>();

            return _units.Where(unit => unit != null && unit.PlayerNumber != player.PlayerNumber);
        }

        public IEnumerable<IUnit> GetEnemyUnits(int playerNumber)
        {
            return _units.Where(unit => unit != null && unit.PlayerNumber != playerNumber);
        }

        public Task UnMark(IEnumerable<IUnit> units) => Task.CompletedTask;
        public Task MarkAsSelected(IUnit unit) => Task.CompletedTask;
        public Task MarkAsFriendly(IEnumerable<IUnit> units) => Task.CompletedTask;
        public Task MarkAsFinished(IEnumerable<IUnit> units) => Task.CompletedTask;
        public Task MarkAsTargetable(IEnumerable<IUnit> units) => Task.CompletedTask;
        public Task MarkAsAttacking(IUnit unit, IUnit target) => Task.CompletedTask;
        public Task MarkAsDefending(IUnit unit, IUnit aggressor) => Task.CompletedTask;
        public Task MarkAsMoving(IUnit unit, ICell source, ICell destination, IEnumerable<ICell> path) => Task.CompletedTask;
        public Task UnMarkAsMoving(IUnit unit, ICell source, ICell destination, IEnumerable<ICell> path) => Task.CompletedTask;
        public Task MarkAsDestroyed(IUnit unit) => Task.CompletedTask;
    }

    /// <summary>
    /// 技能图测试用 PlayerManager 实现。
    /// </summary>
    public sealed class SkillGraphTestPlayerManager : IPlayerManager
    {
        private readonly List<IPlayer> _players = new();

        public void AddPlayer(IPlayer player)
        {
            if (player == null)
                return;

            if (!_players.Contains(player))
                _players.Add(player);
        }

        public void Clear()
        {
            _players.Clear();
        }

        public void Initialize(GridController gridController)
        {
        }

        public IEnumerable<IPlayer> GetPlayers() => _players;

        public IPlayer GetPlayerByNumber(int playerNumber)
        {
            return _players.FirstOrDefault(player => player != null && player.PlayerNumber == playerNumber);
        }
    }
}
