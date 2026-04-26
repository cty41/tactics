using System.Collections.Generic;
using Sirenix.OdinInspector;
using Tactics.Common.Units.Abilities;
using UnityEngine;

namespace Tactics.Common.Units.Classes
{
    [CreateAssetMenu(menuName = "Game/Units/Role Config")]
    public class RoleConfig : ScriptableObject
    {
        [BoxGroup("Basic Info")]
        [SerializeField] private string _displayName;

        [BoxGroup("Basic Info")]
        [SerializeField] private Sprite _icon;

        [BoxGroup("Basic Info")]
        [SerializeField] private RoleType _roleType;

        [BoxGroup("Abilities")]
        [SerializeField]
        [ListDrawerSettings(DraggableItems = true, Expanded = true, ShowPaging = false)]
        private List<AbilityConfig> _abilities = new List<AbilityConfig>();

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public RoleType RoleType => _roleType;
        public IReadOnlyList<AbilityConfig> Abilities => _abilities;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_abilities == null)
            {
                _abilities = new List<AbilityConfig>();
            }
        }
#endif
    }
}
