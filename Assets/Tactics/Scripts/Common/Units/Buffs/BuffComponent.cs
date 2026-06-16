using System;
using System.Collections.Generic;
using Tactics.Common.Controllers;

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

        /// <summary>
        /// Initializes a new instance of the <see cref="BuffComponent"/> class.
        /// </summary>
        /// <param name="owner">The unit this component manages buffs for.</param>
        public BuffComponent(IUnit owner)
        {
            _owner = owner;
            _activeBuffs = new List<Buff>();
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

            // Check uniqueness: if IsUnique is true, don't add if same config already exists
            if (buff.Config != null && buff.Config.IsUnique)
            {
                foreach (var existingBuff in _activeBuffs)
                {
                    if (existingBuff.Config == buff.Config)
                    {
                        return; // Skip adding duplicate unique buff
                    }
                }
            }

            buff.Owner = _owner;
            _activeBuffs.Add(buff);
            buff.OnApplied();
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
            }

            _activeBuffs.Clear();
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
    }
}
