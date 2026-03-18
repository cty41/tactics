using System.Linq;
using TMPro;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Controllers.GameResolvers;
using TurnBasedStrategyFramework.Common.Players;
using TurnBasedStrategyFramework.Unity.Controllers;
using TurnBasedStrategyFramework.Unity.Players;
using UnityEngine;
using UnityEngine.UI;

namespace TurnBasedStrategyFramework.Unity.Examples.TilemapExample.UI
{
    public class TilemapExampleGUIController : MonoBehaviour
    {
        [SerializeField] private UnityGridController _gridController;
        [SerializeField] private Button _endTurnButton;
        [SerializeField] private TMP_Text _gameOverText;

        private void Awake()
        {
            _gridController.GameEnded += OnGameEnded;
            _gridController.TurnStarted += OnTurnStarted;
        }

        private void OnTurnStarted(TurnTransitionParams turnTransitionParams)
        {
            // Only allow manual end turn when it's a human player's turn
            _endTurnButton.interactable = turnTransitionParams.TurnContext.CurrentPlayer.PlayerType == PlayerType.HumanPlayer;
        }

        private void OnGameEnded(GameResult gameResult)
        {
            _endTurnButton.interactable = false;
            _gameOverText.text = $"Player {gameResult.Winners.First().PlayerNumber} Wins!";
            _gameOverText.gameObject.SetActive(true);
        }

        public void EndTurn()
        {
            _gridController.EndTurn();
        }
    }
}