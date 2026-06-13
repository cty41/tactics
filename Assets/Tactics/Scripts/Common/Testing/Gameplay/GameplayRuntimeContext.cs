using System;
using System.Collections.Generic;
using Tactics.Common.Battle;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Units;
using Tactics.Common.Cells;
using Tactics.Common.Units.Abilities;

namespace Tactics.Common.Testing.Gameplay
{
    public sealed class GameplayRuntimeContext : IDisposable
    {
        public SkillGraphTestWorld SkillWorld { get; set; }
        public SkillGraphRuntimeTestResult LastSkillResult { get; set; }
        public string LastStepMessage { get; set; }
        public BattleController BattleController { get; set; }
        public GameResult? LastBattleResult { get; set; }
        public Dictionary<string, SkillGraphAsset> SkillGraphs { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SkillGraphAbilityConfig> SkillAbilityConfigs { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, IAbility> SkillAbilities { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, IUnit> Units { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ICell> Cells { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Dispose()
        {
            SkillWorld?.Dispose();
            SkillWorld = null;
            SkillGraphs.Clear();
            SkillAbilityConfigs.Clear();
            SkillAbilities.Clear();
            Units.Clear();
            Cells.Clear();
            LastSkillResult = null;
            LastStepMessage = null;
            BattleController = null;
            LastBattleResult = null;
        }
    }
}
