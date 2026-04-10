using UnityEngine;

namespace Tactics.Runtime.BattleLog
{
    /// <summary>
    /// Configuration for the battle log UI.
    /// </summary>
    [CreateAssetMenu(fileName = "BattleLogUIConfig", menuName = "Tactics/Battle Log UI Config")]
    public class BattleLogUIConfig : ScriptableObject
    {
        [Header("Display Settings")]
        [Tooltip("Maximum number of log entries to keep")]
        public int MaxEntries = 50;

        [Tooltip("Auto-scroll to newest entries")]
        public bool AutoScroll = true;

        [Tooltip("Show timestamps in log entries")]
        public bool ShowTimestamp = true;

        [Tooltip("Entry lifetime in seconds (0 = permanent)")]
        public float EntryLifetime = 0f;

        [Header("Colors")]
        [Tooltip("Color for attack logs")]
        public Color AttackColor = new Color(1f, 0.65f, 0f); // Orange

        [Tooltip("Color for critical hit logs")]
        public Color CriticalColor = new Color(1f, 0.27f, 0f); // Red-Orange

        [Tooltip("Color for damage logs")]
        public Color DamageColor = new Color(0.39f, 0.58f, 0.93f); // Cornflower Blue

        [Tooltip("Color for destroy logs")]
        public Color DestroyColor = new Color(0.55f, 0f, 0f); // Dark Red

        [Tooltip("Color for skill logs")]
        public Color SkillColor = new Color(0.58f, 0.44f, 0.86f); // Purple

        [Tooltip("Color for turn logs")]
        public Color TurnColor = new Color(1f, 0.84f, 0f); // Gold
    }
}
