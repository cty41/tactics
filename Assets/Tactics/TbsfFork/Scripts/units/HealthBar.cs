using Tactics.Tbsf.Common.Units;
using UnityEngine;

namespace Tactics.Tbsf.Unity.Units
{
    /// <summary>
    /// Handles displaying the health bar.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Unit _unitReference;
        [SerializeField] private Transform _healthBar;

        private void Awake()
        {
            _unitReference.HealthChanged += OnHealthChanged;
        }

        private void OnHealthChanged(HealthChangedEventArgs eventArgs)
        {
            _healthBar.localScale = new Vector3(eventArgs.AffectedUnit.Health / eventArgs.AffectedUnit.MaxHealth, 1, 1);
        }
    }
}