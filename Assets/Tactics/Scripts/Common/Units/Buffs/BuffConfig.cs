using Sirenix.OdinInspector;
using UnityEngine;
using Newtonsoft.Json;

namespace Tactics.Common.Units.Buffs
{
    /// <summary>
    /// Classifies an effect as beneficial or harmful for cleanse behavior.
    /// </summary>
    public enum BuffPolarity
    {
        Beneficial,
        Harmful
    }

    [CreateAssetMenu(menuName = "Game/Buffs/Buff Config")]
    public class BuffConfig : ScriptableObject
    {
        [BoxGroup("Basic Info")]
        [SerializeField] private string _buffName;

        [BoxGroup("Basic Info")]
        [SerializeField] private Sprite _icon;

        [BoxGroup("Basic Info")]
        [SerializeField] private int _defaultDuration = 3;

        [BoxGroup("Behavior")]
        [SerializeField] private bool _canAct = true;

        [BoxGroup("Behavior")]
        [SerializeField] private BuffPolarity _polarity = BuffPolarity.Beneficial;

        [BoxGroup("Effect")]
        [SerializeField] private BuffEffectType _effectType = BuffEffectType.None;

        [BoxGroup("Effect")]
        [SerializeField] private BuffTriggerTiming _triggerTiming = BuffTriggerTiming.None;

        [BoxGroup("Effect")]
        [SerializeField] private string _curseCategory = "";

        [BoxGroup("Effect Params")]
        [SerializeField] private float _damagePerTurn = 0f;

        [BoxGroup("Effect Params")]
        [SerializeField] private ElementType _elementType = ElementType.None;

        [BoxGroup("Effect Params")]
        [SerializeField, Range(0f, 1f)] private float _damageReductionPercent;

        [JsonIgnore]
        public string RuntimeSourceAssetPath { get; set; }

        public string BuffName => _buffName;
        public Sprite Icon => _icon;
        public int DefaultDuration => _defaultDuration;
        public bool CanAct => _canAct;
        public BuffPolarity Polarity => _polarity;
        public BuffEffectType EffectType => _effectType;
        public BuffTriggerTiming TriggerTiming => _triggerTiming;
        public string CurseCategory => _curseCategory;
        public float DamagePerTurn => _damagePerTurn;
        public ElementType ElementType => _elementType;
        public float DamageReductionPercent => _damageReductionPercent;
    }
}
