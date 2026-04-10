using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Features.UnitDeployment
{
    /// <summary>
    /// Represents a deployable unit entry used within the deployment system.
    /// Defines the unit’s display name and corresponding prefab for instantiation.
    /// </summary>
    public class DeployEntry : MonoBehaviour
    {
        /// <summary>
        /// Display name of the unit, shown in deployment UI elements.
        /// </summary>
        [field: SerializeField] public string UnitName { get; set; }

        /// <summary>
        /// Prefab reference for the unit to be deployed into the game.
        /// </summary>
        [field: SerializeField] public Unit UnitPrefab { get; set; }
    }
}
