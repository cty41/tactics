using UnityEngine;

namespace Tactics.RoguelikeMap
{
    public enum RoguelikeNodeType
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

namespace Tactics.RoguelikeMap
{
    [CreateAssetMenu]
    public class RoguelikeNodeBlueprint : ScriptableObject
    {
        public Sprite sprite;
        public RoguelikeNodeType nodeType;

        [Header("Optional roguelike payload (stubs / future systems)")]
        [Tooltip("For Mystery / custom event nodes.")]
        public string eventId;
        [Tooltip("For Store nodes — catalog or config id.")]
        public string shopId;
        [Tooltip("For Treasure nodes — loot table or reward id.")]
        public string treasureId;
    }
}