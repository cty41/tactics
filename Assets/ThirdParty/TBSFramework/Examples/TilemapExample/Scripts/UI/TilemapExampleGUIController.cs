using System.Linq;
using TMPro;
using TurnBasedStrategyFramework.Common.Controllers.GameResolvers;
using TurnBasedStrategyFramework.Unity.Controllers;
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