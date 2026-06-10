using System.Collections.Generic;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// 节点执行器注册表。
    /// </summary>
    public static class SkillNodeExecutorRegistry
    {
        private static readonly Dictionary<SkillGraphNodeType, ISkillNodeExecutor> Executors = new();

        static SkillNodeExecutorRegistry()
        {
            Register(new StartNodeExecutor());
            Register(new SelectPrimaryTargetNodeExecutor());
            Register(new SelectTargetPointNodeExecutor());
            Register(new CollectTargetsInAreaNodeExecutor());
            Register(new ForEachTargetNodeExecutor());
            Register(new DashToTargetNodeExecutor());
            Register(new ApplyDamageNodeExecutor());
            Register(new ApplyKnockbackNodeExecutor());
            Register(new FinishNodeExecutor());
            Register(new FailNodeExecutor());
            Register(new ProjectileLaunchNodeExecutor());
            Register(new OnHitNodeExecutor());
            Register(new ApplyBuffNodeExecutor());
            Register(new SelectSelfNodeExecutor());
            Register(new SelectAllyNodeExecutor());
            Register(new ApplyHealNodeExecutor());
            Register(new DashToAllyNodeExecutor());
            Register(new LaunchUnitNodeExecutor());
            Register(new SelectMoveDestinationNodeExecutor());
            Register(new ExecuteMoveNodeExecutor());
        }

        public static void Register(ISkillNodeExecutor executor)
        {
            Executors[executor.NodeType] = executor;
        }

        public static ISkillNodeExecutor Get(SkillGraphNodeType nodeType)
        {
            Executors.TryGetValue(nodeType, out var executor);
            return executor;
        }
    }
}
