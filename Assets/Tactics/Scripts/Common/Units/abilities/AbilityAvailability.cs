using Tactics.Common.Controllers;

namespace Tactics.Common.Units.Abilities
{
    public enum AbilityAvailabilityState
    {
        Enabled,
        DisabledClickable,
        Hidden
    }

    public readonly struct AbilityAvailability
    {
        public AbilityAvailabilityState State { get; }
        public string Reason { get; }
        public bool CanExecute => State == AbilityAvailabilityState.Enabled;

        public AbilityAvailability(AbilityAvailabilityState state, string reason = null)
        {
            State = state;
            Reason = reason ?? string.Empty;
        }

        public static AbilityAvailability Enabled() => new(AbilityAvailabilityState.Enabled);
        public static AbilityAvailability Disabled(string reason) =>
            new(AbilityAvailabilityState.DisabledClickable, reason);
        public static AbilityAvailability Hidden(string reason = null) =>
            new(AbilityAvailabilityState.Hidden, reason);
    }

    public interface IAbilityAvailabilityProvider
    {
        AbilityAvailability GetAvailability(IGridController gridController);
    }

    public static class AbilityAvailabilityResolver
    {
        public static AbilityAvailability Resolve(IAbility ability, IGridController gridController)
        {
            if (ability == null)
                return AbilityAvailability.Hidden("技能不存在");
            if (ability is IAbilityAvailabilityProvider provider)
                return provider.GetAvailability(gridController);
            return ability.CanPerform(gridController)
                ? AbilityAvailability.Enabled()
                : AbilityAvailability.Disabled("当前无法使用");
        }
    }
}
