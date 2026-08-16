namespace Tactics.Common.Units.Buffs
{
    /// <summary>
    /// Defines how applying the same BuffConfig updates its existing runtime instance.
    /// </summary>
    public enum BuffRefreshStrategy
    {
        AddDuration,
        RefreshDuration,
        AddStacks
    }
}
