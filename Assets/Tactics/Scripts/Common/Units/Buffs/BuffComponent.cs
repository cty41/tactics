using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Battle;
using Tactics.Common.Controllers;
using Tactics.Runtime.BattleLog;
using UnityEngine;

namespace Tactics.Common.Units.Buffs
{
    /// <summary>
    /// Manages buffs for a single unit. Each unit has its own BuffComponent instance.
    /// Not a singleton - follows the same composition pattern as CombatComponent.
    /// </summary>
    public class BuffComponent
    {
        private readonly IUnit _owner;
        private readonly List<Buff> _activeBuffs;
        private IGridController _gridController;

        public event Action<BuffChangedEventArgs> BuffChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="BuffComponent"/> class.
        /// </summary>
        /// <param name="owner">The unit this component manages buffs for.</param>
        public BuffComponent(IUnit owner, IGridController gridController = null)
        {
            _owner = owner;
            _gridController = gridController;
            _activeBuffs = new List<Buff>();
        }

        /// <summary>
        /// Binds the battle controller after buffs were restored before unit initialization.
        /// Existing buffs stay active and can participate in initiative refreshes afterwards.
        /// </summary>
        public void BindGridController(IGridController gridController)
        {
            _gridController = gridController;
        }

        /// <summary>
        /// Applies a new buff to the owner unit. Sets the owner reference before applying.
        /// </summary>
        /// <param name="buff">The buff to apply.</param>
        public void AddBuff(Buff buff)
        {
            if (buff == null)
            {
                throw new ArgumentNullException(nameof(buff), "Cannot add a null buff.");
            }

            // Standard statuses merge by effect type; other buffs merge by Config reference.
            if (buff.Config != null)
            {
                for (int i = 0; i < _activeBuffs.Count; i++)
                {
                    if (IsSameRuntimeStatus(_activeBuffs[i], buff))
                    {
                        var existing = _activeBuffs[i];
                        switch (ResolveRefreshStrategy(existing.Config))
                        {
                            case BuffRefreshStrategy.RefreshDuration:
                                existing.RemainingTurns = buff.RemainingTurns;
                                break;
                            case BuffRefreshStrategy.AddStacks:
                                existing.StackCount += buff.StackCount;
                                break;
                            default:
                                existing.RemainingTurns += buff.RemainingTurns;
                                break;
                        }

                        NotifyStatusChanged(existing.Config);
                        BuffChanged?.Invoke(new BuffChangedEventArgs(BuffChangeType.Refreshed, existing));
                        LogBuff(buff.Source, _owner, existing.BuffName, GetDisplayedAmount(existing));
                        return;
                    }
                }

                // Curse category exclusivity: only one curse per unit, later replaces earlier
                if (!string.IsNullOrEmpty(buff.Config.CurseCategory))
                {
                    for (int i = _activeBuffs.Count - 1; i >= 0; i--)
                    {
                        if (_activeBuffs[i].Config != null && _activeBuffs[i].Config.CurseCategory == buff.Config.CurseCategory)
                        {
                            RemoveBuff(_activeBuffs[i]);
                        }
                    }
                }
            }

            buff.Owner = _owner;
            _activeBuffs.Add(buff);
            buff.OnApplied();
            NotifyStatusChanged(buff.Config);
            BuffChanged?.Invoke(new BuffChangedEventArgs(BuffChangeType.Added, buff));
            LogBuff(buff.Source, _owner, buff.BuffName, GetDisplayedAmount(buff));
        }

        /// <summary>
        /// Removes a specific buff from the owner unit.
        /// </summary>
        /// <param name="buff">The buff to remove.</param>
        public void RemoveBuff(Buff buff)
        {
            if (buff == null || !_activeBuffs.Contains(buff))
            {
                return;
            }

            _activeBuffs.Remove(buff);
            buff.OnRemoved();
            NotifyStatusChanged(buff.Config);
            BuffChanged?.Invoke(new BuffChangedEventArgs(BuffChangeType.Removed, buff));
            LogBuff(buff.Source, _owner, buff.BuffName, 0);
        }

        private static void LogBuff(IUnit source, IUnit target, string buffName, int duration)
        {
            if (!TBattleLog.IsBattleActive)
                return;

            TBattleLog.Log(new BuffLogData
            {
                Source = GetUnitName(source),
                Target = GetUnitName(target),
                BuffName = buffName,
                Duration = duration
            });
        }

        private static string GetUnitName(IUnit unit)
        {
            if (unit is INamedUnit named && !string.IsNullOrWhiteSpace(named.UnitName))
                return named.UnitName;

            return unit == null ? "Unknown" : $"Unit_{unit.UnitID}";
        }

        /// <summary>
        /// Called at the start of the owner's turn. Triggers OnTurnStart for all active buffs.
        /// </summary>
        /// <param name="gridController">The grid controller.</param>
        public void OnTurnStart(IGridController gridController)
        {
            foreach (var buff in new List<Buff>(_activeBuffs))
            {
                buff.OnTurnStart(gridController);
                if (buff.IsExpired)
                {
                    RemoveBuff(buff);
                }
                else if (buff.Config.EffectType == BuffEffectType.Burning)
                {
                    BuffChanged?.Invoke(new BuffChangedEventArgs(BuffChangeType.TurnChanged, buff));
                }
            }
        }

        /// <summary>
        /// Called at the end of the owner's turn. Decrements buff durations and removes expired buffs.
        /// </summary>
        /// <param name="gridController">The grid controller.</param>
        public void OnTurnEnd(IGridController gridController)
        {
            var expiredBuffs = new List<Buff>();

            foreach (var buff in _activeBuffs)
            {
                buff.OnTurnEnd(gridController);
                if (buff.IsExpired)
                {
                    expiredBuffs.Add(buff);
                }
                else
                {
                    BuffChanged?.Invoke(new BuffChangedEventArgs(BuffChangeType.TurnChanged, buff));
                }
            }

            foreach (var buff in expiredBuffs)
            {
                RemoveBuff(buff);
            }
        }

        /// <summary>
        /// Removes all active buffs when the unit is destroyed.
        /// </summary>
        public void OnUnitDestroyed()
        {
            foreach (var buff in new List<Buff>(_activeBuffs))
            {
                buff.OnRemoved();
                BuffChanged?.Invoke(new BuffChangedEventArgs(BuffChangeType.Removed, buff));
                LogBuff(buff.Source, _owner, buff.BuffName, 0);
            }

            _activeBuffs.Clear();
            _owner.RefreshDerivedStats();
        }

        /// <summary>
        /// Returns whether the unit can act (all buffs allow action).
        /// </summary>
        public bool CanAct
        {
            get
            {
                foreach (var buff in _activeBuffs)
                    if (!buff.CanAct) return false;
                return true;
            }
        }

        /// <summary>
        /// Invokes OnBeforeAttacked on all active buffs.
        /// </summary>
        public void OnBeforeAttacked(IUnit attacker, ref float damage, ref bool isCritical)
        {
            foreach (var buff in new List<Buff>(_activeBuffs))
                buff.OnBeforeAttacked(attacker, ref damage, ref isCritical);
        }

        /// <summary>
        /// Invokes OnDamageTaken on all active buffs.
        /// </summary>
        public void OnDamageTaken(IUnit attacker, float damage)
        {
            foreach (var buff in new List<Buff>(_activeBuffs))
                buff.OnDamageTaken(attacker, damage);
        }

        /// <summary>
        /// Checks if the unit has a buff with the given effect type.
        /// </summary>
        public bool HasBuff(BuffEffectType effectType)
        {
            foreach (var buff in _activeBuffs)
            {
                if (buff.Config.EffectType == effectType) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns a read-only list of all active buffs.
        /// </summary>
        public IReadOnlyList<Buff> GetActiveBuffs()
        {
            return _activeBuffs.AsReadOnly();
        }

        public float SpeedModifier => _activeBuffs
            .Where(buff => buff?.Config != null)
            .Sum(buff => buff.Config.EffectType == BuffEffectType.Slow
                ? -2f
                : buff.Config.SpeedModifier);

        private static bool IsSameRuntimeStatus(Buff existing, Buff incoming)
        {
            if (existing?.Config == null || incoming?.Config == null)
                return false;
            if (existing.Config == incoming.Config)
                return true;

            return existing.Config.EffectType == incoming.Config.EffectType &&
                   existing.Config.EffectType is BuffEffectType.Burning
                       or BuffEffectType.Poison
                       or BuffEffectType.Slow
                       or BuffEffectType.Stun;
        }

        private static BuffRefreshStrategy ResolveRefreshStrategy(BuffConfig config)
        {
            return config.EffectType switch
            {
                BuffEffectType.Burning => BuffRefreshStrategy.AddStacks,
                BuffEffectType.Poison => BuffRefreshStrategy.AddDuration,
                BuffEffectType.Slow => BuffRefreshStrategy.RefreshDuration,
                BuffEffectType.Stun => BuffRefreshStrategy.RefreshDuration,
                _ => config.RefreshStrategy
            };
        }

        private static int GetDisplayedAmount(Buff buff)
        {
            return buff.Config.EffectType == BuffEffectType.Burning
                ? buff.StackCount
                : buff.RemainingTurns;
        }

        private void NotifyStatusChanged(BuffConfig config)
        {
            if (config == null || config.EffectType != BuffEffectType.Slow)
                return;

            _owner.RefreshDerivedStats();
            BattleInitiativeService.For(_gridController)?.NotifyInitiativeChanged(_owner);
        }
    }
}
