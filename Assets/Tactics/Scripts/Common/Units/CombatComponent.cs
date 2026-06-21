using System;
using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Units.Buffs;
using UnityEngine;

namespace Tactics.Common.Units
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
        private static readonly System.Random _rng = new System.Random();

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

            if (_unitReference.Health < 0)
            {
                _unitReference.IsDowned = true;
            }
            else if (_unitReference.Health >= 0 && _unitReference.IsDowned)
            {
                _unitReference.IsDowned = false;
            }

            if (_unitReference.Health <= 0)
            {
                // Linked death: kill summoned unit when owner dies
                if (_unitReference.SummonedUnit != null && !_unitReference.SummonedUnit.IsDowned)
                {
                    var summoned = _unitReference.SummonedUnit;
                    _unitReference.SummonedUnit = null;
                    summoned.OwnerUnit = null;
                    summoned.OwnerUnitId = -1;
                    summoned.ModifyHealth(-summoned.Health - 1, null);
                }

                // Linked death: clear owner's SummonedUnit when summoned dies
                if (_unitReference.OwnerUnit != null)
                {
                    _unitReference.OwnerUnit.SummonedUnit = null;
                    _unitReference.OwnerUnit = null;
                }
                if (_unitReference.OwnerUnitId >= 0)
                {
                    _unitReference.OwnerUnitId = -1;
                }

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

        public float CalculateDamageDealt(IUnit defender, ICell defenderCell, ICell aggressorCell, bool isRangedDamage)
        {
            var damage = CalculateBaseDamageBeforeCrit(_unitReference, isRangedDamage);
            return IsCriticalHit() ? GetCriticalDamage(damage) : damage;
        }

        public float CalculateDamageTaken(IUnit aggressor, float damageDealt, ICell aggressorCell, ICell defenderCell)
        {
            return Math.Max(damageDealt - _unitReference.DefenceFactor, 1);
        }

        public float CalculateTotalDamage(IUnit defender, ICell defenderCell, ICell aggressorCell)
        {
            return CalculateTotalDamage(defender, defenderCell, aggressorCell, false);
        }

        public float CalculateTotalDamage(IUnit defender, ICell defenderCell, ICell aggressorCell, bool isRangedDamage)
        {
            var damageDealt = _unitReference.CalculateDamageDealt(defender, defenderCell, aggressorCell, isRangedDamage);
            var damageTaken = defender.CalculateDamageTaken(_unitReference, damageDealt, aggressorCell, defenderCell);

            return damageTaken;
        }

        public float CalculateExpectedTotalDamage(IUnit defender)
        {
            var baseDamage = CalculateBaseDamageBeforeCrit(_unitReference, false);
            var critChance = GetClampedCritChance(_unitReference);
            var expectedDamage = GetExpectedDamage(baseDamage, critChance);
            return Math.Max(expectedDamage - defender.DefenceFactor, 1);
        }

        private float GetAttributeScalingBonus(bool isRangedDamage)
        {
            return GetAttributeScalingBonus(_unitReference, isRangedDamage);
        }

        private bool IsCriticalHit()
        {
            return _rng.NextDouble() < GetClampedCritChance(_unitReference);
        }

        public static float GetClampedCritChance(IUnit unitReference)
        {
            var critChance = BaseCritChance + (unitReference.Luck - NeutralAttributeValue) * CritChancePerLuckPoint;
            return Math.Max(0f, Math.Min(1f, critChance));
        }

        public static float CalculateBaseDamageBeforeCrit(IUnit unitReference, bool isRangedDamage)
        {
            var baseDamage = unitReference.AttackFactor;
            float attributeBonus;
            if (isRangedDamage)
                attributeBonus = Mathf.FloorToInt((unitReference.Agility - NeutralAttributeValue) / 2f);
            else
                attributeBonus = unitReference.Strength - NeutralAttributeValue;
            return Math.Max(baseDamage + attributeBonus, 1);
        }

        /// <summary>
        /// Unified damage application method used by DamageEffect and BuffBehavior.
        /// Handles element type checks (ice break/immunity), buff hooks, crit, and defense.
        /// </summary>
        public static void ApplyDamage(
            IUnit caster,
            IUnit target,
            float baseDamage,
            bool isRangedDamage,
            ElementType elementType,
            bool canTriggerBeforeAttacked,
            bool canCrit,
            bool canTriggerDamageTaken)
        {
            // Check for Frozen buff - ice break logic
            bool isFrozen = target.BuffComponent?.HasBuff(BuffEffectType.Frozen) ?? false;
            if (isFrozen)
            {
                if (elementType == ElementType.Fire)
                {
                    var frozenBuffs = target.GetActiveBuffs()
                        .Where(b => b.Config.EffectType == BuffEffectType.Frozen)
                        .ToList();
                    foreach (var fb in frozenBuffs)
                        target.RemoveBuff(fb);
                }
                else
                {
                    return; // Non-fire damage blocked by ice
                }
            }

            float damage = baseDamage;
            bool isCritical = false;

            // Crit check
            if (canCrit)
            {
                isCritical = _rng.NextDouble() < GetClampedCritChance(caster);
            }

            // OnBeforeAttacked hook (e.g., Mark forces crit)
            if (canTriggerBeforeAttacked)
            {
                target.BuffComponent?.OnBeforeAttacked(caster, ref damage, ref isCritical);
            }

            if (isCritical)
            {
                damage = GetCriticalDamage(damage);
            }

            damage = target.CalculateDamageTaken(caster, damage, caster.CurrentCell, target.CurrentCell);

            // Curse damage amplifier: target takes 30% more damage
            if (target.BuffComponent != null && target.BuffComponent.HasBuff(BuffEffectType.CurseDamageAmplifier))
            {
                damage *= 1.3f;
            }

            target.ModifyHealth(-damage, caster);
            target.InvokeAttacked(new UnitAttackedEventArgs(target, caster, damage));

            if (canTriggerDamageTaken)
            {
                target.BuffComponent?.OnDamageTaken(caster, damage);
            }
        }

        public static float GetCriticalDamage(float baseDamage)
        {
            return baseDamage * CritDamageMultiplier;
        }

        public static float GetExpectedDamage(float baseDamage, float critChance)
        {
            return baseDamage * (1f - critChance) + GetCriticalDamage(baseDamage) * critChance;
        }

        public static float CalculateDodgeRate(IUnit unit)
        {
            return unit.DodgeRate;
        }

        /// <summary>
        /// 基于 Agility 计算基础闪避率。
        /// </summary>
        public static float CalculateDodgeChance(IUnit unit)
        {
            return unit.Agility > NeutralAttributeValue
                ? (unit.Agility - NeutralAttributeValue) * 0.02f
                : 0f;
        }

        public static bool IsHit(IUnit caster, IUnit target, float accuracyPenalty)
        {
            float finalHitChance = 1f;

            // Agility 基础闪避
            float agilityDodge = CalculateDodgeChance(target);
            finalHitChance *= (1f - agilityDodge);

            // Buff、装备等其他来源的闪避率
            finalHitChance *= (1f - target.DodgeRate);

            // 环境/技能造成的命中惩罚
            finalHitChance *= (1f - accuracyPenalty);

            finalHitChance = Mathf.Clamp01(finalHitChance);
            return _rng.NextDouble() < finalHitChance;
        }

        private static float GetAttributeScalingBonus(IUnit unitReference, bool isRangedDamage)
        {
            switch (isRangedDamage)
            {
                case true:
                    return ((unitReference.Agility - NeutralAttributeValue) / 2f);
                default:
                    return (unitReference.Strength - NeutralAttributeValue);
            }
        }
    }
}