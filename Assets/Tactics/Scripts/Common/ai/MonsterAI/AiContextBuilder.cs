using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.AI.MonsterAI
{
    /// <summary>
    /// AI 上下文构建器。
    /// 集中构建一次决策快照。
    /// </summary>
    public static class AiContextBuilder
    {
        /// <summary>
        /// 构建 AI 决策上下文。
        /// </summary>
        public static AiContext Build(IUnit self, IGridController gridController, AiBrainAsset brainAsset)
        {
            var decisionLog = new AiDecisionLog(brainAsset.EnableVerboseLogging);
            decisionLog.Info($"Building context for unit: Unit_{self.UnitID}");

            // 从全战场单位集合获取所有单位
            var allUnits = gridController.UnitManager.GetUnits().ToList();
            decisionLog.Info($"Total battlefield units: {allUnits.Count}");

            // 分离敌我单位
            var enemies = new List<IUnit>();
            var allies = new List<IUnit>();
            foreach (var unit in allUnits)
            {
                if (unit == self || unit.IsDowned) continue;

                if (unit.PlayerNumber != self.PlayerNumber)
                {
                    enemies.Add(unit);
                }
                else
                {
                    allies.Add(unit);
                }
            }

            decisionLog.Info($"Enemies: {enemies.Count}, Allies: {allies.Count}");

            // 获取可达格子
            var reachableCells = self.GetAvailableDestinations(gridController.CellManager.GetCells());
            decisionLog.Info($"Reachable cells: {reachableCells.Count}");

            // 获取候选目标（所有活着的敌人）
            var candidateTargets = enemies.Where(e => !e.IsDowned).ToList();
            decisionLog.Info($"Candidate targets: {candidateTargets.Count}");

            // 获取可用技能（通过 CanPerform 判定实际可用性）
            var availableAbilities = new List<AbilityInfo>();
            var baseAbilities = self.GetBaseAbilities();
            if (baseAbilities != null)
            {
                foreach (var ability in baseAbilities)
                {
                    bool isReady = ability.CanPerform(gridController);
                    int range = ability is Tactics.Common.Units.Abilities.IAbility a ? self.AttackRange : self.AttackRange;
                    availableAbilities.Add(new AbilityInfo(ability.DisplayName, range, isReady, ability));
                }
            }
            decisionLog.Info($"Available abilities: {availableAbilities.Count}");

            return new AiContext(
                self,
                gridController,
                enemies,
                allies,
                reachableCells,
                candidateTargets,
                availableAbilities,
                brainAsset,
                decisionLog
            );
        }
    }
}
