namespace Tactics.Common.Units.Buffs
{
    /// <summary>
    /// Broad damage category. Element is tracked independently, so Magic + None is valid.
    /// </summary>
    public enum DamageCategory
    {
        Physical,
        Magic
    }

    /// <summary>
    /// Legacy name retained for source compatibility. New combat APIs use DamageCategory.
    /// </summary>
    [System.Obsolete("Use DamageCategory instead.")]
    public enum DamageType
    {
        Physical,
        Magic
    }
}
