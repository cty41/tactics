using System.Linq;
using TMPro;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Controllers.GameResolvers;
using TurnBasedStrategyFramework.Common.Players;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Controllers;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;
using UnityEngine.UI;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example1.GUI
{
    /// <summary>
    /// GUI Controller for the Example 1 demo, manages the game's user interface.
    /// </summary>
    public class Example1GUIController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Button restartLevelButton;

        [Header("Controllers")]
        [SerializeField] private UnityGridController gridController;
        [SerializeField] private UnityUnitManager unitManager;

        [Header("Unit UI")]
        [SerializeField] private Image unitColorImage;
        [SerializeField] private TextMeshProUGUI unitNameLabel;
        [SerializeField] private TextMeshProUGUI healthLabel;
        [SerializeField] private TextMeshProUGUI attackLabel;
        [SerializeField] private TextMeshProUGUI defenceLabel;
        [SerializeField] private TextMeshProUGUI rangeLabel;

        [Header("Status UI")]
        [SerializeField] private TextMeshProUGUI statusLabel;

        private IUnit highlightedUnit;

        private void Awake()
        {
            endTurnButton.onClick.AddListener(OnEndTurnButtonPressed);
            restartLevelButton.onClick.AddListener(OnRestartLevelButtonPressed);

            gridController.TurnStarted += OnTurnStarted;
            gridController.GameEnded += OnGameEnded;
            unitManager.UnitAdded += OnUnitAdded;

            ClearUnitInfo();
        }

        private void OnTurnStarted(TurnTransitionParams turnParams)
        {
            var turnState = turnParams.TurnContext;
            statusLabel.text = $"PLAYER {turnState.CurrentPlayer.PlayerNumber}";
            endTurnButton.interactable = !turnState.CurrentPlayer.PlayerType.Equals(PlayerType.AutomatedPlayer);
        }

        private void OnGameEnded(GameResult result)
        {
            statusLabel.text = $"PLAYER {result.Winners.First().PlayerNumber} WINS!";
            endTurnButton.interactable = false;
        }

        private void OnUnitAdded(IUnit unit)
        {
            unit.UnitHighlighted += OnUnitHighlighted;
            unit.UnitDehighlighted += OnUnitDehighlighted;
            unit.HealthChanged += OnHealthChanged;
            unit.UnitDestroyed += OnUnitDestroyed;
        }

        private void OnHealthChanged(HealthChangedEventArgs args)
        {
            if (args.AffectedUnit == highlightedUnit)
            {
                OnUnitHighlighted(args.AffectedUnit);
            }
        }

        private void OnUnitDestroyed(UnitDestroyedEventArgs args)
        {
            var unit = args.AffectedUnit;
            unit.UnitHighlighted -= OnUnitHighlighted;
            unit.UnitDehighlighted -= OnUnitDehighlighted;
            unit.HealthChanged -= OnHealthChanged;
            unit.UnitDestroyed -= OnUnitDestroyed;
        }

        private void OnUnitHighlighted(IUnit unit)
        {
            highlightedUnit = unit;

            if (unit is IColoredUnit coloredUnit)
            {
                unitColorImage.color = coloredUnit.Color;
            }

            if (unit is INamedUnit namedUnit)
            {
                unitNameLabel.text = namedUnit.UnitName;
            }

            healthLabel.text = $"Health: {Mathf.Max(0, unit.Health)} / {unit.MaxHealth}";
            attackLabel.text = $"Attack: {unit.AttackFactor}";
            defenceLabel.text = $"Defence: {unit.DefenceFactor}";
            rangeLabel.text = $"Range: {unit.AttackRange}";
        }

        private void OnUnitDehighlighted(IUnit unit)
        {
            highlightedUnit = null;
            unitColorImage.color = Color.white;
            ClearUnitInfo();
        }

        private void ClearUnitInfo()
        {
            unitNameLabel.text = "";
            healthLabel.text = "";
            attackLabel.text = "";
            defenceLabel.text = "";
            rangeLabel.text = "";
        }

        private void OnRestartLevelButtonPressed()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        private void OnEndTurnButtonPressed()
        {
            gridController.EndTurn();
        }
    }
}

