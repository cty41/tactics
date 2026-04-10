namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Cells
{
    /// <summary>
    /// Represents a cell that affects unit's defence.
    /// </summary>
    public interface IDefenceAffectingCell
    {
        public int DefenceModifier { get; }
    }
}