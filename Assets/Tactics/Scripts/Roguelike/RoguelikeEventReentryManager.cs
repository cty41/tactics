using UnityEngine;
using Tactics.Runtime.Utilities;

namespace Tactics.Roguelike
{
    public static class RoguelikeEventReentryManager
    {
        public const string EventInProgressKey = "Tactics_EventInProgress";
        public const string EventNodeKey = "Tactics_EventNode";

        /// <summary>
        /// Mark that a roguelike event node is currently in progress.
        /// Call this when entering a node (battle, rest, store, treasure, mystery).
        /// </summary>
        public static void MarkEventInProgress(string eventType, string nodeId)
        {
            PlayerPrefs.SetString(EventInProgressKey, eventType);
            PlayerPrefs.SetString(EventNodeKey, nodeId ?? string.Empty);
            PlayerPrefs.Save();
            TLog.Info($"[RoguelikeEventReentryManager] Marked event in progress: type={eventType}, nodeId={nodeId}");
        }

        /// <summary>
        /// Clear the event in progress markers.
        /// Call this when the event node is fully completed.
        /// </summary>
        public static void ClearEventInProgress()
        {
            string prevType = PlayerPrefs.GetString(EventInProgressKey, "null");
            string prevNode = PlayerPrefs.GetString(EventNodeKey, "null");
            PlayerPrefs.DeleteKey(EventInProgressKey);
            PlayerPrefs.DeleteKey(EventNodeKey);
            PlayerPrefs.Save();
            TLog.Info($"[RoguelikeEventReentryManager] Cleared event in progress: was type={prevType}, node={prevNode}");
        }

        /// <summary>
        /// Check if there's an event in progress.
        /// </summary>
        /// <param name="eventType">Output: the event type string (e.g. "Battle", "Rest")</param>
        /// <param name="nodeId">Output: the node ID. null if not set.</param>
        /// <returns>true if an event is in progress</returns>
        public static bool IsEventInProgress(out string eventType, out string nodeId)
        {
            eventType = PlayerPrefs.GetString(EventInProgressKey, null);
            nodeId = null;

            if (string.IsNullOrEmpty(eventType))
                return false;

            string nodeStr = PlayerPrefs.GetString(EventNodeKey, null);
            if (!string.IsNullOrEmpty(nodeStr))
            {
                nodeId = nodeStr;
            }

            return true;
        }
    }
}
