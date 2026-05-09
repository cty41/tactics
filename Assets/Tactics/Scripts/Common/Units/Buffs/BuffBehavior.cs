using Tactics.Common.Controllers;
using Tactics.Runtime.BattleLog;
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

        public virtual void OnDamageTaken(Buff buff, IUnit attacker, float damage)
        {
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
                buff.Owner, attacker, baseDamage, false, _config.ElementType,
                canTriggerBeforeAttacked: true, canCrit: true, canTriggerDamageTaken: false);
        }

        public virtual void OnTurnStart(Buff buff, IGridController gridController)
        {
            if (_config.TriggerTiming != BuffTriggerTiming.TurnStart)
                return;

            if (buff.Owner == null) return;
            if (_config.DamagePerTurn <= 0) return;

            CombatComponent.ApplyDamage(
                buff.Source, buff.Owner, _config.DamagePerTurn, false, _config.ElementType,
                canTriggerBeforeAttacked: false, canCrit: false, canTriggerDamageTaken: false);

            string ownerName = buff.Owner is INamedUnit named ? named.UnitName : buff.Owner.ToString();

            TBattleLog.Log(new DamageLogData
            {
                Source = buff.BuffName,
                Target = ownerName,
                Damage = _config.DamagePerTurn,
                RemainingHealth = buff.Owner.Health
            });
        }

        public virtual void OnTurnEnd(Buff buff, IGridController gridController) { }

        public virtual void OnRemoved(Buff buff) { }

        public virtual bool CanAct => _config.CanAct;
    }
}
