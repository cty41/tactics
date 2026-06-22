using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// 测试用敌方关卡配置资产。
    /// 通过 SpawnId 绑定 scene 中的 EnemySpawnPoint，不直接引用 scene 对象。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Battle/Test Encounter Config")]
    public sealed class BattleEncounterTestConfig : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private List<EncounterTestSlot> _slots = new();

        public string DisplayName => _displayName;
        public IReadOnlyList<EncounterTestSlot> Slots => _slots;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_slots == null)
                _slots = new List<EncounterTestSlot>();
        }
#endif
    }

    [Serializable]
    public sealed class EncounterTestSlot
    {
        [SerializeField] private string _spawnId;
        [SerializeField] private GameObject _unitPrefab;
        [SerializeField] private string _aiBrainAssetPath;
        [SerializeField] private string _displayName;
        [SerializeField] private int _playerNumber = 2;

        public string SpawnId => _spawnId;
        public GameObject UnitPrefab => _unitPrefab;
        public string AiBrainAssetPath => _aiBrainAssetPath;
        public string DisplayName => _displayName;
        public int PlayerNumber => _playerNumber;
    }
}
