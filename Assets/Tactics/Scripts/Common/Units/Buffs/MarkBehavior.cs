using System;
using Tactics.Common.Controllers;

namespace Tactics.Common.Units.Buffs
{
    [Serializable]
    public class MarkBehavior : BuffBehavior
    {
        public override void OnBeforeAttacked(Buff buff, IUnit attacker, ref float damage, ref bool isCritical)
        {
            isCritical = true;
        }
    }
}
