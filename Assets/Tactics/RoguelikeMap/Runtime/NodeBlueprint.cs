using UnityEngine;

namespace Map
{
    public enum NodeType
    {
        MinorEnemy,
        EliteEnemy,
        RestSite,
        Treasure,
        Store,
        Boss,
        Mystery
    }
}

namespace Map
{
    [CreateAssetMenu]
    public class NodeBlueprint : ScriptableObject
    {
        public Sprite sprite;
        public NodeType nodeType;

        [Header("Optional roguelike payload (stubs / future systems)")]
        [Tooltip("For Mystery / custom event nodes.")]
        public string eventId;
        [Tooltip("For Store nodes — catalog or config id.")]
        public string shopId;
        [Tooltip("For Treasure nodes — loot table or reward id.")]
        public string treasureId;
    }
}