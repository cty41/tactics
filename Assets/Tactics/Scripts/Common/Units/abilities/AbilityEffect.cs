using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Runtime.BattleLog;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Base class for all ability effects. Effects define what happens when an ability is executed.
    /// </summary>
    [Serializable]
    public abstract class AbilityEffect
    {
        /// <summary>
        /// Executes the effect on the specified targets.
        /// </summary>
        public abstract Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController);
    }

    /// <summary>
    /// Deals damage to targets using the caster's combat stats.
    /// </summary>
    [Serializable]
    public class DamageEffect : AbilityEffect
    {
        [SerializeField] private float _baseDamage;
        [SerializeField] private AttributeScalingType _scalingType;
        [SerializeField] private bool _isRangedDamage;

        public float BaseDamage => _baseDamage;
        public AttributeScalingType ScalingType => _scalingType;
        public bool IsRangedDamage => _isRangedDamage;

        public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
        {
            foreach (var target in targets)
            {
                if (target == null) continue;

                float damage = CalculateDamage(caster, target);
                target.ModifyHealth(-damage, caster);
                target.InvokeAttacked(new UnitAttackedEventArgs(target, caster, damage));
            }
            await Task.CompletedTask;
        }

        protected virtual float CalculateDamage(IUnit caster, IUnit target)
        {
            float damage = _baseDamage;
            if (_scalingType != AttributeScalingType.None)
            {
                float scaling = CombatComponent.CalculateBaseDamageBeforeCrit(caster, _isRangedDamage) - caster.AttackFactor;
                damage += scaling;
            }
            if (UnityEngine.Random.value < CombatComponent.GetClampedCritChance(caster))
            {
                damage = CombatComponent.GetCriticalDamage(damage);
            }
            damage = target.CalculateDamageTaken(caster, damage, caster.CurrentCell, target.CurrentCell);
            return damage;
        }
    }

    /// <summary>
    /// Restores health to targets.
    /// </summary>
    [Serializable]
    public class HealEffect : AbilityEffect
    {
        [SerializeField] private float _healAmount;
        [SerializeField] private bool _capAtMaxHealth = true;

        public float HealAmount => _healAmount;
        public bool CapAtMaxHealth => _capAtMaxHealth;

        public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
        {
            foreach (var target in targets)
            {
                if (target == null) continue;

                float heal = _healAmount;
                if (_capAtMaxHealth)
                {
                    float maxPossibleHeal = target.MaxHealth - target.Health;
                    heal = Mathf.Min(heal, maxPossibleHeal);
                }
                target.ModifyHealth(heal, caster);
            }
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Moves the caster to a target cell.
    /// </summary>
    [Serializable]
    public class MoveEffect : AbilityEffect
    {
        [SerializeField] private bool _requiresPathfinding = true;

        public MoveEffect(bool requiresPathfinding = true)
        {
            _requiresPathfinding = requiresPathfinding;
        }

        public bool RequiresPathfinding => _requiresPathfinding;

        public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
        {
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Applies a buff to targets.
    /// </summary>
    [Serializable]
    public class ApplyBuffEffect : AbilityEffect
    {
        [SerializeField] private string _buffType;
        [SerializeField] private int _duration;

        public string BuffType => _buffType;
        public int Duration => _duration;

        public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
        {
            foreach (var target in targets)
            {
                if (target == null) continue;
                // TODO: Implement buff system integration
            }
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Applies damage over time to targets.
    /// </summary>
    [Serializable]
    public class DamageOverTimeEffect : AbilityEffect
    {
        [SerializeField] private float _damagePerTurn;
        [SerializeField] private int _duration;

        public float DamagePerTurn => _damagePerTurn;
        public int Duration => _duration;

        public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
        {
            foreach (var target in targets)
            {
                if (target == null) continue;
                // TODO: Apply DoT buff
            }
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Deals damage to targets with an accuracy penalty. May miss based on target dodge rate.
    /// </summary>
    [Serializable]
    public class AccuracyDamageEffect : DamageEffect
    {
        [SerializeField] private float _accuracyPenalty = 0f;

        public float AccuracyPenalty => _accuracyPenalty;

        public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
        {
            foreach (var target in targets)
            {
                if (target == null) continue;

                if (!CombatComponent.IsHit(caster, target, _accuracyPenalty))
                {
                    BattleLogger.Log(new AttackLogData
                    {
                        Attacker = caster is Tactics.Common.Units.INamedUnit na ? na.UnitName : caster.ToString(),
                        Target = target is Tactics.Common.Units.INamedUnit nt ? nt.UnitName : target.ToString(),
                        Damage = 0,
                        IsMissed = true
                    });
                    continue;
                }

                float damage = CalculateDamage(caster, target);
                target.ModifyHealth(-damage, caster);
                target.InvokeAttacked(new UnitAttackedEventArgs(target, caster, damage));
            }
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Knocks targets back a specified distance.
    /// </summary>
    [Serializable]
    public class KnockbackEffect : AbilityEffect
    {
        [SerializeField] private int _distance;

        public int Distance => _distance;

        public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
        {
            foreach (var target in targets)
            {
                if (target == null) continue;
                // TODO: Implement knockback logic
            }
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Spawns a prefab at a target location.
    /// </summary>
    [Serializable]
    public class SpawnEffect : AbilityEffect
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField] private Vector3 _spawnOffset;

        public GameObject Prefab => _prefab;
        public Vector3 SpawnOffset => _spawnOffset;

        public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
        {
            foreach (var target in targets)
            {
                if (target == null) continue;
                if (_prefab != null)
                {
                    var worldPos = target.WorldPosition;
                    var pos = new UnityEngine.Vector3(worldPos.x, worldPos.y, worldPos.z) + _spawnOffset;
                    GameObject.Instantiate(_prefab, pos, Quaternion.identity);
                }
            }
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Defines which attribute is used for damage scaling.
    /// </summary>
    public enum AttributeScalingType
    {
        None,
        Strength,
        Agility,
        Intelligence,
        Charisma
    }
}
