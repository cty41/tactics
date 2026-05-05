using System;
using Tactics.Common.Controllers;
using Tactics.Runtime.BattleLog;
using UnityEngine;

namespace Tactics.Common.Units.Buffs
{
    [Serializable]
    public class DoTBehavior : BuffBehavior
    {
        [SerializeField] private float _damagePerTurn = 5f;

        public float DamagePerTurn => _damagePerTurn;

        public override void OnTurnStart(Buff buff, IGridController gridController)
        {
            if (buff.Owner == null) return;

            buff.Owner.ModifyHealth(-_damagePerTurn, buff.Source);

            string ownerName = buff.Owner is INamedUnit named ? named.UnitName : buff.Owner.ToString();

            BattleLogger.Log(new DamageLogData
            {
                Source = buff.BuffName,
                Target = ownerName,
                Damage = _damagePerTurn,
                RemainingHealth = buff.Owner.Health
            });
        }
    }
}
