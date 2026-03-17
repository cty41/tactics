using TMPro;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.TilemapExample.Units
{
    /// <summary>
    /// Handles unit health display in Example 4
    /// </summary>
    public class HealthUpdate : MonoBehaviour
    {
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private Unit _unit;

        void Start()
        {
            _unit.HealthChanged += OnHealthChanged;
            _healthText.text = _unit.Health.ToString();
        }

        private void OnHealthChanged(HealthChangedEventArgs obj)
        {
            _healthText.text = obj.AffectedUnit.Health.ToString();
        }
    }
}

