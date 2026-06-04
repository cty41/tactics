using System.Collections.Generic;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// 技能图执行状态。
    /// </summary>
    public enum SkillGraphExecutionState
    {
        Running,
        Completed,
        Failed,
        Aborted
    }

    /// <summary>
    /// 单次施法执行上下文。
    /// 保存 SkillGraph 在一次释放过程中的全部运行时状态。
    /// </summary>
    public class SkillExecutionContext
    {
        // ── 施法者 ──
        public IUnit Caster { get; }

        // ── 图引用 ──
        public SkillGraphAsset GraphAsset { get; }
        public SkillGraphRuntimeDefinition RuntimeDef { get; }

        // ── 执行状态 ──
        public SkillGraphExecutionState State { get; set; } = SkillGraphExecutionState.Running;
        public string CurrentNodeId { get; set; }
        public string LastError { get; set; }
        public int StepCount { get; set; }

        // ── 运行时目标数据 ──
        public IUnit PrimaryTarget { get; set; }
        public ICell TargetPoint { get; set; }
        public List<IUnit> TargetSet { get; set; } = new();
        public IGridController GridController { get; }

        // ── 黑板（临时变量）──
        private readonly Dictionary<string, object> _blackboard = new();

        // ── 最大步数保护 ──
        public int MaxSteps { get; set; } = 200;

        public SkillExecutionContext(IUnit caster, SkillGraphAsset graphAsset, SkillGraphRuntimeDefinition runtimeDef, IGridController gridController)
        {
            Caster = caster;
            GraphAsset = graphAsset;
            RuntimeDef = runtimeDef;
            GridController = gridController;
            CurrentNodeId = runtimeDef.EntryNodeId;
        }

        // ── 黑板操作 ──

        public void SetBlackboard<T>(string key, T value)
        {
            _blackboard[key] = value;
        }

        public T GetBlackboard<T>(string key, T defaultValue = default)
        {
            if (_blackboard.TryGetValue(key, out var value) && value is T typed)
                return typed;
            return defaultValue;
        }

        public bool HasBlackboard(string key)
        {
            return _blackboard.ContainsKey(key);
        }

        // ── 状态查询 ──

        public bool IsRunning => State == SkillGraphExecutionState.Running;
        public bool IsCompleted => State == SkillGraphExecutionState.Completed;
        public bool IsFailed => State == SkillGraphExecutionState.Failed;
        public bool IsAborted => State == SkillGraphExecutionState.Aborted;

        public void Complete()
        {
            State = SkillGraphExecutionState.Completed;
        }

        public void Fail(string reason = null)
        {
            State = SkillGraphExecutionState.Failed;
            LastError = reason;
        }

        public void Abort(string reason = null)
        {
            State = SkillGraphExecutionState.Aborted;
            LastError = reason;
        }
    }
}
