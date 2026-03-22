using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Features.Initiative
{
    /// <summary>
    /// Manages the visual representation of a unit's initiative charge.
    /// </summary>
    public class ActionBar : MonoBehaviour
    {
        /// <summary>
        /// The InitiativeComponent this bar tracks for charge updates.
        /// </summary>
        [SerializeField] private InitiativeComponent _initiativeComponent;
        /// <summary>
        /// The Transform of the visual bar element whose scale will represent the charge.
        /// </summary>
        [SerializeField] private Transform _actionBar;

        private void Awake()
        {
            _initiativeComponent.ChargeChanged += OnChargeChanged;
        }
        private void OnChargeChanged()
        {
            // Note: Assuming a threshold of 1 for the normalization calculation.
            // If the threshold is dynamic, it should be retrieved from the InitiativeTurnResolver.
            _actionBar.localScale = new Vector3(_initiativeComponent.Charge / 1, 1, 1);
        }
    }
}