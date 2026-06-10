using System;
using System.Collections.Generic;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Units;

namespace Tactics.Common.Testing.Gameplay
{
    public sealed class GameplayRuntimeContext : IDisposable
    {
        public SkillGraphTestWorld SkillWorld { get; set; }
        public SkillGraphRuntimeTestResult LastSkillResult { get; set; }
        public Dictionary<string, SkillGraphAsset> SkillGraphs { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, IUnit> Units { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Dispose()
        {
            SkillWorld?.Dispose();
            SkillWorld = null;
            SkillGraphs.Clear();
            Units.Clear();
            LastSkillResult = null;
        }
    }
}
