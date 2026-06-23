using UnityEngine;

namespace Tactics.Common.Battle.Authoring
{
    /// <summary>
    /// Scene 中玩家出生点预览锚点。
    /// 仅用于编辑器可视化和辅助摆放，不再作为测试配置的运行时真相源。
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
