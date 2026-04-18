using System;
using System.Collections.Generic;
using Tactics.Common.Controllers;
using Tactics.Common.Units;

namespace Tactics.Common.AI
{
    /// <summary>
    /// A concrete implementation of <see cref="IUnitSelector"/> that uses the <see cref="SubsequentUnitSelectorImpl"/>
    /// to select units in sequence.
    /// </summary>
    public class SubsequentUnitSelector : IUnitSelector
    {
        private readonly SubsequentUnitSelectorImpl _unitSelector = new SubsequentUnitSelectorImpl();

        public IEnumerable<IUnit> SelectNext(Func<IEnumerable<IUnit>> getUnits, GridController gridController)
        {
            return _unitSelector.SelectNext(getUnits, gridController);
        }
    }
}
