using System.Threading.Tasks;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// 节点执行器接口。
    /// </summary>
    public interface ISkillNodeExecutor
    {
        SkillGraphNodeType NodeType { get; }
        Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context);
    }
}
