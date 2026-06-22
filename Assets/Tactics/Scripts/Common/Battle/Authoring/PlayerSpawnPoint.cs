using UnityEngine;

namespace Tactics.Common.Battle.Authoring
{
    /// <summary>
    /// Scene 中玩家出生点标记。
    /// 通过 SpawnId 与测试配置资产绑定，不直接存 scene 引用。
    /// </summary>
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string _spawnId;

        public string SpawnId => _spawnId;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_spawnId))
            {
                _spawnId = gameObject.name;
            }
        }
#endif
    }
}
