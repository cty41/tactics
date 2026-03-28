using System;
using System.Collections.Generic;
using Tactics.Tbsf.Common.AI;
using Tactics.Tbsf.Common.Controllers;
using Tactics.Tbsf.Common.Units;
using UnityEngine;

namespace Tactics.Tbsf.Unity.AI
{
    public abstract class UnityUnitSelector : MonoBehaviour, IUnitSelector
    {
        public abstract IEnumerable<IUnit> SelectNext(Func<IEnumerable<IUnit>> getUnits, GridController gridController);
    }
}