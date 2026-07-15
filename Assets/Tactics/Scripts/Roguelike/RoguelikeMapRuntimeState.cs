using System.Collections.Generic;
using System.Linq;
using Tactics.RoguelikeMap;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Roguelike
{
    public readonly struct PureRunRuntimeSnapshot
    {
        public int RunSeed { get; }
        public int CurrentLayerIndex { get; }
        public int BattleVictoryCount { get; }
        public string CurrentNodeId { get; }
        public string PendingBattleNodeId { get; }

        public PureRunRuntimeSnapshot(
            int runSeed,
            int currentLayerIndex,
            int battleVictoryCount,
            string currentNodeId,
            string pendingBattleNodeId)
        {
            RunSeed = runSeed;
            CurrentLayerIndex = currentLayerIndex;
            BattleVictoryCount = battleVictoryCount;
            CurrentNodeId = currentNodeId;
            PendingBattleNodeId = pendingBattleNodeId;
        }
    }

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
        public static int RunSeed => CurrentMap?.runSeed ?? 0;
        public static int CurrentLayerIndex => CurrentMap?.GetNode(CurrentNodeId)?.LayerIndex ?? 0;
        public static int BattleVictoryCount => CurrentMap?.battleVictoryCount ?? 0;

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
                                    ?? ResolveCurrentNodeId(map, map.currentNodeId)
                                    ?? ResolveCurrentNodeId(map, CurrentNodeId)
                                    ?? ResolveSingleVisitedNodeId(map);

            CurrentNodeId = resolvedNodeId;
            if (!string.IsNullOrEmpty(resolvedNodeId))
                CurrentMap.currentNodeId = resolvedNodeId;

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

        public static bool CommitNodeProgress(string nodeId, bool isBattleVictory = false)
        {
            if (CurrentMap == null || string.IsNullOrEmpty(nodeId))
                return false;

            var node = CurrentMap.GetNode(nodeId);
            if (node == null)
            {
                TLog.Warning($"[RoguelikeMapRuntimeState] Cannot commit missing node: {nodeId}");
                return false;
            }

            CurrentMap.RecordNodeCompletion(nodeId, isBattleVictory);
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

            bool committed = CommitNodeProgress(PendingBattleNodeId, true);
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

        /// <summary>
        /// Returns a compact state snapshot for command-layer and deterministic test consumers.
        /// </summary>
        public static PureRunRuntimeSnapshot GetSnapshot()
        {
            return new PureRunRuntimeSnapshot(
                RunSeed,
                CurrentLayerIndex,
                BattleVictoryCount,
                CurrentNodeId,
                PendingBattleNodeId);
        }

        /// <summary>
        /// Derives a stable stream seed without using string.GetHashCode or Unity's global random state.
        /// </summary>
        public static int DeriveSeed(int runSeed, string streamName, int ordinal = 0)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)runSeed) * 16777619u;
                hash = (hash ^ (uint)ordinal) * 16777619u;

                if (!string.IsNullOrEmpty(streamName))
                {
                    foreach (char character in streamName)
                        hash = (hash ^ character) * 16777619u;
                }

                return (int)hash;
            }
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
