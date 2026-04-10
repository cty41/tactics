using System.Linq;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Unity.Units;
using TurnBasedStrategyFramework.Unity.Units.Abilities;
using UnityEngine;
using UnityEngine.UI;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units.Abilities
{
    /// <summary>
    /// Allows units to capture enemy structures by adjusting their loyalty based on the health of the capturing unit.
    /// </summary>
    public class CaptureAbility : Ability
    {
        [SerializeField] private Button activationButton;
        [SerializeField] private ScriptableObject structureUnitType;

        private ICapturable _capturedStructure;

        public override void Initialize(IGridController gridController)
        {
            activationButton.onClick.AddListener(() =>
            {
                if (CanPerform(gridController))
                {
                    _capturedStructure = (UnitReference.CurrentCell.CurrentUnits
                        .First(u => (u as ITypedUnit).UnitType.Equals(structureUnitType)) as Unit).GetComponent<ICapturable>();

                    int loyaltyDelta = -Mathf.CeilToInt(UnitReference.Health * 10f / UnitReference.MaxHealth);
                    var color = (UnitReference as IColoredUnit).Color;

                    UnitReference.HumanExecuteAbility(new CaptureCommand(
                        _capturedStructure,
                        loyaltyDelta,
                        color,
                        structureUnitType,
                        UnitReference.CurrentCell.GridCoordinates.x,
                        UnitReference.CurrentCell.GridCoordinates.y
                    ), gridController);
                }
            });
        }

        public override void Display(IGridController gridController)
        {
            activationButton.gameObject.SetActive(CanPerform(gridController));
        }

        public override void CleanUp(IGridController gridController)
        {
            activationButton.gameObject.SetActive(false);
        }

        public override void OnAbilitySelected(IGridController gridController)
        {
            if (_capturedStructure != null)
            {
                if (!UnitReference.CurrentCell.CurrentUnits.Contains(_capturedStructure.UnitReference))
                {
                    _capturedStructure.ResetLoyalty();
                    _capturedStructure = null;
                }
            }
        }

        public override bool CanPerform(IGridController gridController)
        {
            return UnitReference.ActionPoints > 0 &&
                   UnitReference.CurrentCell.CurrentUnits.Any(u =>
                       (u as ITypedUnit).UnitType.Equals(structureUnitType) &&
                       u.PlayerNumber != UnitReference.PlayerNumber);
        }
    }
}
