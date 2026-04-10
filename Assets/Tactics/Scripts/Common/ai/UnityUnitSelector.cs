using System;
using System.Collections.Generic;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using UnityEngine;

namespace Tactics.Common.AI
{
    public abstract class UnityUnitSelector : MonoBehaviour, IUnitSelector
    {
        public abstract IEnumerable<IUnit> SelectNext(Func<IEnumerable<IUnit>> getUnits, GridController gridController);
    }
}
