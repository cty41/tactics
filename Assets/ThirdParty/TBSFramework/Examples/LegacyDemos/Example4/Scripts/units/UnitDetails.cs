using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units
{
    /// <summary>
    /// Provides editor-configurable data for a unit's name, price, and portrait.
    /// </summary>
    public class UnitDetails : MonoBehaviour, IUnitDetails
    {
        [SerializeField] private string _unitName;
        [SerializeField] private int _unitPrice;
        [SerializeField] private Sprite _unitPortrait;

        public string GetName()
        {
            return _unitName;
        }

        public Sprite GetPortrait()
        {
            return _unitPortrait;
        }

        public int GetPrice()
        {
            return _unitPrice;
        }
    }
}