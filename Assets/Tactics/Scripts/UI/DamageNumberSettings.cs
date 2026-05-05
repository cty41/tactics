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

    [CreateAssetMenu(fileName = "DamageNumberSettings", menuName = "Tactics/Damage Number Settings")]
    public sealed class DamageNumberSettings : ScriptableObject
    {
        [System.Serializable]
        public struct TypeConfig
        {
            [Header("Lifetime")]
            public float lifetime;
            public float fadeInDuration;
            public float fadeOutDuration;

            [Header("Movement")]
            public float moveSpeed;

            [Header("Scale")]
            public float startScale;
            public float peakScale;
            public float endScale;

            [Header("Style")]
            public Color color;
            public int fontSize;
            public string ussClassName;
        }

        [Header("Normal")]
        public TypeConfig normal = new TypeConfig
        {
            lifetime = 1.5f,
            fadeInDuration = 0.2f,
            fadeOutDuration = 0.3f,
            moveSpeed = 60f,
            startScale = 0.5f,
            peakScale = 1.2f,
            endScale = 1.0f,
            color = Color.white,
            fontSize = 24,
            ussClassName = "damage-number-normal"
        };

        [Header("Critical")]
        public TypeConfig crit = new TypeConfig
        {
            lifetime = 1.5f,
            fadeInDuration = 0.2f,
            fadeOutDuration = 0.3f,
            moveSpeed = 60f,
            startScale = 0.5f,
            peakScale = 1.5f,
            endScale = 1.0f,
            color = new Color(1f, 0.86f, 0.2f),
            fontSize = 32,
            ussClassName = "damage-number-crit"
        };

        [Header("Heal")]
        public TypeConfig heal = new TypeConfig
        {
            lifetime = 1.5f,
            fadeInDuration = 0.2f,
            fadeOutDuration = 0.3f,
            moveSpeed = 60f,
            startScale = 0.5f,
            peakScale = 1.2f,
            endScale = 1.0f,
            color = new Color(0.31f, 1f, 0.47f),
            fontSize = 24,
            ussClassName = "damage-number-heal"
        };

        [Header("Miss")]
        public TypeConfig miss = new TypeConfig
        {
            lifetime = 1.0f,
            fadeInDuration = 0.2f,
            fadeOutDuration = 0.3f,
            moveSpeed = 60f,
            startScale = 0.5f,
            peakScale = 1.2f,
            endScale = 1.0f,
            color = new Color(0.59f, 0.59f, 0.59f),
            fontSize = 20,
            ussClassName = "damage-number-miss"
        };

        public TypeConfig GetConfig(DamageNumberType type)
        {
            return type switch
            {
                DamageNumberType.Normal => normal,
                DamageNumberType.Critical => crit,
                DamageNumberType.Heal => heal,
                DamageNumberType.Miss => miss,
                _ => normal
            };
        }
    }
}
