using System;
using System.Linq;
using Tactics.Tbsf.Common.Cells;
using Tactics.Tbsf.Common.Controllers.GameResolvers;
using Tactics.Tbsf.Common.Controllers.GridStates;
using Tactics.Tbsf.Common.Controllers.TurnResolvers;
using Tactics.Tbsf.Common.Players;
using Tactics.Tbsf.Common.Units;
using Tactics.Tbsf.Common.Units.Abilities;

namespace Tactics.Tbsf.Common.Controllers
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
        public TurnContext TurnContext { get; protected set; }

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
            TurnContext = TurnResolver.ResolveStart(this);
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
            if (unit.PlayerNumber.Equals(TurnContext.CurrentPlayer.PlayerNumber))
            {
                // UnitSpeed 期望每回合只有一个可行动单位。
                // 当场景绑定异常导致可行动单位列表变宽时，必须在执行层阻止越权单位的命令。
                var activeUnit = TurnContext.PlayableUnits().FirstOrDefault();
                if (activeUnit == null || !ReferenceEquals(activeUnit, unit))
                {
                    return;
                }

                _ = eventArgs.PreAction(this);
                await eventArgs.Command.Execute(unit, this);
                _ = eventArgs.PostAction(this);
            }
        }

        private async void OnUnitDestroyed(UnitDestroyedEventArgs eventArgs)
        {
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

            UnitManager.UnMark(TurnContext.PlayableUnits());
            TurnContext = TurnResolver.ResolveTurn(this);

            foreach (var unit in TurnContext.PlayableUnits())
            {
                unit.OnTurnStart(this);
                foreach (var ability in unit.GetBaseAbilities())
                {
                    ability.OnTurnStart(this);
                }
            }

            TurnStarted?.Invoke(new TurnTransitionParams(TurnContext, isNetworkInvoked));
            UnitManager.MarkAsFriendly(TurnContext.PlayableUnits());
            TurnContext.CurrentPlayer.Play(this);
        }

        public void InvokeGameEnded(GameResult gameResult)
        {
            GameEnded?.Invoke(gameResult);
            GridState = new GridStateGameEnded();
        }
    }
}