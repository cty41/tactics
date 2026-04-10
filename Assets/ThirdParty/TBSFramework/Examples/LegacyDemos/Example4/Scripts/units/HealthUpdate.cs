using TMPro;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units
{
    /// <summary>
    /// Handles unit health display in Example 4
    /// </summary>
    public class HealthUpdate : MonoBehaviour
    {
        [SerializeField] private TMP_Text _hpText;
        [SerializeField] private Unit _unit;

        void Start()
        {
            _unit.HealthChanged += OnHealthChanged;
        }

        private void OnHealthChanged(HealthChangedEventArgs obj)
        {
            _hpText.text = ((int)Mathf.Ceil(_unit.Health * 10f / _unit.MaxHealth)).ToString();
        }
    }
}

