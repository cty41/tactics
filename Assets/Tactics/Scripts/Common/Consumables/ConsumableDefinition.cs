using System;
using System.Collections.Generic;

namespace Tactics.Consumables
{
    public enum ConsumableRarity
    {
        Common,
        Uncommon,
        Rare
    }

    public enum ConsumableTargetMode
    {
        Self,
        AllyIncludingSelf
    }

    /// <summary>
    /// Immutable content definition for a battle-only consumable.
    /// </summary>
    /// <remarks>
    /// The item owns acquisition and durability data. AbilityTemplateId selects a
    /// SkillGraph template, so targeting and effect execution stay in the ability runtime.
    /// </remarks>
    [Serializable]
    public sealed class ConsumableDefinition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public ConsumableRarity Rarity;
        public int Price;
        public int MaxCharges = 1;
        public string AbilityTemplateId;
        public float Magnitude;
        public int MaxRange;
        public ConsumableTargetMode TargetMode;
    }

    /// <summary>
    /// One independently persisted consumable copy in the current run.
    /// </summary>
    [Serializable]
    public sealed class ConsumableInstance
    {
        public string InstanceId;
        public string DefinitionId;
        public int RemainingCharges;
        public int MaxCharges;

        public static ConsumableInstance Create(ConsumableDefinition definition, string instanceId = null)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            int maxCharges = Math.Max(1, definition.MaxCharges);
            return new ConsumableInstance
            {
                InstanceId = string.IsNullOrWhiteSpace(instanceId)
                    ? Guid.NewGuid().ToString("N")
                    : instanceId,
                DefinitionId = definition.Id,
                RemainingCharges = maxCharges,
                MaxCharges = maxCharges
            };
        }
    }

    [Serializable]
    public sealed class WeightedConsumableEntry
    {
        public string ConsumableId;
        public float Weight = 1f;
    }

    [Serializable]
    public sealed class ConsumablePoolDefinition
    {
        public string Id;
        public List<WeightedConsumableEntry> Entries = new List<WeightedConsumableEntry>();
    }

    [Serializable]
    public sealed class ConsumableContentFile
    {
        public List<ConsumableDefinition> Definitions = new List<ConsumableDefinition>();
        public List<ConsumablePoolDefinition> Pools = new List<ConsumablePoolDefinition>();
    }
}
