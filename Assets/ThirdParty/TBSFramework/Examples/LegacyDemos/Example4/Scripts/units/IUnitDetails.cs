using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units
{
    /// <summary>
    /// Provides access to metadata for a unit, including name, cost, and visual portrait.
    /// </summary>
    public interface IUnitDetails
    {
        public string GetName();
        public int GetPrice();
        public Sprite GetPortrait();
    }
}