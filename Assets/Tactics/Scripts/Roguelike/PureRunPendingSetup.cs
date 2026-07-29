using Tactics.Roster;
using UnityEngine;

namespace Tactics.Roguelike
{
    /// <summary>
    /// In-memory handoff for a Pure Run state customized before map generation
    /// (e.g. starting-skill selection). Never persisted: abandoning the flow
    /// mid-way simply discards the pending state. Consumed exactly once by
    /// RoguelikeMapUIController.GenerateNewMap.
    /// </summary>
    public static class PureRunPendingSetup
    {
        public static PlayerAdventureState PendingState { get; private set; }

        public static void SetPending(PlayerAdventureState state) => PendingState = state;

        public static void Clear() => PendingState = null;

        /// <summary>Returns the pending state and clears it (single consume).</summary>
        public static PlayerAdventureState Consume()
        {
            var state = PendingState;
            PendingState = null;
            return state;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PendingState = null;
        }
    }
}
