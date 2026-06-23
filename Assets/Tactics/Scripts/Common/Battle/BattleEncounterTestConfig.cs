using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// 测试用敌方关卡配置资产。
    /// 直接通过格子坐标生成，不依赖 scene 中的出生点对象。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Battle/Test Encounter Config")]
    public sealed class BattleEncounterTestConfig : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private List<EncounterTestSlot> _slots = new();
        [SerializeField] private List<CorpseTestSlot> _corpseSlots = new();

        public string DisplayName => _displayName;
        public IReadOnlyList<EncounterTestSlot> Slots => _slots;
        public IReadOnlyList<CorpseTestSlot> CorpseSlots => _corpseSlots;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_slots == null)
                _slots = new List<EncounterTestSlot>();
            if (_corpseSlots == null)
                _corpseSlots = new List<CorpseTestSlot>();
        }
#endif
    }

    [Serializable]
    public sealed class EncounterTestSlot
    {
        [SerializeField] private Vector2Int _spawnCell;
        [SerializeField] private GameObject _unitPrefab;
        [SerializeField] private string _aiBrainAssetPath;
        [SerializeField] private string _displayName;
        [SerializeField] private int _playerNumber = 2;

        public Vector2Int SpawnCell => _spawnCell;
        public GameObject UnitPrefab => _unitPrefab;
        public string AiBrainAssetPath => _aiBrainAssetPath;
        public string DisplayName => _displayName;
        public int PlayerNumber => _playerNumber;
    }

    /// <summary>
    /// 尸体测试 slot：在指定格子坐标同时生成 dead unit + Corpse interactable。
    /// </summary>
    [Serializable]
    public sealed class CorpseTestSlot
    {
        [SerializeField] private Vector2Int _spawnCell;
        [SerializeField] private GameObject _unitPrefab;
        [SerializeField] private string _displayName;
        [SerializeField] private int _playerNumber = 2;

        public Vector2Int SpawnCell => _spawnCell;
        public GameObject UnitPrefab => _unitPrefab;
        public string DisplayName => _displayName;
        public int PlayerNumber => _playerNumber;
    }
}
