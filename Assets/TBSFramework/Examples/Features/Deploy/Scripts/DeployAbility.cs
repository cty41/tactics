using System.Collections.Generic;
using System.Linq;
using TMPro;
using TurnBasedStrategyFramework.Common.Cells;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Cells;
using TurnBasedStrategyFramework.Unity.Units.Abilities;
using UnityEngine;
using UnityEngine.UI;

namespace TurnBasedStrategyFramework.Unity.Examples.Features.UnitDeployment
{
    /// <summary>
    /// Represents an ability that allows a player to deploy units on specific cells within the grid.
    /// Handles the visualization of deployable cells, creation of UI deployment buttons,
    /// and instantiation of selected unit types on chosen cells.
    /// </summary>
    public class DeployAbility : Ability
    {
        /// <summary>
        /// List of cells where deployment is allowed.
        /// </summary>
        [SerializeField] private List<Cell> _deplayableCells;

        /// <summary>
        /// List of available unit deployment options.
        /// </summary>
        [SerializeField] private List<DeployEntry> _deployEntries;
        private DeployEntry _selectedEntry;
        [SerializeField] private GameObject _deployButtonTemplate;
        private List<Button> _buttons = new List<Button>();

        public override void Initialize(IGridController gridController)
        {
            _selectedEntry = _deployEntries[0]; // Set _selectedEntry to the first element of _deployEntires by default
        }

        public override void Display(IGridController gridController)
        {
            gridController.CellManager.MarkAsReachable(_deplayableCells); // Mark _deployableCells as reachable to visually indicate that unit can be deployed to this position.
            foreach (var deployEntry in _deployEntries) // Iterate over _deployEntires to create a button to seelct given unit for deployment
            {
                var button = Instantiate(_deployButtonTemplate).GetComponent<Button>();
                button.GetComponentInChildren<TMP_Text>().text = deployEntry.UnitName;
                button.onClick.AddListener(() => { _selectedEntry = deployEntry; });

                button.gameObject.SetActive(true);
                button.transform.SetParent(_deployButtonTemplate.transform.parent);
                _buttons.Add(button);
            }
        }

        public override void CleanUp(IGridController gridController)
        {
            gridController.CellManager.UnMark(_deplayableCells);
            for (int i = 0; i < _buttons.Count; i++)
            {
                var button = _buttons[i];
                Destroy(button.gameObject);
            }
        }

        public override void OnCellDehighlighted(ICell cell, IGridController gridController)
        {
            if (_deplayableCells.Any(c => c.Equals(cell)))
            {
                gridController.CellManager.MarkAsReachable(cell);
            }
        }

        public override void OnCellClicked(ICell cell, IGridController gridController)
        {
            // Deploy selected unit to given position.
            if (!cell.IsTaken && _deplayableCells.Any(c => c.Equals(cell)))
            {
                var unit = Instantiate(_selectedEntry.UnitPrefab).GetComponent<IUnit>();
                unit.CurrentCell = cell;
                unit.WorldPosition = cell.WorldPosition;
                unit.PlayerNumber = UnitReference.PlayerNumber;

                cell.IsTaken = true;
                cell.CurrentUnits.Add(unit);

                gridController.UnitManager.AddUnit(unit);
            }
        }
    }
}