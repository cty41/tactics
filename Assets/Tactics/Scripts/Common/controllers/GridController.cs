using System;
using Tactics.Runtime.Utilities;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Controllers.TurnResolvers;
using Tactics.Common.Interactables;
using Tactics.Common.Players;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Utilities;

namespace Tactics.Common.Controllers
{
    /// <summary>
    /// Represents a controller for managing the grid, units, players, and turns in the game.
    /// It handles game initialization, state transitions, and interactions between game entities.
    /// </summary>
    public class GridController : IGridController
    {
        public ICellManager CellManager { get; set; }
        public IUnitManager UnitManager { get; set; }
        public IPlayerManager PlayerManager { get; set; }
        public ITurnResolver TurnResolver { get; set; }
        public Action<IGridController> BeforeUnitManagerInitialize { get; set; }
        public string CorpsePrefabPath { get; set; }
        public TurnContext TurnContext { get; protected set; }
        public int CurrentRound { get; protected set; } = 1;
        private int _transitionCount;

        /// <summary>
        /// 当设置为 true 时，OnAbilityUsed 跳过活跃单位检查，允许 AI 测试直接执行命令。
        /// </summary>
        public bool BypassActiveUnitCheck { get; set; } = false;

        /// <summary>
        /// 当设置为 true 时，禁止 AI 自动执行 Play()，用于测试环境。
        /// </summary>
        public bool DisableAiAutoPlay { get; set; } = false;

        public event Action GameStarted;
        public event Action GameInitialized;
        public event Action<GameResult> GameEnded;
        public event Action<TurnTransitionParams> TurnStarted;
        public event Action<TurnTransitionParams> TurnEnded;

        private GridState _gridState;
        public GridState GridState
        {
            get
            {
                return _gridState;
            }
            set
            {
                var nextState = _gridState.MakeTransition(value);
                _gridState?.OnStateExit(this);
                _gridState = nextState;
                _gridState.OnStateEnter(this);
            }
        }

        public virtual void InitializeGame(bool isNetworkInvoked = false)
        {
            _gridState = new GridStateBlockInput();

            CellManager.CellAdded += RegisterCell;
            CellManager.Initialize(this);
            CellManager.UnMark(CellManager.GetCells());

            BeforeUnitManagerInitialize?.Invoke(this);

            UnitManager.UnitAdded += RegisterUnit;
            UnitManager.Initialize(this);

            PlayerManager.Initialize(this);
            foreach (var player in PlayerManager.GetPlayers())
            {
                player.Initialize(this);
            }

            GameInitialized?.Invoke();
        }

        public virtual void StartGame(bool isNetworkInvoked = false)
        {
            CurrentRound = 1;
            _transitionCount = 0;
            TurnContext = TurnResolver.ResolveStart(this);
            if (TurnContext.CurrentPlayer == null)
            {
                TLog.Error($"[GridController] TurnContext.CurrentPlayer is null. TurnResolver type: {TurnResolver?.GetType()?.Name ?? "null"}. " +
                    "Ensure BattleController._players is configured and players' PlayerNumber matches units' PlayerNumber.");
                return;
            }
            foreach (var unit in TurnContext.PlayableUnits())
            {
                unit.OnTurnStart(this);
                foreach (var ability in unit.GetBaseAbilities())
                {
                    ability.OnTurnStart(this);
                }
            }

            GameStarted?.Invoke();
            TurnStarted?.Invoke(new TurnTransitionParams(TurnContext, isNetworkInvoked));
            UnitManager.MarkAsFriendly(TurnContext.PlayableUnits());
            if (TurnContext.CurrentPlayer == null)
            {
                TLog.Error($"[GridController] MakeTurnTransition: CurrentPlayer is null. UnitPlayerNumber={TurnContext.PlayableUnits().FirstOrDefault()?.PlayerNumber ?? -1}. Skipping turn.");
                return;
            }
            TurnContext.CurrentPlayer.Play(this);
        }

        public virtual void InitializeAndStart(bool isNetworkInvoked = false)
        {
            InitializeGame(isNetworkInvoked);
            StartGame(isNetworkInvoked);
        }

        protected virtual void OnCellClicked(ICell cell)
        {
            GridState.OnCellClicked(cell, this);
        }

        protected virtual void OnCellDehighlighted(ICell cell)
        {
            GridState.OnCellDehighlighted(cell, this);
        }

        protected virtual void OnCellHighlighted(ICell cell)
        {
            GridState.OnCellHighlighted(cell, this);
        }

        protected virtual void OnUnitDehighlighted(IUnit unit)
        {
            GridState.OnUnitDehighlighted(unit, this);
        }

        protected virtual void OnUnitHighlighted(IUnit unit)
        {
            GridState.OnUnitHighlighted(unit, this);
        }

        protected virtual void OnUnitClicked(IUnit unit)
        {
            GridState.OnUnitClicked(unit, this);
        }

        private void RegisterCell(ICell cell)
        {
            cell.CellHighlighted += OnCellHighlighted;
            cell.CellDehighlighted += OnCellDehighlighted;
            cell.CellClicked += OnCellClicked;
        }

        private void RegisterUnit(IUnit unit)
        {
            unit.Initialize(this);
            unit.UnitClicked += OnUnitClicked;
            unit.UnitHighlighted += OnUnitHighlighted;
            unit.UnitDehighlighted += OnUnitDehighlighted;
            unit.UnitDestroyed += OnUnitDestroyed;
            unit.AbilityUsed += (eventArgs) => OnAbilityUsed(unit, eventArgs);
        }

        /// <summary>
        /// Handles the event when an ability is used by a unit.
        /// </summary>
        /// <param name="unit">The unit that used the ability.</param>
        /// <param name="eventArgs">The event arguments containing the details of the ability used.</param>
        /// <returns>A task representing the asynchronous execution of the ability.</returns>
        protected virtual async void OnAbilityUsed(IUnit unit, AbilityUsedEventArgs eventArgs)
        {
            if (unit.PlayerNumber.Equals(TurnContext.CurrentPlayer.PlayerNumber) || BypassActiveUnitCheck)
            {
                // UnitSpeed 期望每回合只有一个可行动单位。
                // 当场景绑定异常导致可行动单位列表变宽时，必须在执行层阻止越权单位的命令。
                // 但 AI 测试需要绕过此检查。
                if (!BypassActiveUnitCheck)
                {
                    var activeUnit = TurnContext.PlayableUnits().FirstOrDefault();
                    if (activeUnit == null || !ReferenceEquals(activeUnit, unit))
                    {
                        return;
                    }
                }

                _ = eventArgs.PreAction(this);
                await eventArgs.Command.Execute(unit, this);
                _ = eventArgs.PostAction(this);
            }
        }

        /// <summary>
        /// 真正等待能力执行完成的异步方法。
        /// 不通过事件系统，直接执行命令。
        /// </summary>
        public async Task HandleAbilityUsedAsync(IUnit unit, ICommand command)
        {
            if (unit.PlayerNumber.Equals(TurnContext.CurrentPlayer.PlayerNumber) || BypassActiveUnitCheck)
            {
                if (!BypassActiveUnitCheck)
                {
                    var activeUnit = TurnContext.PlayableUnits().FirstOrDefault();
                    if (activeUnit == null || !ReferenceEquals(activeUnit, unit))
                    {
                        return;
                    }
                }

                await command.Execute(unit, this);
            }
        }

        private async void OnUnitDestroyed(UnitDestroyedEventArgs eventArgs)
        {
            // Linked death: if this unit has a summoned unit, kill it
            if (eventArgs.AffectedUnit.SummonedUnit != null && !eventArgs.AffectedUnit.SummonedUnit.IsDowned)
            {
                var summoned = eventArgs.AffectedUnit.SummonedUnit;
                eventArgs.AffectedUnit.SummonedUnit = null;
                summoned.OwnerUnitId = -1;
                summoned.ModifyHealth(-summoned.Health - 1, null);
            }

            // Linked death: if this unit is summoned, clear owner reference
            if (eventArgs.AffectedUnit.OwnerUnitId >= 0)
            {
                var owner = UnitManager.GetUnits().FirstOrDefault(u => u.UnitID == eventArgs.AffectedUnit.OwnerUnitId);
                if (owner != null)
                {
                    owner.SummonedUnit = null;
                }
                eventArgs.AffectedUnit.OwnerUnitId = -1;
            }

            // Corpse generation: first time death → create Corpse interactable on grid
            if (!eventArgs.AffectedUnit.IsCorpse)
            {
                eventArgs.AffectedUnit.IsCorpse = true;
                var cell = eventArgs.AffectedUnit.CurrentCell;
                if (cell != null)
                {
                    Corpse corpse = null;

                    if (!string.IsNullOrEmpty(CorpsePrefabPath))
                    {
                        var mgr = AssetPipeline.GameAssetManager.Instance;
                        if (mgr != null)
                        {
                            var prefab = mgr.Load<UnityEngine.GameObject>(CorpsePrefabPath);
                            if (prefab != null)
                            {
                                var go = UnityEngine.Object.Instantiate(prefab, cell.WorldPosition.ToVector3(), UnityEngine.Quaternion.identity);
                                corpse = go.GetComponent<Corpse>();
                            }
                        }
                    }

                    if (corpse == null)
                    {
                        var go = new UnityEngine.GameObject("Corpse");
                        corpse = go.AddComponent<Corpse>();
                        go.transform.position = cell.WorldPosition.ToVector3();
                    }

                    cell.AddInteractable(corpse);
                    TLog.Info($"[GridController] Unit {eventArgs.AffectedUnit.UnitID} died, Corpse created at {cell.GridCoordinates}");
                }
                return;
            }

            foreach (var ability in eventArgs.AffectedUnit.GetBaseAbilities())
            {
                ability.OnUnitDestroyed(this);
            }

            UnitManager.RemoveUnit(eventArgs.AffectedUnit);

            eventArgs.AffectedUnit.UnitClicked -= OnUnitClicked;
            eventArgs.AffectedUnit.UnitSelected -= OnUnitHighlighted;
            eventArgs.AffectedUnit.UnitDeselected -= OnUnitDehighlighted;
            eventArgs.AffectedUnit.UnitDestroyed -= OnUnitDestroyed;

            eventArgs.AffectedUnit.Cleanup(this);
            await UnitManager.MarkAsDestroyed(eventArgs.AffectedUnit);
            eventArgs.AffectedUnit.OnDestroyed(this);
        }

        /// <summary>
        /// 真正等待单位销毁完成的异步方法。
        /// </summary>
        public async Task HandleUnitDestroyedAsync(IUnit unit)
        {
            // Linked death: if this unit has a summoned unit, kill it
            if (unit.SummonedUnit != null && !unit.SummonedUnit.IsDowned)
            {
                var summoned = unit.SummonedUnit;
                unit.SummonedUnit = null;
                summoned.OwnerUnitId = -1;
                summoned.ModifyHealth(-summoned.Health - 1, null);
            }

            // Linked death: if this unit is summoned, clear owner reference
            if (unit.OwnerUnitId >= 0)
            {
                var owner = UnitManager.GetUnits().FirstOrDefault(u => u.UnitID == unit.OwnerUnitId);
                if (owner != null)
                {
                    owner.SummonedUnit = null;
                }
                unit.OwnerUnitId = -1;
            }

            foreach (var ability in unit.GetBaseAbilities())
            {
                ability.OnUnitDestroyed(this);
            }

            UnitManager.RemoveUnit(unit);

            unit.UnitClicked -= OnUnitClicked;
            unit.UnitSelected -= OnUnitHighlighted;
            unit.UnitDeselected -= OnUnitDehighlighted;
            unit.UnitDestroyed -= OnUnitDestroyed;

            unit.Cleanup(this);
            await UnitManager.MarkAsDestroyed(unit);
            unit.OnDestroyed(this);
        }

        public void EndTurn(bool isNetworkInvoked = false)
        {
            _gridState.EndTurn(this, isNetworkInvoked);
        }

        public void MakeTurnTransition(bool isNetworkInvoked = false)
        {
            GridState = new GridStateBlockInput();

            foreach (var unit in TurnContext.PlayableUnits())
            {
                unit.OnTurnEnd(this);
                foreach (var ability in unit.GetBaseAbilities())
                {
                    ability.OnTurnEnd(this);
                }
            }
            TurnEnded?.Invoke(new TurnTransitionParams(TurnContext, isNetworkInvoked));

            var previousPlayer = TurnContext.CurrentPlayer;
            UnitManager.UnMark(TurnContext.PlayableUnits());
            TurnContext = TurnResolver.ResolveTurn(this);

            var newUnit = TurnContext.PlayableUnits().FirstOrDefault();
            var newBuffCount = (newUnit as Tactics.Common.Units.Unit)?.BuffComponent?.GetActiveBuffs()?.Count ?? 0;
            // TEMP: diagnostic log for freeze bug investigation — remove after fix confirmed
            TLog.Info($"[TurnTransition] P{previousPlayer?.PlayerNumber} → P{TurnContext.CurrentPlayer?.PlayerNumber}, CanAct={newUnit?.CanAct}, Buffs={newBuffCount}");

            _transitionCount++;
            int totalUnits = UnitManager.GetUnits().Count();
            if (_transitionCount >= totalUnits)
            {
                CurrentRound++;
                _transitionCount = 0;
                TLog.Info($"[GridController] Round complete. CurrentRound={CurrentRound}, TotalUnits={totalUnits}");
            }

            foreach (var unit in TurnContext.PlayableUnits())
            {
                unit.PrepareForTurn();
                unit.OnTurnStart(this);
                foreach (var ability in unit.GetBaseAbilities())
                {
                    ability.OnTurnStart(this);
                }
            }

            TurnStarted?.Invoke(new TurnTransitionParams(TurnContext, isNetworkInvoked));
            UnitManager.MarkAsFriendly(TurnContext.PlayableUnits());
            if (!DisableAiAutoPlay)
            {
                TurnContext.CurrentPlayer.Play(this);
            }
        }

        public void InvokeGameEnded(GameResult gameResult)
        {
            GameEnded?.Invoke(gameResult);
            GridState = new GridStateGameEnded();
        }
    }
}
