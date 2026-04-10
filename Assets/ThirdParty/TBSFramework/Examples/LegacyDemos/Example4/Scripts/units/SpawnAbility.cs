using System.Collections.Generic;
using System.Linq;
using TMPro;
using TurnBasedStrategyFramework.Common.Cells;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Controllers.GridStates;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Units;
using TurnBasedStrategyFramework.Unity.Units.Abilities;
using UnityEngine;
using UnityEngine.UI;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units.Abilities
{
    /// <summary>
    /// Represents ability for spawning new units during the game.
    /// </summary>
    public class SpawnAbility : Ability
    {
        [Header("Unit Data")]
        [SerializeField] private List<GameObject> _unitData;

        [Header("UI Elements")]
        [SerializeField] private GameObject _container;
        [SerializeField] private Transform _buttonsParent;
        [SerializeField] private GameObject _buttonTemplate;

        [Header("Economy")]
        [SerializeField] private EconomyController _economyController;
        [SerializeField] private TextMeshProUGUI _resourcesLabel;

        private readonly List<GameObject> buttons = new();

        public override void Display(IGridController gridController)
        {
            _resourcesLabel.text = $"G. {_economyController.GetValue(UnitReference.PlayerNumber)}";
            foreach (var unit in _unitData)
            {
                GameObject buttonGO = Instantiate(_buttonTemplate, _buttonsParent);
                buttonGO.SetActive(true);

                var icon = buttonGO.transform.Find("UnitImage").GetComponent<Image>();
                icon.sprite = unit.GetComponent<IUnitDetails>().GetPortrait();

                var nameText = buttonGO.transform.Find("UnitNameText").GetComponent<TextMeshProUGUI>();
                nameText.text = unit.GetComponent<IUnitDetails>().GetName();

                var unitCost = unit.GetComponent<IUnitDetails>().GetPrice();
                var priceText = buttonGO.transform.Find("UnitPriceText").GetComponent<TextMeshProUGUI>();
                priceText.text = unitCost.ToString();

                var button = buttonGO.GetComponent<Button>();
                bool canAfford = _economyController.GetValue(UnitReference.PlayerNumber) >= unitCost;
                button.interactable = canAfford;

                button.onClick.AddListener(() =>
                {
                    var color = (UnitReference as IColoredUnit).Color;
                    UnitReference.HumanExecuteAbility(
                        new SpawnCommand(unit, UnitReference.CurrentCell,
                            color, _economyController, unitCost),
                            gridController
                    );
                });

                buttons.Add(buttonGO);
            }

            _container.SetActive(true);
        }

        public override void CleanUp(IGridController gridController)
        {
            _container.SetActive(false);
            foreach (var btn in buttons)
            {
                Destroy(btn);
            }
            buttons.Clear();
        }

        public override void OnCellClicked(ICell cell, IGridController gridController)
        {
            gridController.GridState = new GridStateAwaitInput();
        }

        public override void OnUnitClicked(IUnit unit, IGridController gridController)
        {
            if (gridController.TurnContext.PlayableUnits().Contains(unit))
            {
                gridController.GridState = new GridStateUnitSelected(unit, unit.GetBaseAbilities());
            }
        }

        public override bool CanPerform(IGridController gridController)
        {
            return !UnitReference.CurrentCell.IsTaken;
        }
    }
}
