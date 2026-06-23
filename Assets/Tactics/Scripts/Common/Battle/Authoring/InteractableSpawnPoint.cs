using UnityEngine;

namespace Tactics.Common.Battle.Authoring
{
    /// <summary>
    /// 通用交互物出生点预览锚点。
    /// 用于编辑器可视化和辅助摆放，尸体是第一种接入对象，后续可扩展宝箱、金币等。
    /// </summary>
    public sealed class InteractableSpawnPoint : MonoBehaviour
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
