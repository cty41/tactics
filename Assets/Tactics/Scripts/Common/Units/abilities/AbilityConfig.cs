using Sirenix.OdinInspector;
using Tactics.Common.Controllers;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// ScriptableObject container for ability configurations.
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

        [BoxGroup("Costs")]
        [Tooltip("每回合最大成功使用次数。0 表示不限次数，1 等价于 cantrip 的每回合一次限制。")]
        [SerializeField, Min(0)] private int _maxUsesPerTurn;

        [BoxGroup("Basic Info")]
        [Tooltip("If true, this ability can be used once per turn without consuming Mana. Examples: Move, MeleeAttack, RangedAttack.")]
        [SerializeField] private bool _isBasicAbility;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public string Description => _description;
        public int ManaCost => _manaCost;
        public float Cooldown => _cooldown;
        public int MaxUsesPerTurn => _maxUsesPerTurn;
        public bool IsBasicAbility => _isBasicAbility;

        /// <summary>
        /// Sets basic config fields for runtime-created instances (e.g., default Move fallback).
        /// </summary>
        protected void InitializeRuntime(string displayName, bool isBasicAbility, int maxUsesPerTurn = 0)
        {
            _displayName = displayName;
            _isBasicAbility = isBasicAbility;
            _maxUsesPerTurn = Mathf.Max(0, maxUsesPerTurn);
        }

        /// <summary>
        /// Creates a runtime IAbility instance from this configuration.
        /// </summary>
        public virtual IAbility CreateAbility(IUnit owner)
        {
            TLog.Warning($"[AbilityConfig] CreateAbility is not supported on base AbilityConfig asset '{name}'.");
            return null;
        }
    }
}
