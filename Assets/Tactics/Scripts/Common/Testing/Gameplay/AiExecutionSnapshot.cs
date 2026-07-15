using Tactics.Common.Utilities;

namespace Tactics.Common.Testing.Gameplay
{
    /// <summary>
    /// AI 执行前后状态快照。
    /// 用于验证 AI 决策是否真正产出了移动/攻击/治疗等效果。
    /// </summary>
    public sealed class AiExecutionSnapshot
    {
        // Actor（执行 AI 的单位）
        public string ActorAlias { get; set; }
        public int ActorUnitId { get; set; }
        public Vector2IntImpl ActorPositionBefore { get; set; }
        public Vector2IntImpl ActorPositionAfter { get; set; }
        public float ActorHealthBefore { get; set; }
        public float ActorHealthAfter { get; set; }
        public float ActorManaBefore { get; set; }
        public float ActorManaAfter { get; set; }

        // Target（AI 选中的目标单位，可能为 null）
        public string TargetAlias { get; set; }
        public int TargetUnitId { get; set; }
        public Vector2IntImpl TargetPositionBefore { get; set; }
        public Vector2IntImpl TargetPositionAfter { get; set; }
        public float TargetHealthBefore { get; set; }
        public float TargetHealthAfter { get; set; }

        // AI 决策结果
        public string SelectedIntentType { get; set; }
        public string SelectedActionType { get; set; }
        public string SelectedAbilityName { get; set; }
        public float SelectedScore { get; set; }

        // 执行效果
        public bool DidMove { get; set; }
        public bool DidDamageTarget { get; set; }
        public bool DidHealTarget { get; set; }
        public bool WasNoOp { get; set; }
        public string FailureReason { get; set; }
    }

    /// <summary>
    /// Stable, structured projection of one AI turn for gameplay assertions.
    /// </summary>
    /// <remarks>
    /// This test-facing contract remains available while the runtime AI result API evolves.
    /// Missing runtime fields are represented by empty strings or zero rather than inferred success.
    /// </remarks>
    public sealed class AiTurnResultSnapshot
    {
        public bool Succeeded { get; set; }
        public string AbilityId { get; set; }
        public string Destination { get; set; }
        public string TargetPoint { get; set; }
        public int TargetCount { get; set; }
        public bool UsedFallback { get; set; }
        public string PatternStep { get; set; }
        public string FailureReason { get; set; }
    }
}
