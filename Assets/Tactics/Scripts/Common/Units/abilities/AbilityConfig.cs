using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Tactics.Common.Controllers;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// ScriptableObject container for ability configurations.
    /// Uses [SerializeReference] for polymorphic serialization of effects and targeting strategies.
    /// Enhanced with Odin Inspector for better editing experience.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Abilities/Ability Config")]
    public class AbilityConfig : ScriptableObject
    {
        [BoxGroup("Basic Info")]
        [SerializeField] private string _displayName;
        
        [BoxGroup("Basic Info")]
        [SerializeField] private Sprite _icon;
        
        [BoxGroup("Basic Info")]
        [SerializeField, TextArea(2, 4)] private string _description;

        [BoxGroup("Costs")]
        [SerializeField] private int _manaCost;
        
        [BoxGroup("Costs")]
        [SerializeField] private float _cooldown;
        
        [BoxGroup("Basic Info")]
        [Tooltip("If true, this ability can be used once per turn without consuming Mana. Examples: Move, MeleeAttack, RangedAttack.")]
        [SerializeField] private bool _isBasicAbility;

        [BoxGroup("Targeting")]
        [SerializeReference] 
        [InlineProperty]
        [HideLabel]
        private TargetingStrategy _targetingStrategy;

        [BoxGroup("Effects")]
        [SerializeReference] 
        [InlineProperty]
        [ListDrawerSettings(DraggableItems = true, Expanded = true, ShowPaging = false)]
        private List<AbilityEffect> _effects = new List<AbilityEffect>();

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public string Description => _description;
        public int ManaCost => _manaCost;
        public float Cooldown => _cooldown;
        public bool IsBasicAbility => _isBasicAbility;
        public TargetingStrategy TargetingStrategy => _targetingStrategy;
        public IReadOnlyList<AbilityEffect> Effects => _effects;

        /// <summary>
        /// Creates a runtime IAbility instance from this configuration.
        /// </summary>
        public IAbility CreateAbility(IUnit owner)
        {
            return new GenericAbilityImpl(owner, this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_effects == null)
            {
                _effects = new List<AbilityEffect>();
            }
        }
#endif
    }
}
