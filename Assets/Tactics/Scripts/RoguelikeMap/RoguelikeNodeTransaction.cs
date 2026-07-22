using System;
using System.Collections.Generic;
using Tactics.RoguelikeMap.Events;

namespace Tactics.RoguelikeMap
{
    /// <summary>
    /// Persistent lifecycle for a non-battle node. A resolved transaction is saved before
    /// its reward is applied, so re-entry can finish the same result without rolling again.
    /// </summary>
    public enum RoguelikeNodeTransactionPhase
    {
        None,
        Entered,
        Resolved,
        Committed
    }

    [Serializable]
    public sealed class RoguelikeEventEffectSnapshot
    {
        public EventResultType Type;
        public EventTargetType Target;
        public int Amount;
        public string ItemId;
        public string ItemPoolId;
        public string Description;

        public EventResult ToEventResult()
        {
            return new EventResult
            {
                type = Type,
                target = Target,
                amount = Amount,
                itemId = ItemId,
                itemPoolId = ItemPoolId,
                description = Description
            };
        }

        public static RoguelikeEventEffectSnapshot FromResult(EventResult result)
        {
            if (result == null)
                return null;

            return new RoguelikeEventEffectSnapshot
            {
                Type = result.type,
                Target = result.target,
                Amount = result.amount,
                ItemId = result.itemId,
                ItemPoolId = result.itemPoolId,
                Description = result.description
            };
        }
    }

    [Serializable]
    public sealed class RoguelikeNodeTransaction
    {
        public string TransactionKey;
        public string NodeType;
        public RoguelikeNodeTransactionPhase Phase;
        public string EventId;
        public string OptionId;
        public bool Succeeded;
        public string AdjudicatorCharacterId;
        public int AttributeValue;
        public int SuccessRate;
        public int Roll;
        public string ResultText;
        public RoguelikeEventEffectSnapshot Effect;
        public bool RewardApplied;
    }
}
