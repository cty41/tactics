using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DG.Tweening;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
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
        [SerializeField] private DamageType _damageType = DamageType.Physical;
        [SerializeField] private ElementType _elementType = ElementType.None;

        public float BaseDamage => _baseDamage;
        public AttributeScalingType ScalingType => _scalingType;
        public bool IsRangedDamage => _isRangedDamage;
        public DamageType DamageType => _damageType;
        public ElementType ElementType => _elementType;

        public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
        {
            foreach (var target in targets)
            {
                if (target == null) continue;

                float baseDamage = _baseDamage;
                if (_scalingType != AttributeScalingType.None)
                {
                    float scaling = CombatComponent.CalculateBaseDamageBeforeCrit(caster, _isRangedDamage) - caster.AttackFactor;
                    baseDamage += scaling;
                }

                CombatComponent.ApplyDamage(
                    caster, target, baseDamage, _isRangedDamage, _elementType,
                    canTriggerBeforeAttacked: true, canCrit: true, canTriggerDamageTaken: true);
            }
            await Task.CompletedTask;
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
        [SerializeField] private BuffConfig _buffConfig;
        [SerializeField] private int _duration;

        public BuffConfig BuffConfig => _buffConfig;
        public int Duration => _duration;

        public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
        {
            foreach (var target in targets)
            {
                if (target == null) continue;
                if (_buffConfig == null) continue;

                var buff = new Buff(_buffConfig, caster, _duration);
                target.AddBuff(buff);
            }
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Applies damage over time to targets via a DoT buff.
    /// </summary>
    [Serializable]
    public class DamageOverTimeEffect : AbilityEffect
    {
        [SerializeField] private float _damagePerTurn;
        [SerializeField] private int _duration;
        [SerializeField] private BuffConfig _doTBuffConfig;

        public float DamagePerTurn => _damagePerTurn;
        public int Duration => _duration;
        public BuffConfig DoTBuffConfig => _doTBuffConfig;

        public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
        {
            foreach (var target in targets)
            {
                if (target == null) continue;

                if (_doTBuffConfig != null)
                {
                    var buff = new Buff(_doTBuffConfig, caster, _duration);
                    target.AddBuff(buff);
                }
                else
                {
                    Debug.LogWarning("[DamageOverTimeEffect] No DoT BuffConfig assigned. Skipping DoT application.");
                }
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
                    TBattleLog.Log(new AttackLogData
                    {
                        Attacker = caster is Tactics.Common.Units.INamedUnit na ? na.UnitName : caster.ToString(),
                        Target = target is Tactics.Common.Units.INamedUnit nt ? nt.UnitName : target.ToString(),
                        Damage = 0,
                        IsMissed = true
                    });
                    continue;
                }

                float baseDamage = BaseDamage;
                if (ScalingType != AttributeScalingType.None)
                {
                    float scaling = CombatComponent.CalculateBaseDamageBeforeCrit(caster, IsRangedDamage) - caster.AttackFactor;
                    baseDamage += scaling;
                }

                CombatComponent.ApplyDamage(
                    caster, target, baseDamage, IsRangedDamage, ElementType,
                    canTriggerBeforeAttacked: true, canCrit: true, canTriggerDamageTaken: true);
            }
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Performs a knockback flight: target is launched into the air along a parabolic arc
    /// and lands at a cell up to _distance away. Uses DOTween for smooth animation.
    /// </summary>
    [Serializable]
    public class KnockbackEffect : AbilityEffect
    {
        [SerializeField] private int _distance;
        [SerializeField] private float _duration = 0.5f;
        [SerializeField] private float _height = 2f;

        public int Distance => _distance;
        public float Duration => _duration;
        public float Height => _height;

        public override async Task Execute(IUnit caster, IEnumerable<IUnit> targets, IGridController gridController)
        {
            foreach (var target in targets)
            {
                if (target == null) continue;

                ICell targetCell = target.CurrentCell;
                ICell casterCell = caster.CurrentCell;
                if (targetCell == null || casterCell == null) continue;

                // Calculate direction (caster -> target)
                int dx = targetCell.GridCoordinates.x - casterCell.GridCoordinates.x;
                int dy = targetCell.GridCoordinates.y - casterCell.GridCoordinates.y;
                float mag = Mathf.Sqrt(dx * dx + dy * dy);
                if (mag < 0.01f) continue;

                int dirX = Mathf.RoundToInt(dx / mag);
                int dirY = Mathf.RoundToInt(dy / mag);

                // Find landing cell
                ICell landingCell = FindLandingCell(targetCell, dirX, dirY, _distance, gridController);

                if (landingCell != null && landingCell != targetCell)
                {
                    // Remove from old cell before animation
                    if (target.CurrentCell != null)
                    {
                        target.CurrentCell.CurrentUnits.Remove(target);
                        target.CurrentCell.IsTaken = target.CurrentCell.CurrentUnits.Count > 0;
                    }

                    // Perform parabolic flight animation
                    await PerformKnockbackFlight(target, landingCell, _duration, _height);

                    // Update to new cell after landing
                    target.CurrentCell = landingCell;
                    if (!landingCell.CurrentUnits.Contains(target))
                        landingCell.CurrentUnits.Add(target);
                    landingCell.IsTaken = landingCell.CurrentUnits.Count > 0;
                    target.WorldPosition = landingCell.WorldPosition;
                }
            }
        }

        private ICell FindLandingCell(ICell startCell, int dirX, int dirY, int maxDistance, IGridController gridController)
        {
            ICell lastValidCell = startCell;

            for (int i = 1; i <= maxDistance; i++)
            {
                var coord = new Vector2IntImpl(startCell.GridCoordinates.x + dirX * i, startCell.GridCoordinates.y + dirY * i);
                var candidateCell = gridController.CellManager.GetCellAt(coord);
                if (candidateCell == null) break; // Out of bounds
                if (!gridController.CellManager.IsCellWalkable(candidateCell)) break; // Not walkable

                lastValidCell = candidateCell;
            }

            return lastValidCell != startCell ? lastValidCell : null;
        }

        private async Task PerformKnockbackFlight(IUnit target, ICell landingCell, float duration, float height)
        {
            if (target is not MonoBehaviour mb) return;

            Vector3 startPos = mb.transform.position;
            Vector3 endPos = landingCell.WorldPosition.ToVector3();

            float progress = 0f;
            var tween = DOTween.To(() => progress, x => progress = x, 1f, duration)
                .OnUpdate(() =>
                {
                    Vector3 pos = Vector3.Lerp(startPos, endPos, progress);
                    pos.y += Mathf.Sin(Mathf.PI * progress) * height;
                    mb.transform.position = pos;
                })
                .SetEase(Ease.Linear);

            await tween.AsyncWaitForCompletion();
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
