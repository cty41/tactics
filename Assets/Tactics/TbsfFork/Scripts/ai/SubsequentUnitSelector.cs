using System;
using System.Collections.Generic;
using Tactics.Tbsf.Common.AI;
using Tactics.Tbsf.Common.Controllers;
using Tactics.Tbsf.Common.Units;

namespace Tactics.Tbsf.Unity.AI
{
    /// <summary>
    /// A concrete implementation of <see cref="UnityUnitSelector"/> that uses the <see cref="SubsequentUnitSelectorImpl"/>
    /// to select units in sequence.
    /// </summary>
    public class SubsequentUnitSelector : UnityUnitSelector
    {
        private readonly SubsequentUnitSelectorImpl _unitSelector = new SubsequentUnitSelectorImpl();

        public override IEnumerable<IUnit> SelectNext(Func<IEnumerable<IUnit>> getUnits, GridController gridController)
        {
            return _unitSelector.SelectNext(getUnits, gridController);
        }
    }
}