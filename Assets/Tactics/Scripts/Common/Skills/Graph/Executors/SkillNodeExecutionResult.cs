namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// 节点执行结果。
    /// </summary>
    public enum SkillNodeResultType
    {
        Success,
        Failed,
        Waiting,
        Completed
    }

    public class SkillNodeExecutionResult
    {
        public SkillNodeResultType ResultType { get; }
        public string BranchPort { get; }
        public string FailReason { get; }

        private SkillNodeExecutionResult(SkillNodeResultType type, string branchPort = null, string failReason = null)
        {
            ResultType = type;
            BranchPort = branchPort;
            FailReason = failReason;
        }

        public static SkillNodeExecutionResult Success()
            => new(SkillNodeResultType.Success);

        public static SkillNodeExecutionResult Branch(string portName)
            => new(SkillNodeResultType.Success, branchPort: portName);

        public static SkillNodeExecutionResult Failed(string reason = null)
            => new(SkillNodeResultType.Failed, failReason: reason);

        public static SkillNodeExecutionResult Waiting()
            => new(SkillNodeResultType.Waiting);

        public static SkillNodeExecutionResult Completed()
            => new(SkillNodeResultType.Completed);

        public bool IsSuccess => ResultType == SkillNodeResultType.Success;
        public bool IsFailed => ResultType == SkillNodeResultType.Failed;
        public bool IsWaiting => ResultType == SkillNodeResultType.Waiting;
        public bool IsCompleted => ResultType == SkillNodeResultType.Completed;
        public bool IsBranch => !string.IsNullOrEmpty(BranchPort);
    }
}
