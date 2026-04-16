using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Tactics.Common.Controllers;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Players;
using Tactics.Common.Units;
using Tactics.AssetPipeline;
using Tactics.Roguelike;
using Tactics.UI;

namespace Tactics.Common.Battle
{
    public sealed class BattleController : MonoBehaviourSingleton<BattleController>
    {
        public event Action BattleStarted;
        public event Action<GameResult> BattleEnded;
        public bool IsBattleActive { get; private set; }
        public UnityGridController GridController { get; private set; }

        private UnityUnitManager _unitManager;
        private UnityPlayerManager _playerManager;

        protected override void Awake()
        {
            base.Awake();
            GridController = GetComponent<UnityGridController>();
            if (GridController != null)
                GridController.GameEnded += OnGameEnded;

            RoguelikeBattleReturnHandler.Instance.RegisterController(this);
        }

        protected override void OnDestroy()
        {
            if (GridController != null)
                GridController.GameEnded -= OnGameEnded;

            if (_unitManager != null)
                _unitManager.UnitRemoved -= OnUnitRemoved;

            RoguelikeBattleReturnHandler.Instance.UnregisterController(this);
            base.OnDestroy();
        }

        private async void Start()
        {
            await StartBattleAsync();
        }

        public async Task StartBattleAsync()
        {
            if (IsBattleActive) return;
            IsBattleActive = true;

            if (GridController != null)
            {
                _unitManager = GridController.UnitManager as UnityUnitManager;
                _playerManager = GridController.PlayerManager as UnityPlayerManager;

                if (_unitManager != null)
                    _unitManager.UnitRemoved += OnUnitRemoved;
            }

            _ = ShowBattleUIAsync();

            BattleStarted?.Invoke();
        }

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

        private void OnUnitRemoved(IUnit unit)
        {
            if (_unitManager == null || _playerManager == null || GridController == null)
                return;

            var playersWithUnitsAlive = _unitManager.GetUnits()
                .Select(u => u.PlayerNumber)
                .Distinct();

            if (playersWithUnitsAlive.Count() == 1)
            {
                var winner = _playerManager.GetPlayers()
                    .First(p => p.PlayerNumber == playersWithUnitsAlive.First());
                var losers = _playerManager.GetPlayers()
                    .Where(p => p != winner);

                GridController.InvokeGameEnded(new GameResult(winner, losers));
            }
        }

        private void OnGameEnded(GameResult result)
        {
            EndBattle(result);
        }
    }
}
