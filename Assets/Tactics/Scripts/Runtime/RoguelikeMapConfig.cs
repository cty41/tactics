using System.Collections.Generic;
using OneLine;
using UnityEngine;
using Tactics.Utils;

namespace Tactics.RoguelikeMap
{
    [CreateAssetMenu]
    public class RoguelikeMapConfig : ScriptableObject
    {
        public List<RoguelikeNodeBlueprint> nodeBlueprints;
        [Tooltip("Nodes that will be used on layers with Randomize Nodes > 0")]
        public List<RoguelikeNodeType> randomNodes = new List<RoguelikeNodeType>
            {RoguelikeNodeType.Mystery, RoguelikeNodeType.Store, RoguelikeNodeType.Treasure, RoguelikeNodeType.MinorEnemy, RoguelikeNodeType.RestSite};
        public int GridWidth => Mathf.Max(numOfPreBossNodes.max, numOfStartingNodes.max);

        [OneLineWithHeader]
        public IntMinMax numOfPreBossNodes;
        [OneLineWithHeader]
        public IntMinMax numOfStartingNodes;

        [Tooltip("Increase this number to generate more paths")]
        public int extraPaths;
        public List<RoguelikeMapLayer> layers;
    }
}