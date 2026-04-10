using System.Linq;
using TMPro;
using TurnBasedStrategyFramework.Common.Cells;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Controllers.GameResolvers;
using TurnBasedStrategyFramework.Common.Players;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Cells;
using TurnBasedStrategyFramework.Unity.Controllers;
using TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Cells;
using TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;
using UnityEngine.UI;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.GUI
{
    /// <summary>
    /// GUI Controller for the Example 4 demo, manages the game's user interface.
    /// </summary>
    public class Example4GUIController : MonoBehaviour
    {
        [SerializeField] private Button _endTurnButton;
        [SerializeField] private Button _restartLevelButton;

        [SerializeField] private GameObject _terrainPanel;
        [SerializeField] private TMP_Text _terrainDataText;

        [SerializeField] private GameObject _unitPanel;
        [SerializeField] private TMP_Text _unitDataText;
        [SerializeField] private ScriptableObject _unitStructureType;

        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private TMP_Text _gameOverText;

        [SerializeField] private UnityGridController _gridController;

        private void Awake()
        {
            _restartLevelButton.onClick.AddListener(() => OnRestartLevelButtonPressed());
            _gridController.GameInitialized += OnGameInitialized;
        }

        private void OnGameInitialized()
        {
            _endTurnButton.onClick.AddListener(() => OnEndTurnButtonPressed());

            _gridController.TurnStarted += OnTurnStarted;
            _gridController.GameEnded += OnGameEnded;

            foreach (var cell in _gridController.CellManager.GetCells())
            {
                cell.CellHighlighted += OnCellHighlighted;
                cell.CellDehighlighted += OnCellDehighlighted;
            }

            _gridController.UnitManager.UnitAdded += OnUnitAdded;
        }

        private void OnUnitAdded(IUnit unit)
        {
            unit.UnitHighlighted += OnUnitHighlighted;
            unit.UnitDehighlighted += OnUnitDehighlighted;
        }

        private void OnUnitDehighlighted(IUnit unit)
        {
            _unitPanel.SetActive(false);
        }

        private void OnUnitHighlighted(IUnit unit)
        {
            if ((unit as ITypedUnit)?.UnitType == _unitStructureType)
                return;

            var unitDetails = (unit as Unit).GetComponent<IUnitDetails>();
            var defenceAffectingCell = unit.CurrentCell as IDefenceAffectingCell;
            var modifier = defenceAffectingCell.DefenceModifier;
            var modifierText = modifier > 0 ? $"+{modifier}" : "";

            _unitDataText.text =
                $"<size=50>{unitDetails.GetName()}</size>\n" +
                $"HP: {unit.Health}/{unit.MaxHealth}\n" +
                $"Def: {unit.DefenceFactor}{modifierText}\n" +
                $"Attck: {unit.AttackFactor}";

            _unitPanel.SetActive(true);
        }


        private void OnCellDehighlighted(ICell cell)
        {
            _terrainPanel.SetActive(false);
        }

        private void OnCellHighlighted(ICell cell)
        {
            var namedCell = cell as INamedCell;
            var defenceAffectingCell = cell as IDefenceAffectingCell;
            var defBoost = defenceAffectingCell?.DefenceModifier ?? 0;

            _terrainDataText.text =
                $"<size=45>{namedCell?.CellName}</size>\n" +
                $"mov cost: {cell.MovementCost}\n" +
                $"def boost: {defBoost}";

            _terrainPanel.SetActive(true);
        }

        private void OnGameEnded(GameResult gameResult)
        {
            _gameOverText.text = $"Player {gameResult.Winners.First().PlayerNumber} wins!";
            _gameOverPanel.SetActive(true);

            _endTurnButton.interactable = false;
        }

        private void OnTurnStarted(TurnTransitionParams turnTransitionParams)
        {
            _endTurnButton.interactable = turnTransitionParams.TurnContext.CurrentPlayer.PlayerType.Equals(PlayerType.HumanPlayer);
        }

        private void OnRestartLevelButtonPressed()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        private void OnEndTurnButtonPressed()
        {
            _gridController.EndTurn();
        }
    }
}