using System;
using Tactics.Common.Controllers;

namespace Tactics.Common.Units.Buffs
{
    /// <summary>
    /// Abstract base class for buffs/status effects applied to units.
    /// Buffs are managed by BuffComponent, not singletons.
    /// </summary>
    public abstract class Buff
    {
        /// <summary>
        /// Display name of the buff.
        /// </summary>
        public abstract string BuffName { get; }

        /// <summary>
        /// The unit that owns this buff. Set by BuffComponent when added.
        /// </summary>
        public IUnit Owner { get; internal set; }

        /// <summary>
        /// The unit that applied this buff.
        /// </summary>
        public IUnit Source { get; }

        /// <summary>
        /// Number of turns remaining before this buff expires.
        /// </summary>
        public int RemainingTurns { get; set; }

        /// <summary>
        /// Returns true if the buff has expired.
        /// </summary>
        public bool IsExpired => RemainingTurns <= 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="Buff"/> class.
        /// </summary>
        /// <param name="source">The unit that applied the buff.</param>
        /// <param name="duration">Number of turns the buff lasts.</param>
        protected Buff(IUnit source, int duration)
        {
            Source = source;
            RemainingTurns = duration;
        }

        /// <summary>
        /// Called when the buff is first applied to a unit.
        /// </summary>
        public virtual void OnApplied() { }

        /// <summary>
        /// Called at the start of the owner's turn. Used for damage-over-time effects.
        /// </summary>
        /// <param name="gridController">The grid controller.</param>
        public virtual void OnTurnStart(IGridController gridController) { }

        /// <summary>
        /// Called at the end of the owner's turn. Decrements remaining turns.
        /// </summary>
        /// <param name="gridController">The grid controller.</param>
        public virtual void OnTurnEnd(IGridController gridController)
        {
            RemainingTurns--;
        }

        /// <summary>
        /// Called when the buff is removed or expires.
        /// </summary>
        public virtual void OnRemoved() { }
    }
}
