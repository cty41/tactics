using System.Collections.Generic;
using System.Linq;
using TMPro;
using TurnBasedStrategyFramework.Unity.Controllers;
using TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units.Abilities;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.GUI
{
    public class SummaryUI : MonoBehaviour
    {
        [SerializeField] private GameObject _playerPanelTemplate;
        [SerializeField] private TMP_Text _turnText;
        [SerializeField] private UnityGridController _gridController;

        [SerializeField] private ScriptableObject _structureUnitType;
        [SerializeField] private EconomyController _economyController;

        private List<GameObject> _infoPanels;
        private int _turnsPassed = 1;
        private Dictionary<int, int> _lostUnits;

        private void Awake()
        {
            _gridController.TurnStarted += (context) => _turnsPassed++;
            _gridController.GameInitialized += () =>
            {
                _lostUnits = new Dictionary<int, int>();
                foreach (var player in _gridController.PlayerManager.GetPlayers())
                {
                    _lostUnits[player.PlayerNumber] = 0;
                }
                _gridController.UnitManager.UnitRemoved += (unit) => _lostUnits[unit.PlayerNumber]++;
            };


        }
        public void CleanUp()
        {
            foreach (var panel in _infoPanels)
            {
                Destroy(panel);
            }
        }

        public void UpdateUI()
        {
            _turnText.text = $"Day {_turnsPassed / _gridController.PlayerManager.GetPlayers().Count() + 1}";

            _infoPanels = new List<GameObject>();
            foreach (var player in _gridController.PlayerManager.GetPlayers())
            {
                var playerPanel = Instantiate(_playerPanelTemplate, _playerPanelTemplate.transform.parent);

                var playerPanelComponent = playerPanel.GetComponent<PlayerPanel>();

                playerPanelComponent.PlayerNumberText.text = $"Player {player.PlayerNumber + 1}";
                playerPanelComponent.UnitsText.text = $"{_gridController.UnitManager.GetUnits().Count(u => u.PlayerNumber == player.PlayerNumber && !(u as ITypedUnit).UnitType.Equals(_structureUnitType))}";
                playerPanelComponent.UnitsLostText.text = $"{_lostUnits[player.PlayerNumber]}";
                playerPanelComponent.BasesText.text = $"{_gridController.UnitManager.GetUnits().Count(u => u.PlayerNumber == player.PlayerNumber && (u as ITypedUnit).UnitType.Equals(_structureUnitType))}";
                playerPanelComponent.IncomeText.text = $"{_gridController.UnitManager.GetUnits().Where(u => u.PlayerNumber == player.PlayerNumber && (u as ITypedUnit).UnitType.Equals(_structureUnitType)).Select(u => (u as Unit).GetComponent<IncomeGenerationAbility>()).Sum(a => a.Amount)}";
                playerPanelComponent.FundsText.text = $"{_economyController.GetValue(player.PlayerNumber)}";

                playerPanel.SetActive(true);
                _infoPanels.Add(playerPanel);
            }
        }
    }
}