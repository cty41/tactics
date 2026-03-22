using System;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Features.Initiative
{
    /// <summary>
    /// Represents the initiative status of a unit in an Initiative-based system.
    /// This component tracks the unit's base initiative speed and its accumulated turn charge.
    /// </summary>
    public class InitiativeComponent : MonoBehaviour
    {
        /// <summary>
        /// Event fired whenever the unit's <see cref="Charge"/> value changes.
        /// </summary>
        public event Action ChargeChanged;
        /// <summary>
        /// The unit's base speed used to accumulate turn charge. Higher values result in faster turns.
        /// </summary>
        [field: SerializeField] public float Initiative { get; set; }
        /// <summary>
        /// Gets the current accumulated charge. When this value meets or exceeds the threshold 
        /// set by the <see cref="InitiativeTurnResolver"/>, the unit is ready to act.
        /// </summary>
        public float Charge { get; private set; }
        /// <summary>
        /// Modifies the current charge by a specified amount.
        /// </summary>
        /// <param name="amount">The value to add to the current charge. Can be positive (accumulation) or negative (spending).</param>
        public void ModifyCharge(float amount)
        {
            Charge += amount;
            ChargeChanged?.Invoke();
        }
    }
}