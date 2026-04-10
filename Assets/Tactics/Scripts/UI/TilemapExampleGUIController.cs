using System.Linq;
using TMPro;
using Tactics.Tbsf.Common.Controllers;
using Tactics.Tbsf.Common.Controllers.GameResolvers;
using Tactics.Tbsf.Common.Players;
using UnityEngine;
using UnityEngine.UI;

namespace Tactics.UI
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