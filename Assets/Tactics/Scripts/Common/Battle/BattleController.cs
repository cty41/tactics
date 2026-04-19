using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Controllers.GridStates;
using Tactics.Common.Controllers.TurnResolvers;
using Tactics.Common.Players;
using Tactics.Common.Units;
using Tactics.AssetPipeline;
using Tactics.Roguelike;
using Tactics.UI;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// 战斗控制器，统一管理网格、单位、玩家、回合以及战斗生命周期。
    /// 合并了原 UnityGridController 的职责，直接实现 IGridController 接口。
    /// </summary>
    public sealed class BattleController : MonoBehaviourSingleton<BattleController>, IGridController
    {
        #region Battle Events

        public event Action BattleStarted;
        public event Action<GameResult> BattleEnded;
        public bool IsBattleActive { get; private set; }

        #endregion

        #region IGridController Implementation (from UnityGridController)

        /// <summary>
        /// 当游戏开始时触发。
        /// </summary>
        public event Action GameStarted;

        /// <summary>
        /// 当游戏初始化完成时触发。
        /// </summary>
        public event Action GameInitialized;

        /// <summary>
        /// 当游戏结束时触发，提供游戏结果。
        /// </summary>
        public event Action<GameResult> GameEnded;

        /// <summary>
        /// 当新回合开始时触发，提供回合上下文。
        /// </summary>
        public event Action<TurnTransitionParams> TurnStarted;

        /// <summary>
        /// 当当前回合结束时触发，提供回合上下文。
        /// </summary>
        public event Action<TurnTransitionParams> TurnEnded;

        public ICellManager CellManager
        {
            get => _controller.CellManager;
            set { _controller.CellManager = value; _cellManager = value as UnityCellManager; }
        }

        public IUnitManager UnitManager
        {
            get => _controller.UnitManager;
            set { _controller.UnitManager = value; _unitManager = value as UnityUnitManager; }
        }

        public IPlayerManager PlayerManager
        {
            get => _controller.PlayerManager;
            set { _controller.PlayerManager = value; _playerManager = value as UnityPlayerManager; }
        }

        public ITurnResolver TurnResolver
        {
            get => _controller.TurnResolver;
            set { _controller.TurnResolver = value; _turnResolver = value; }
        }

        public TurnContext TurnContext => _controller.TurnContext;

        public GridState GridState
        {
            get => _controller.GridState;
            set => _controller.GridState = value;
        }

        #endregion

        #region Serialized Fields

        /// <summary>
        /// 是否在场景加载时立即开始游戏。
        /// </summary>
        [SerializeField] private bool _startImmediatelly = true;
        [SerializeField] private UnityCellManager _cellManager;
        [SerializeField] private UnityUnitManager _unitManager;
        [SerializeField] private UnityPlayerManager _playerManager;
        [SerializeReference] private ITurnResolver _turnResolver;

        #endregion

        #region Private Fields

        private readonly GridController _controller = new GridController();
        private UnityPlayerManager _battlePlayerManager;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();

            // 初始化 GridController 的依赖
            _controller.CellManager = _cellManager;
            _controller.UnitManager = _unitManager;
            _controller.PlayerManager = _playerManager;
            _controller.TurnResolver = _turnResolver;

            // 订阅 GridController 的事件
            _controller.GameInitialized += OnGameInitialized;
            _controller.GameEnded += OnGameEnded;
            _controller.GameStarted += OnGameStarted;
            _controller.TurnStarted += OnTurnStarted;
            _controller.TurnEnded += OnTurnEnded;

            // 订阅 UnitRemoved 事件用于判断胜负
            if (_unitManager != null)
                _unitManager.UnitRemoved += OnUnitRemoved;

            RoguelikeBattleReturnHandler.Instance.RegisterController(this);
        }

        protected override void OnDestroy()
        {
            // 取消订阅事件
            _controller.GameInitialized -= OnGameInitialized;
            _controller.GameEnded -= OnGameEnded;
            _controller.GameStarted -= OnGameStarted;
            _controller.TurnStarted -= OnTurnStarted;
            _controller.TurnEnded -= OnTurnEnded;

            if (_unitManager != null)
                _unitManager.UnitRemoved -= OnUnitRemoved;

            RoguelikeBattleReturnHandler.Instance.UnregisterController(this);
            base.OnDestroy();
        }

        private async void Start()
        {
            if (_startImmediatelly)
            {
                // Initialize and start the game logic first (sets up CellManager, UnitManager, etc.)
                InitializeGame();
                StartGame();
                // Then handle battle-specific setup
                await StartBattleAsync();
            }
        }

        #endregion

        #region Battle Methods

        /// <summary>
        /// 开始战斗。
        /// </summary>
        public async Task StartBattleAsync()
        {
            if (IsBattleActive) return;
            IsBattleActive = true;

            _battlePlayerManager = _playerManager;

            _ = ShowBattleUIAsync();

            BattleStarted?.Invoke();
        }

        /// <summary>
        /// 结束战斗。
        /// </summary>
        public void EndBattle(GameResult result)
        {
            if (!IsBattleActive) return;
            IsBattleActive = false;
            BattleEnded?.Invoke(result);
        }

        private async Task ShowBattleUIAsync()
        {
            try
            {
                await UIManager.Instance.ShowAsync(UIManager.UIId.Battle);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BattleController] Failed to show Battle UI: {ex.Message}");
            }
        }

        #endregion

        #region IGridController Methods (from UnityGridController)

        public void InitializeGame(bool isNetworkInvoked = false)
        {
            _controller.InitializeGame(isNetworkInvoked);
        }

        public void StartGame(bool isNetworkInvoked = false)
        {
            _controller.StartGame(isNetworkInvoked);
        }

        public void InitializeAndStart(bool isNetworkInvoked = false)
        {
            _controller.InitializeAndStart(isNetworkInvoked);
        }

        public void EndTurn(bool isNetworkInvoked = false)
        {
            _controller.EndTurn(isNetworkInvoked);
        }

        public void MakeTurnTransition(bool isNetworkInvoked = false)
        {
            _controller.MakeTurnTransition(isNetworkInvoked);
        }

        public void InvokeGameEnded(GameResult gameResult)
        {
            _controller.InvokeGameEnded(gameResult);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 处理游戏初始化事件。
        /// </summary>
        private void OnGameInitialized()
        {
            GameInitialized?.Invoke();
        }

        /// <summary>
        /// 处理游戏开始事件。
        /// </summary>
        private void OnGameStarted()
        {
            GameStarted?.Invoke();
        }

        /// <summary>
        /// 处理游戏结束事件，同时触发战斗结束。
        /// </summary>
        private void OnGameEnded(GameResult result)
        {
            GameEnded?.Invoke(result);
            EndBattle(result);
        }

        /// <summary>
        /// 处理回合开始事件。
        /// </summary>
        private void OnTurnStarted(TurnTransitionParams turnTransitionParams)
        {
            TurnStarted?.Invoke(turnTransitionParams);
        }

        /// <summary>
        /// 处理回合结束事件。
        /// </summary>
        private void OnTurnEnded(TurnTransitionParams turnTransitionParams)
        {
            TurnEnded?.Invoke(turnTransitionParams);
        }

        /// <summary>
        /// 当单位被移除时，检查是否只剩一方的单位，若是则结束游戏。
        /// </summary>
        private void OnUnitRemoved(IUnit unit)
        {
            if (_unitManager == null || _battlePlayerManager == null)
                return;

            var playersWithUnitsAlive = _unitManager.GetUnits()
                .Select(u => u.PlayerNumber)
                .Distinct();

            if (playersWithUnitsAlive.Count() == 1)
            {
                var winner = _battlePlayerManager.GetPlayers()
                    .First(p => p.PlayerNumber == playersWithUnitsAlive.First());
                var losers = _battlePlayerManager.GetPlayers()
                    .Where(p => p != winner);

                InvokeGameEnded(new GameResult(winner, losers));
            }
        }

        #endregion
    }
}