using System.Collections.Generic;
using Sirenix.OdinInspector;
using Tactics.Common.Skills.Graph;
using UnityEngine;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// SkillGraph 能力配置。
    /// 持有 SkillGraphAsset 引用，创建 SkillGraphAbilityImpl。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Abilities/Skill Graph Ability Config")]
    public class SkillGraphAbilityConfig : AbilityConfig
    {
        [BoxGroup("Skill Graph")]
        [SerializeField] private SkillGraphAsset _skillGraph;

        [BoxGroup("Skill Graph")]
        [SerializeField] private int _targetRange = 1;

        public SkillGraphAsset SkillGraph => _skillGraph;
        public int TargetRange => _targetRange;

        public override IAbility CreateAbility(IUnit owner)
        {
            return new SkillGraphAbilityImpl(owner, this);
        }

        private static SkillGraphAbilityConfig _defaultMoveConfig;

        /// <summary>
        /// Creates a minimal runtime Move config for environments where GameAssetManager is unavailable.
        /// </summary>
        public static SkillGraphAbilityConfig CreateDefaultMoveConfig()
        {
            if (_defaultMoveConfig != null) return _defaultMoveConfig;

            var graph = CreateInstance<SkillGraphAsset>();
            graph.DisplayName = "Move";
            var start = graph.AddNode(SkillGraphNodeType.Start, Vector2.zero);
            var selectDest = graph.AddNode(SkillGraphNodeType.SelectMoveDestination, new Vector2(200, 0));
            var execMove = graph.AddNode(SkillGraphNodeType.ExecuteMove, new Vector2(400, 0));
            graph.AddEdge(start.NodeId, selectDest.NodeId);
            graph.AddEdge(selectDest.NodeId, execMove.NodeId);

            _defaultMoveConfig = CreateInstance<SkillGraphAbilityConfig>();
            _defaultMoveConfig.InitializeRuntime("Move", true);
            _defaultMoveConfig._skillGraph = graph;
            _defaultMoveConfig._targetRange = 1;
            return _defaultMoveConfig;
        }
    }
}
