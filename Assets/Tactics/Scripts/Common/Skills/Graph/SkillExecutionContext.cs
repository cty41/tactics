using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tactics.Common.Battle.Runtime;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Interactables;
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
    /// 结构化执行事件（替代字符串匹配）。
    /// </summary>
    public sealed class SkillGraphExecutionEvent
    {
        public string EventType;
        public string NodeId;
        public string TargetUnitName;
        public string CellCoordinates;
        public float Timestamp;
    }

    /// <summary>
    /// 阶段执行结果快照。
    /// </summary>
    public sealed class SkillStageResult
    {
        public int StageIndex;
        public string NodeId;
        public SkillGraphExecutionState State;
        public string FailReason;
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
        public List<Corpse> TargetCorpses { get; set; } = new();
        public IGridController GridController { get; }

        // ── 黑板（临时变量）──
        private readonly Dictionary<string, object> _blackboard = new();

        // ── 最大步数保护 ──
        public int MaxSteps { get; set; } = 200;

        // ── 取消令牌 ──
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        // ── 运行时作用域 ──
        public IBattleRuntimeScope RuntimeScope { get; set; }

        // ── 可选表现层 ──
        public ISkillVfxSink VfxSink { get; set; }

        // ── 结构化事件追踪 ──
        public List<SkillGraphExecutionEvent> ExecutionEvents { get; } = new();

        // ── 阶段结果记录 ──
        public List<SkillStageResult> StageResults { get; } = new();

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

        public void ClearBlackboard()
        {
            _blackboard.Clear();
        }

        public int ResolveSkillLevel()
        {
            if (GraphAsset?.Nodes == null)
                return 1;
            foreach (SkillGraphNodeRecord node in GraphAsset.Nodes)
            {
                switch (node)
                {
                    case AmazonSkillNodeRecord amazon:
                        return amazon.Level;
                    case MageSkillNodeRecord mage:
                        return mage.Level;
                    case NecromancerSkillNodeRecord necromancer:
                        return necromancer.Level;
                }
            }
            return 1;
        }

        /// <summary>
        /// Plays an optional semantic VFX cue without making visual availability a gameplay dependency.
        /// </summary>
        public Task PlayVfxAsync(SkillVfxCueKind cue, SkillVfxCueContext cueContext)
        {
            if (VfxSink == null || cueContext == null)
                return Task.CompletedTask;

            var cancellationToken = RuntimeScope?.Token ?? CancellationToken;
            return VfxSink.PlayAsync(cue, cueContext, cancellationToken);
        }

        // ── 事件记录 ──

        public void RecordEvent(string eventType, string nodeId, IUnit target = null)
        {
            bool targetAvailable = IsUnityUnitAvailable(target);
            ExecutionEvents.Add(new SkillGraphExecutionEvent
            {
                EventType = eventType,
                NodeId = nodeId,
                TargetUnitName = targetAvailable ? GetUnitName(target) : null,
                CellCoordinates = targetAvailable ? target.CurrentCell?.GridCoordinates.ToString() : null,
                Timestamp = UnityEngine.Time.time
            });
        }

        public void RecordEventAtCell(string eventType, string nodeId, Tactics.Common.Cells.ICell cell)
        {
            ExecutionEvents.Add(new SkillGraphExecutionEvent
            {
                EventType = eventType,
                NodeId = nodeId,
                CellCoordinates = cell?.GridCoordinates.ToString(),
                Timestamp = UnityEngine.Time.time
            });
        }

        // ── 阶段记录 ──

        public void RecordStage(int stageIndex, string nodeId, SkillGraphExecutionState state, string failReason = null)
        {
            StageResults.Add(new SkillStageResult
            {
                StageIndex = stageIndex,
                NodeId = nodeId,
                State = state,
                FailReason = failReason
            });
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

        private static string GetUnitName(IUnit unit)
        {
            if (!IsUnityUnitAvailable(unit)) return null;
            return unit is UnityEngine.MonoBehaviour mb ? mb.gameObject.name : unit.GetType().Name;
        }

        private static bool IsUnityUnitAvailable(IUnit unit)
        {
            return unit != null &&
                (unit is not UnityEngine.Object unityObject || unityObject != null);
        }
    }
}
