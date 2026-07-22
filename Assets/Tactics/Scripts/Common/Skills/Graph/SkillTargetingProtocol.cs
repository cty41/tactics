using System;

namespace Tactics.Common.Skills.Graph
{
    public enum SkillTargetMode
    {
        PrimaryUnit,
        AnyCellCenter,
        DirectionCone,
        OrderedMultiTarget,
        PhysicalObjectCell,
        RecoveryAction,
        PathlessMove
    }

    /// <summary>
    /// Serializable targeting contract shared by player input, AI queries, and tests.
    /// Existing graphs default to PrimaryUnit and may still infer a more specific mode
    /// from their selection nodes.
    /// </summary>
    [Serializable]
    public sealed class SkillTargetingProtocol
    {
        public SkillTargetMode Mode = SkillTargetMode.PrimaryUnit;
        public int MinimumSelections = 1;
        public int MaximumSelections = 1;
        public int ConeDepth = 1;
        public int ConeWidth = 1;
        public bool AllowsEmptyCell;
        public bool UsesPathfinding = true;
    }
}
