using System;
using System.Collections.Generic;
using Tactics.Common.Units.Classes;
using UnityEngine;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// 测试用玩家队伍配置资产。
    /// 通过 SpawnId 绑定 scene 中的 PlayerSpawnPoint，不直接引用 scene 对象。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Battle/Test Party Config")]
    public sealed class BattlePartyTestConfig : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private List<PartyTestSlot> _slots = new();

        public string DisplayName => _displayName;
        public IReadOnlyList<PartyTestSlot> Slots => _slots;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_slots == null)
                _slots = new List<PartyTestSlot>();
        }
#endif
    }

    [Serializable]
    public sealed class PartyTestSlot
    {
        [SerializeField] private string _spawnId;
        [SerializeField] private GameObject _unitPrefab;
        [SerializeField] private RoleType _roleType = RoleType.Barbarian;
        [SerializeField] private string _displayName;
        [SerializeField] private int _level = 1;

        [Header("Combat Overrides")]
        [SerializeField] private int _strength = 5;
        [SerializeField] private int _agility = 5;
        [SerializeField] private int _constitution = 5;
        [SerializeField] private int _intelligence = 5;
        [SerializeField] private int _charisma = 5;
        [SerializeField] private int _luck = 5;
        [SerializeField] private float _speed = 5f;
        [SerializeField] private int _attackFactor = 1;
        [SerializeField] private int _defenceFactor = 1;

        public string SpawnId => _spawnId;
        public GameObject UnitPrefab => _unitPrefab;
        public RoleType RoleType => _roleType;
        public string DisplayName => _displayName;
        public int Level => _level;
        public int Strength => _strength;
        public int Agility => _agility;
        public int Constitution => _constitution;
        public int Intelligence => _intelligence;
        public int Charisma => _charisma;
        public int Luck => _luck;
        public float Speed => _speed;
        public int AttackFactor => _attackFactor;
        public int DefenceFactor => _defenceFactor;
    }
}
