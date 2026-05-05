using System;
using Tactics.Common.Controllers;
using UnityEngine;

namespace Tactics.Common.Units.Buffs
{
    [Serializable]
    public class CounterBehavior : BuffBehavior
    {
        [SerializeField] private float _counterDamageMultiplier = 0.5f;

        public float CounterDamageMultiplier => _counterDamageMultiplier;

        public override void OnDamageTaken(Buff buff, IUnit attacker, float damage)
        {
            if (buff.Owner == null || attacker == null) return;
            if (ReferenceEquals(buff.Owner, attacker)) return;

            float counterDamage = damage * _counterDamageMultiplier;
            attacker.ModifyHealth(-counterDamage, buff.Owner);
        }
    }
}
