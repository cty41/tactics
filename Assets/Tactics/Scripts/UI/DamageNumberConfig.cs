using UnityEngine;

namespace Tactics.UI
{
    public enum DamageNumberType
    {
        Normal,
        Critical,
        Heal,
        Miss
    }

    [CreateAssetMenu(fileName = "DamageNumberConfig", menuName = "Tactics/Damage Number Config")]
    public sealed class DamageNumberConfig : ScriptableObject
    {
        [Header("Lifetime")]
        public float lifetime = 1.5f;
        public float fadeInDuration = 0.2f;
        public float fadeOutDuration = 0.3f;

        [Header("Movement")]
        public float moveSpeed = 60f;

        [Header("Scale")]
        public float startScale = 0.5f;
        public float peakScale = 1.2f;
        public float endScale = 1.0f;

        [Header("Style")]
        public Color textColor = Color.white;
        public int fontSize = 24;
        public string ussClassName = "damage-number-normal";
    }
}
