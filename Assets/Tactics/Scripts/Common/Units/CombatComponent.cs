using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Units.Buffs;
using Tactics.Runtime.BattleLog;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Units
{
    /// <summary>
    /// Represents the combat component of a unit, handling health modifications, damage calculations, and attack checks.
    /// </summary>
    public class CombatComponent
    {
        private readonly struct DamageShieldState
        {
            public DamageShieldState(float amount, bool absorbsAllDamage)
            {
                Amount = amount;
                AbsorbsAllDamage = absorbsAllDamage;
            }

            public float Amount { get; }
            public bool AbsorbsAllDamage { get; }
        }

        private static readonly Dictionary<IUnit, DamageShieldState> DamageShields = new();
        private static readonly Dictionary<IUnit, int> CombatTechniqueLevels = new();
        private static readonly System.Random CombatTechniqueRandom = new();
        private static double? _combatTechniqueRollOverride;

        public static void EnableCombatTechniques(IUnit unit, int level = 1)
        {
            if (unit != null) CombatTechniqueLevels[unit] = Math.Max(1, level);
        }

        public static void DisableCombatTechniques(IUnit unit)
        {
            if (unit != null) CombatTechniqueLevels.Remove(unit);
        }

        public static bool HasCombatTechniques(IUnit unit)
        {
            return GetCombatTechniqueLevel(unit) > 0;
        }

        public static int GetCombatTechniqueLevel(IUnit unit) =>
            unit != null && CombatTechniqueLevels.TryGetValue(unit, out int level) ? level : 0;

        public static void SetCombatTechniqueRollForTests(double? value) =>
            _combatTechniqueRollOverride = value;

        public static bool RollCombatTechniqueFollowUp(IUnit unit) =>
            GetCombatTechniqueLevel(unit) >= 2 && NextCombatTechniqueRoll() < 0.30d;

        public static void ApplyDamageShield(IUnit unit, float amount, bool absorbsAllDamage = false)
        {
            if (unit != null)
                DamageShields[unit] = new DamageShieldState(Math.Max(0f, amount), absorbsAllDamage);
        }

        public static float GetDamageShield(IUnit unit)
        {
            return unit != null && DamageShields.TryGetValue(unit, out var value) ? value.Amount : 0f;
        }
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
            // Reanimated summons remain selectable by healing abilities, but every
            // positive HP change resolves as no effect at the shared health boundary.
            if (healthChangeAmount > 0f && !_unitReference.CanReceiveHealing)
                return;

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
            if (GetCombatTechniqueLevel(unitReference) >= 3)
                critChance += 0.20f;
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
        public static DamageResolution ApplyDamage(
            IUnit caster,
            IUnit target,
            float baseDamage,
            bool isRangedDamage,
            ElementType elementType,
            bool canTriggerBeforeAttacked,
            bool canCrit,
            bool canTriggerDamageTaken,
            string logSourceName = null,
            bool bypassDefense = false,
            float accuracyPenalty = 0f)
        {
            return ApplyDamage(
                caster,
                target,
                baseDamage,
                isRangedDamage,
                DamageCategory.Physical,
                elementType,
                canTriggerBeforeAttacked,
                canCrit,
                canTriggerDamageTaken,
                logSourceName,
                bypassDefense,
                accuracyPenalty);
        }

        /// <summary>
        /// Applies one damage attempt with independent category and element classification.
        /// </summary>
        public static DamageResolution ApplyDamage(
            IUnit caster,
            IUnit target,
            float baseDamage,
            bool isRangedDamage,
            DamageCategory damageCategory,
            ElementType elementType,
            bool canTriggerBeforeAttacked,
            bool canCrit,
            bool canTriggerDamageTaken,
            string logSourceName = null,
            bool bypassDefense = false,
            float accuracyPenalty = 0f)
        {
            if (target == null)
                return DamageResolution.Invalid();

            float dodgeChance = 0f;
            if (caster != null && canTriggerBeforeAttacked)
            {
                float agilityDodge = Mathf.Clamp01(CalculateDodgeChance(target));
                float otherDodge = Mathf.Clamp01(target.DodgeRate);
                float accuracyMiss = Mathf.Clamp01(accuracyPenalty);
                dodgeChance = 1f -
                    (1f - agilityDodge) * (1f - otherDodge) * (1f - accuracyMiss);
                if (GetCombatTechniqueLevel(target) >= 1)
                    dodgeChance += 0.30f;
            }
            if (dodgeChance > 0f && NextCombatTechniqueRoll() < Math.Min(1f, dodgeChance))
            {
                TLog.Info($"[CombatTechniques] {target.UnitID} dodged direct damage.");
                LogMiss(caster, target);
                return DamageResolution.Dodged();
            }

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
                    LogMiss(caster, target);
                    return DamageResolution.Blocked(); // Non-fire damage blocked by ice
                }
            }

            // Encounter output scaling belongs at the unified damage entry so direct
            // attacks, abilities, retaliation, and source-traceable DoT share one rule.
            float damage = baseDamage * Tactics.Common.Battle.EncounterUnitRuntimeModifiers.ResolveOutputMultiplier(caster);
            bool isCritical = false;

            // Crit check
            if (canCrit && caster != null)
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

            if (!bypassDefense)
            {
                damage = target.CalculateDamageTaken(
                    caster,
                    damage,
                    caster?.CurrentCell,
                    target.CurrentCell);
            }

            // Curse damage amplifier: target takes 30% more damage
            if (target.BuffComponent != null && target.BuffComponent.HasBuff(BuffEffectType.CurseDamageAmplifier))
            {
                damage *= 1.3f;
            }

            if (target.BuffComponent != null && target.BuffComponent.HasBuff(BuffEffectType.DamageReduction))
            {
                foreach (var armor in target.GetActiveBuffs())
                {
                    if (armor.Config.EffectType != BuffEffectType.DamageReduction)
                        continue;
                    damage *= Mathf.Clamp01(1f - armor.Config.DamageReductionPercent);
                }
            }

            if (DamageShields.TryGetValue(target, out var shieldState)
                && (damageCategory == DamageCategory.Physical || shieldState.AbsorbsAllDamage))
            {
                float shield = shieldState.Amount;
                float absorbed = Math.Min(shield, damage);
                damage -= absorbed;
                shield -= absorbed;
                if (shield <= 0f) DamageShields.Remove(target);
                else DamageShields[target] = new DamageShieldState(shield, shieldState.AbsorbsAllDamage);
            }

            if (damage > 0f) target.ModifyHealth(-damage, caster);
            target.InvokeAttacked(new UnitAttackedEventArgs(target, caster, damage));

            LogDamage(logSourceName ?? GetUnitName(caster), target, damage);

            if (canTriggerDamageTaken)
            {
                target.BuffComponent?.OnDamageTaken(caster, damage, isRangedDamage);
            }

            return DamageResolution.Hit(damage, isCritical);
        }

        private static double NextCombatTechniqueRoll() =>
            _combatTechniqueRollOverride ?? CombatTechniqueRandom.NextDouble();

        private static void LogDamage(string sourceName, IUnit target, float damage)
        {
            if (!TBattleLog.IsBattleActive || target == null)
                return;

            TBattleLog.Log(new DamageLogData
            {
                Source = string.IsNullOrWhiteSpace(sourceName) ? "Unknown" : sourceName,
                Target = GetUnitName(target),
                Damage = damage,
                RemainingHealth = target.Health
            });
        }

        private static void LogMiss(IUnit caster, IUnit target)
        {
            if (!TBattleLog.IsBattleActive || target == null)
                return;

            TBattleLog.Log(new AttackLogData
            {
                Attacker = GetUnitName(caster),
                Target = GetUnitName(target),
                IsMissed = true
            });
        }

        private static string GetUnitName(IUnit unit)
        {
            if (unit is INamedUnit named && !string.IsNullOrWhiteSpace(named.UnitName))
                return named.UnitName;

            return unit == null ? "Unknown" : $"Unit_{unit.UnitID}";
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
