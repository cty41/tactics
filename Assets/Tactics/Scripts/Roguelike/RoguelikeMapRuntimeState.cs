using System.Collections.Generic;
using System.Linq;
using Tactics.RoguelikeMap;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Roguelike
{
    /// <summary>
    /// In-memory runtime state for the current roguelike run.
    /// This is the source of truth for map progress during the current play session.
    /// </summary>
    public static class RoguelikeMapRuntimeState
    {
        public static global::Tactics.RoguelikeMap.RoguelikeMap CurrentMap { get; private set; }
        public static string CurrentNodeId { get; private set; }
        public static string PendingBattleNodeId { get; private set; }
        public static string ReturnSceneName { get; private set; } = "Home";
        public static bool ShouldResumeMapOnHome { get; private set; }
        public static List<string> VisitedPathNodeIds { get; } = new List<string>();

        public static bool HasActiveRun => CurrentMap != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ClearAll();
        }

        public static void AttachMap(global::Tactics.RoguelikeMap.RoguelikeMap map, string currentNodeId = null)
        {
            if (map == null)
            {
                ClearAll();
                return;
            }

            bool mapChanged = !ReferenceEquals(CurrentMap, map);
            CurrentMap = map;

            string resolvedNodeId = ResolveCurrentNodeId(map, currentNodeId)
                                    ?? ResolveCurrentNodeId(map, CurrentNodeId)
                                    ?? ResolveSingleVisitedNodeId(map);

            CurrentNodeId = resolvedNodeId;

            if (!mapChanged)
            {
                if (VisitedPathNodeIds.Count == 0 && !string.IsNullOrEmpty(resolvedNodeId))
                    VisitedPathNodeIds.Add(resolvedNodeId);
                return;
            }

            VisitedPathNodeIds.Clear();
            if (!string.IsNullOrEmpty(resolvedNodeId))
                VisitedPathNodeIds.Add(resolvedNodeId);
        }

        public static void BeginBattleFromNode(global::Tactics.RoguelikeMap.RoguelikeMap map, string nodeId, string returnSceneName = "Home")
        {
            AttachMap(map);

            if (string.IsNullOrEmpty(nodeId))
                return;

            PendingBattleNodeId = nodeId;
            ReturnSceneName = string.IsNullOrWhiteSpace(returnSceneName) ? "Home" : returnSceneName;
        }

        public static bool CommitNodeProgress(string nodeId)
        {
            if (CurrentMap == null || string.IsNullOrEmpty(nodeId))
                return false;

            var node = CurrentMap.GetNode(nodeId);
            if (node == null)
            {
                TLog.Warning($"[RoguelikeMapRuntimeState] Cannot commit missing node: {nodeId}");
                return false;
            }

            CurrentMap.visitedNodes.Add(nodeId);
            node.VisitState = NodeVisitState.Visited;
            node.IsReachable = false;
            CurrentNodeId = nodeId;

            if (VisitedPathNodeIds.Count == 0 || VisitedPathNodeIds[^1] != nodeId)
                VisitedPathNodeIds.Add(nodeId);

            return true;
        }

        public static bool TryCommitPendingBattleVictory()
        {
            if (CurrentMap == null || string.IsNullOrEmpty(PendingBattleNodeId))
                return false;

            bool committed = CommitNodeProgress(PendingBattleNodeId);
            PendingBattleNodeId = null;

            if (committed)
                ShouldResumeMapOnHome = true;

            return committed;
        }

        public static void ClearPendingBattle()
        {
            PendingBattleNodeId = null;
        }

        public static void MarkResumeMapOnHome()
        {
            ShouldResumeMapOnHome = true;
        }

        public static bool ConsumeResumeMapOnHomeFlag()
        {
            bool shouldResume = ShouldResumeMapOnHome;
            ShouldResumeMapOnHome = false;
            return shouldResume;
        }

        public static void ClearAll()
        {
            CurrentMap = null;
            CurrentNodeId = null;
            PendingBattleNodeId = null;
            ReturnSceneName = "Home";
            ShouldResumeMapOnHome = false;
            VisitedPathNodeIds.Clear();
        }

        private static string ResolveCurrentNodeId(global::Tactics.RoguelikeMap.RoguelikeMap map, string nodeId)
        {
            if (map == null || string.IsNullOrEmpty(nodeId))
                return null;

            return map.GetNode(nodeId) != null ? nodeId : null;
        }

        private static string ResolveSingleVisitedNodeId(global::Tactics.RoguelikeMap.RoguelikeMap map)
        {
            if (map?.visitedNodes == null || map.visitedNodes.Count != 1)
                return null;

            return map.visitedNodes.FirstOrDefault();
        }
    }
}
