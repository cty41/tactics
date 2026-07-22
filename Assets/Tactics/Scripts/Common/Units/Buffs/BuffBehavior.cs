using Tactics.Common.Controllers;
using UnityEngine;

namespace Tactics.Common.Units.Buffs
{
    public class BuffBehavior
    {
        private readonly BuffConfig _config;

        public BuffBehavior(BuffConfig config)
        {
            _config = config;
        }

        public virtual void OnApplied(Buff buff) { }

        public virtual void OnBeforeAttacked(Buff buff, IUnit attacker, ref float damage, ref bool isCritical)
        {
            if (_config.TriggerTiming != BuffTriggerTiming.BeforeAttacked)
                return;

            isCritical = true;
        }

        public virtual void OnDamageTaken(Buff buff, IUnit attacker, float damage, bool isRangedDamage)
        {
            if (!isRangedDamage && _config.MeleeRetaliationBuff != null && _config.MeleeRetaliationDuration > 0
                && buff.Owner != null && attacker != null && !ReferenceEquals(buff.Owner, attacker)
                && buff.Owner.CurrentCell != null && attacker.CurrentCell != null
                && buff.Owner.CurrentCell.GetDistance(attacker.CurrentCell) <= 1)
            {
                attacker.AddBuff(new Buff(_config.MeleeRetaliationBuff, buff.Owner, _config.MeleeRetaliationDuration));
            }

            if (_config.TriggerTiming != BuffTriggerTiming.DamageTaken)
                return;

            if (buff.Owner == null || attacker == null) return;
            if (ReferenceEquals(buff.Owner, attacker)) return;

            // Range check: only counter if attacker is within melee range (1 cell)
            if (buff.Owner.CurrentCell == null || attacker.CurrentCell == null) return;
            int distance = buff.Owner.CurrentCell.GetDistance(attacker.CurrentCell);
            if (distance > 1) return;

            // Perform a full melee attack
            float baseDamage = CombatComponent.CalculateBaseDamageBeforeCrit(buff.Owner, false);

            CombatComponent.ApplyDamage(
                buff.Owner, attacker, baseDamage, false, DamageCategory.Physical, _config.ElementType,
                canTriggerBeforeAttacked: true, canCrit: true, canTriggerDamageTaken: false,
                logSourceName: buff.BuffName);
        }

        public virtual void OnTurnStart(Buff buff, IGridController gridController)
        {
            if (_config.TriggerTiming != BuffTriggerTiming.TurnStart &&
                _config.EffectType is not BuffEffectType.Burning and not BuffEffectType.Poison)
                return;

            if (buff.Owner == null) return;

            float damage = _config.EffectType switch
            {
                BuffEffectType.Burning => buff.StackCount,
                BuffEffectType.Poison => 2f,
                _ => _config.DamagePerTurn
            };
            if (damage <= 0f) return;

            CombatComponent.ApplyDamage(
                buff.Source, buff.Owner, damage, false, _config.DamageCategory, _config.ElementType,
                canTriggerBeforeAttacked: false, canCrit: false, canTriggerDamageTaken: false,
                logSourceName: buff.BuffName,
                bypassDefense: true);

            if (_config.EffectType == BuffEffectType.Burning)
                buff.ReduceStack();
        }

        public virtual void OnTurnEnd(Buff buff, IGridController gridController) { }

        public virtual void OnRemoved(Buff buff) { }

        public virtual bool CanAct => _config.CanAct;
    }
}
