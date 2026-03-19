using System;
using TurnBasedStrategyFramework.Common.Cells;

namespace TurnBasedStrategyFramework.Common.Units
{
    /// <summary>
    /// Represents the combat component of a unit, handling health modifications, damage calculations, and attack checks.
    /// </summary>
    public class CombatComponent
    {
        private const int NeutralAttributeValue = 5;
        private const float BaseCritChance = 0.10f;
        private const float CritChancePerLuckPoint = 0.02f;
        private const float CritDamageMultiplier = 2f;
        private static readonly Random _rng = new Random();

        /// <summary>
        /// The unit that owns this combat component.
        /// </summary>
        private readonly IUnit _unitReference;

        /// <summary>
        /// Initializes a new instance of the <see cref="CombatComponent"/> class with the specified unit reference.
        /// </summary>
        /// <param name="unitReference">The unit that owns this combat component.</param>
        public CombatComponent(IUnit unitReference)
        {
            _unitReference = unitReference;
        }

        public void ModifyHealth(float healthChangeAmount, IUnit sourceUnit)
        {
            _unitReference.Health += healthChangeAmount;
            _unitReference.InvokeHealthChanged(new HealthChangedEventArgs(_unitReference, sourceUnit, healthChangeAmount));
            if (_unitReference.Health <= 0)
            {
                _unitReference.InvokeDestroyed(new UnitDestroyedEventArgs(_unitReference, sourceUnit));
            }
        }

        public bool IsUnitAttackable(IUnit otherUnit, ICell otherUnitCell, ICell attackSourceCell)
        {
            return otherUnitCell.GetDistance(attackSourceCell) <= _unitReference.AttackRange;
        }

        public float CalculateDamageDealt(IUnit defender, ICell defenderCell, ICell aggressorCell)
        {
            return CalculateDamageDealt(defender, defenderCell, aggressorCell, false);
        }

        public float CalculateDamageDealt(IUnit defender, ICell defenderCell, ICell aggressorCell, bool isRangedDamage, bool halfScaling = false)
        {
            var baseDamage = _unitReference.AttackFactor;
            var attributeBonus = GetAttributeScalingBonus(isRangedDamage, halfScaling);
            var damage = Math.Max(baseDamage + attributeBonus, 1);

            return IsCriticalHit() ? damage * CritDamageMultiplier : damage;
        }

        public float CalculateDamageTaken(IUnit aggressor, float damageDealt, ICell aggressorCell, ICell defenderCell)
        {
            return Math.Max(damageDealt - _unitReference.DefenceFactor, 1);
        }

        public float CalculateTotalDamage(IUnit defender, ICell defenderCell, ICell aggressorCell)
        {
            return CalculateTotalDamage(defender, defenderCell, aggressorCell, false);
        }

        public float CalculateTotalDamage(IUnit defender, ICell defenderCell, ICell aggressorCell, bool isRangedDamage, bool halfScaling = false)
        {
            var damageDealt = _unitReference.CalculateDamageDealt(defender, defenderCell, aggressorCell, isRangedDamage, halfScaling);
            var damageTaken = defender.CalculateDamageTaken(_unitReference, damageDealt, aggressorCell, defenderCell);

            return damageTaken;
        }

        private float GetAttributeScalingBonus(bool isRangedDamage, bool halfScaling)
        {
            var scalingMultiplier = halfScaling ? 0.5f : 1f;
            switch (isRangedDamage)
            {
                case true:
                    return ((_unitReference.Agility - NeutralAttributeValue) / 2f) * scalingMultiplier;
                default:
                    return (_unitReference.Strength - NeutralAttributeValue) * scalingMultiplier;
            }
        }

        private bool IsCriticalHit()
        {
            var critChance = BaseCritChance + (_unitReference.Luck - NeutralAttributeValue) * CritChancePerLuckPoint;
            var clampedChance = Math.Max(0f, Math.Min(1f, critChance));
            return _rng.NextDouble() < clampedChance;
        }
    }
}