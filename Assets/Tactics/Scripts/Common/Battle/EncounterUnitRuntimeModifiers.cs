using Tactics.Common.Units;
using UnityEngine;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// Stores authored encounter modifiers on a spawned formal enemy unit.
    /// </summary>
    public sealed class EncounterUnitRuntimeModifiers : MonoBehaviour
    {
        public string MonsterId { get; private set; }
        public float HealthMultiplier { get; private set; } = 1f;
        public float OutputMultiplier { get; private set; } = 1f;
        public int MinimumStartingMana { get; private set; }
        public bool IsFormalEncounterUnit => !string.IsNullOrWhiteSpace(MonsterId);

        public void Configure(string monsterId, float healthMultiplier, float outputMultiplier, int minimumStartingMana)
        {
            MonsterId = monsterId ?? string.Empty;
            HealthMultiplier = Mathf.Max(0.01f, healthMultiplier);
            OutputMultiplier = Mathf.Max(0.01f, outputMultiplier);
            MinimumStartingMana = Mathf.Max(0, minimumStartingMana);
        }

        public void ApplyAfterUnitInitialization(IUnit unit)
        {
            if (unit is not Unit concreteUnit)
                return;

            concreteUnit.MaxHealth = Mathf.Max(1, Mathf.CeilToInt(concreteUnit.MaxHealth * HealthMultiplier));
            concreteUnit.Health = concreteUnit.MaxHealth;
            if (MinimumStartingMana > 0)
            {
                concreteUnit.MaxMana = Mathf.Max(concreteUnit.MaxMana, MinimumStartingMana);
                concreteUnit.Mana = Mathf.Max(concreteUnit.Mana, MinimumStartingMana);
            }
        }

        public static float ResolveOutputMultiplier(IUnit source)
        {
            if (source is not Component component ||
                !component.TryGetComponent<EncounterUnitRuntimeModifiers>(out var modifiers))
            {
                return 1f;
            }

            return modifiers.OutputMultiplier;
        }
    }
}
