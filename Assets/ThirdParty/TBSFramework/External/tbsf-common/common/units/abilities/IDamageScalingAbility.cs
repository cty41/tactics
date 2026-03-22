namespace TurnBasedStrategyFramework.Common.Units.Abilities
{
    public interface IDamageScalingAbility
    {
        bool IsRangedDamage { get; }
        bool HasHalfScaling { get; }
    }
}
