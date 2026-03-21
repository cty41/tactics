using TMPro;
using Tactics.Tbsf.Common.Units;
using Tactics.Tbsf.Unity.Units;
using UnityEngine;

namespace Tactics.Units
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

