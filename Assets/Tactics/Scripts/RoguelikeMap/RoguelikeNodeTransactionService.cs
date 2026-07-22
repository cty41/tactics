using System;
using Tactics.Roguelike;
using Tactics.RoguelikeMap.Events;
using Tactics.RoguelikeMap.Interaction;
using Tactics.Roster;
using Tactics.Runtime.Utilities;

namespace Tactics.RoguelikeMap
{
    /// <summary>
    /// Coordinates persisted node state and the adventure-state idempotency ledger.
    /// </summary>
    public static class RoguelikeNodeTransactionService
    {
        public static RoguelikeNodeTransaction Begin(
            RoguelikeMapNode node,
            global::Tactics.RoguelikeMap.RoguelikeMap map)
        {
            if (node == null)
                return null;

            if (node.Transaction == null ||
                node.Transaction.Phase == RoguelikeNodeTransactionPhase.None)
            {
                node.Transaction = new RoguelikeNodeTransaction
                {
                    TransactionKey = BuildKey(node.nodeId, node.nodeType.ToString()),
                    NodeType = node.nodeType.ToString(),
                    Phase = RoguelikeNodeTransactionPhase.Entered
                };
                SaveMap(map);
            }

            return node.Transaction;
        }

        public static void ResolveEvent(
            RoguelikeMapNode node,
            global::Tactics.RoguelikeMap.RoguelikeMap map,
            string eventId,
            EventOption option,
            bool succeeded,
            CharacterDefinition adjudicator,
            int attributeValue,
            int successRate,
            int roll,
            EventResult result,
            string resultText)
        {
            var transaction = Begin(node, map);
            if (transaction == null || transaction.Phase >= RoguelikeNodeTransactionPhase.Resolved)
                return;

            transaction.EventId = eventId;
            transaction.OptionId = option?.stableOptionId;
            transaction.Succeeded = succeeded;
            transaction.AdjudicatorCharacterId = adjudicator?.Id;
            transaction.AttributeValue = attributeValue;
            transaction.SuccessRate = successRate;
            transaction.Roll = roll;
            transaction.ResultText = resultText ?? string.Empty;
            transaction.Effect = RoguelikeEventEffectSnapshot.FromResult(result);
            transaction.Phase = RoguelikeNodeTransactionPhase.Resolved;
            SaveMap(map);
        }

        public static RewardResult EnsureResolvedEventApplied(
            RoguelikeMapNode node,
            global::Tactics.RoguelikeMap.RoguelikeMap map,
            EventEffectContext context)
        {
            var transaction = node?.Transaction;
            if (transaction?.Phase < RoguelikeNodeTransactionPhase.Resolved || transaction.Effect == null)
                return RewardResult.Empty();

            if (context != null && !string.IsNullOrWhiteSpace(transaction.AdjudicatorCharacterId))
                context.SelfCharacterId = transaction.AdjudicatorCharacterId;

            EventResult eventResult = transaction.Effect.ToEventResult();
            RewardResult rewardResult = eventResult.ToRewardResult(context);
            if (TryApplyOnce(context?.AdventureState, transaction.TransactionKey, rewardResult))
                TLog.Info($"[NodeTransaction] Applied event reward: {transaction.TransactionKey}");

            if (!transaction.RewardApplied)
            {
                transaction.RewardApplied = true;
                SaveMap(map);
            }

            return rewardResult;
        }

        public static bool TryApplyOnce(PlayerAdventureState state, string key, RewardResult rewardResult)
        {
            if (state == null || string.IsNullOrWhiteSpace(key) || rewardResult == null)
                return false;

            state.AppliedNodeTransactionKeys ??= new System.Collections.Generic.List<string>();
            if (state.AppliedNodeTransactionKeys.Contains(key))
                return false;

            rewardResult.ApplyToState(state);
            state.AppliedNodeTransactionKeys.Add(key);
            PlayerAdventureStateStore.Save(state);
            return true;
        }

        public static void MarkResolved(
            RoguelikeMapNode node,
            global::Tactics.RoguelikeMap.RoguelikeMap map,
            string resultText)
        {
            var transaction = Begin(node, map);
            if (transaction == null || transaction.Phase >= RoguelikeNodeTransactionPhase.Resolved)
                return;

            transaction.ResultText = resultText ?? string.Empty;
            transaction.Phase = RoguelikeNodeTransactionPhase.Resolved;
            SaveMap(map);
        }

        public static void Commit(
            RoguelikeMapNode node,
            global::Tactics.RoguelikeMap.RoguelikeMap map,
            bool consumeNode)
        {
            var transaction = Begin(node, map);
            if (transaction == null)
                return;

            transaction.Phase = RoguelikeNodeTransactionPhase.Committed;
            if (consumeNode)
                node.IsConsumed = true;
            SaveMap(map);
        }

        public static string BuildActionKey(RoguelikeMapNode node, string actionId)
        {
            return BuildKey(node?.nodeId, actionId);
        }

        public static bool WasApplied(PlayerAdventureState state, string key)
        {
            return state?.AppliedNodeTransactionKeys?.Contains(key) == true;
        }

        private static string BuildKey(string nodeId, string actionId)
        {
            return $"node:{nodeId ?? "unknown"}:{actionId ?? "interaction"}";
        }

        private static void SaveMap(global::Tactics.RoguelikeMap.RoguelikeMap map)
        {
            if (map != null)
                PureRunSessionStore.SaveMap(map);
        }
    }
}
