using System;
using UnityEngine;

namespace Tactics.Common.Units.Buffs
{
    [Serializable]
    public class FrozenBehavior : BuffBehavior
    {
        public override bool CanAct => false;
    }
}
