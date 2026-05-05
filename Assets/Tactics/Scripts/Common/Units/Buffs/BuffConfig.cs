using System.Collections.Generic;
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

        [BoxGroup("Behavior")]
        [SerializeField] private bool _isUnique = false;

        [BoxGroup("Behaviors")]
        [SerializeReference]
        [ListDrawerSettings(Expanded = true)]
        private List<BuffBehavior> _behaviors = new List<BuffBehavior>();

        public string BuffName => _buffName;
        public Sprite Icon => _icon;
        public int DefaultDuration => _defaultDuration;
        public bool CanAct => _canAct;
        public bool IsUnique => _isUnique;
        public IReadOnlyList<BuffBehavior> Behaviors => _behaviors;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_behaviors == null)
                _behaviors = new List<BuffBehavior>();
        }
#endif
    }
}
