using Sirenix.OdinInspector;
using UnityEngine;

namespace Tactics.Common.Units.Buffs
{
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

        public string BuffName => _buffName;
        public Sprite Icon => _icon;
        public int DefaultDuration => _defaultDuration;
        public bool CanAct => _canAct;
        public BuffEffectType EffectType => _effectType;
        public BuffTriggerTiming TriggerTiming => _triggerTiming;
        public string CurseCategory => _curseCategory;
        public float DamagePerTurn => _damagePerTurn;
        public ElementType ElementType => _elementType;
    }
}
