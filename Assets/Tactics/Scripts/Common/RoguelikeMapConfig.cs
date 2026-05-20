using System.Collections.Generic;
using UnityEngine;

namespace Tactics.RoguelikeMap
{
    [CreateAssetMenu]
    public class RoguelikeMapConfig : ScriptableObject
    {
        public List<RoguelikeNodeBlueprint> nodeBlueprints;

        [Tooltip("Nodes that will be used during random node assignment")]
        public List<RoguelikeNodeType> randomNodes = new List<RoguelikeNodeType>
            {RoguelikeNodeType.Mystery, RoguelikeNodeType.Store, RoguelikeNodeType.Treasure, RoguelikeNodeType.MinorEnemy, RoguelikeNodeType.RestSite};

        [Tooltip("Number of grid columns")]
        public int gridColumns = 5;

        [Tooltip("Number of grid rows")]
        public int gridRows = 4;

        [Tooltip("Total number of nodes to generate on the map (gridColumns * gridRows)")]
        public int nodeCount => gridColumns * gridRows;

        [Tooltip("Maximum distance a node can reach/connect to another node")]
        public float maxReachableDistance = 3.0f;

        [Tooltip("Vision range for map exploration (how far the player can see)")]
        public float visionRange = 5.0f;

        [Tooltip("Minimum distance between any two nodes")]
        public float minDistanceBetweenNodes = 1.0f;

        [Tooltip("Minimum distance between store nodes")]
        public float storeMinDistance = 2.0f;

        [Tooltip("Increase this number to generate more paths")]
        public int extraPaths;

        [Tooltip("事件JSON文件列表，用于加载区域事件")]
        public List<TextAsset> eventFiles = new List<TextAsset>();
    }
}