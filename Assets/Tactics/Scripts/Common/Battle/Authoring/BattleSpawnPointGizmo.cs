using UnityEngine;

namespace Tactics.Common.Battle.Authoring
{
    /// <summary>
    /// 轻量 Gizmo 辅助组件，用于在 SceneView 中区分玩家/敌方出生点。
    /// 不承担运行时逻辑，仅提升 Editor 所见即所得体验。
    /// </summary>
    [ExecuteInEditMode]
    public sealed class BattleSpawnPointGizmo : MonoBehaviour
    {
        [SerializeField] private Color _color = Color.cyan;
        [SerializeField] private float _radius = 0.35f;
        [SerializeField] private string _label;

        public void Setup(Color color, float radius, string label)
        {
            _color = color;
            _radius = radius;
            _label = label;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _color;
            Gizmos.DrawSphere(transform.position, _radius);

            if (!string.IsNullOrWhiteSpace(_label))
            {
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, _label);
                #endif
            }
        }
    }
}
