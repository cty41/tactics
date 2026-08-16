using Tactics.RoguelikeMap;
using Tactics.RoguelikeMap.Interaction;
using Tactics.Roster;

namespace Tactics.Roguelike
{
    /// <summary>
    /// Records committed Pure Run statistics with stable idempotency keys.
    /// </summary>
    public static class PureRunSummaryRecorder
    {
        public static bool RecordReward(PlayerAdventureState state, string transactionKey, RewardResult reward)
        {
            if (!TryBegin(state, $"reward:{transactionKey}"))
                return false;

            reward?.ApplyToSummary(state.CurrentRunSummary);
            return true;
        }

        public static bool RecordEnemiesDefeated(PlayerAdventureState state, string transactionKey, int count)
        {
            if (!TryBegin(state, $"enemies:{transactionKey}"))
                return false;

            for (int i = 0; i < System.Math.Max(0, count); i++)
                state.CurrentRunSummary.IncrementEnemiesDefeated();
            return true;
        }

        public static bool RecordNodeCompletion(
            PlayerAdventureState state,
            string nodeId,
            RoguelikeNodeType nodeType)
        {
            if (!TryBegin(state, $"node:{nodeId}"))
                return false;

            state.CurrentRunSummary.IncrementNodesVisited();
            if (nodeType == RoguelikeNodeType.Mystery)
                state.CurrentRunSummary.IncrementEventsCompleted();
            return true;
        }

        public static RunSummary CreateTerminalSnapshot(
            PlayerAdventureState state,
            RunOutcome outcome,
            bool bossDefeated)
        {
            Ensure(state);
            var snapshot = state?.CurrentRunSummary?.Clone() ?? new RunSummary();
            snapshot.SetRunOutcome(outcome);
            if (bossDefeated)
                snapshot.MarkBossDefeated();
            return snapshot;
        }

        private static bool TryBegin(PlayerAdventureState state, string key)
        {
            if (state?.IsPureRun != true || string.IsNullOrWhiteSpace(key))
                return false;

            Ensure(state);
            if (state.AppliedRunSummaryKeys.Contains(key))
                return false;

            state.AppliedRunSummaryKeys.Add(key);
            return true;
        }

        private static void Ensure(PlayerAdventureState state)
        {
            if (state == null)
                return;

            state.CurrentRunSummary ??= new RunSummary();
            state.AppliedRunSummaryKeys ??= new System.Collections.Generic.List<string>();
        }
    }
}
