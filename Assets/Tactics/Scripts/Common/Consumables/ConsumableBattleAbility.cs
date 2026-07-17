using System;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;

namespace Tactics.Consumables
{
    /// <summary>
    /// Marks a runtime SkillGraph ability as the consumable carried by one roster character.
    /// </summary>
    public sealed class ConsumableBattleAbility : SkillGraphAbilityImpl
    {
        private readonly ConsumableUsePolicy _usePolicy;

        internal ConsumableBattleAbility(
            IUnit owner,
            SkillGraphAbilityConfig config,
            ConsumableUsePolicy usePolicy,
            ConsumableDefinition definition,
            string instanceId,
            string characterId)
            : base(owner, config, usePolicy)
        {
            _usePolicy = usePolicy;
            Definition = definition;
            InstanceId = instanceId;
            CharacterId = characterId;
            _usePolicy.UseCommitted += () => UseCommitted?.Invoke(this);
        }

        /// <summary>The immutable content definition used by the battle button.</summary>
        public ConsumableDefinition Definition { get; }

        /// <summary>The persisted consumable copy consumed by this ability.</summary>
        public string InstanceId { get; }

        /// <summary>The roster character that carried the instance into battle.</summary>
        public string CharacterId { get; }

        /// <summary>The current persisted charge count.</summary>
        public int RemainingCharges => _usePolicy.RemainingCharges;

        /// <summary>Raised after a completed graph has been committed and saved.</summary>
        public event Action<ConsumableBattleAbility> UseCommitted;
    }
}
