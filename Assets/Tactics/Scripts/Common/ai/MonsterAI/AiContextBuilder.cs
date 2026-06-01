using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
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

            var allCells = gridController.CellManager.GetCells().ToList();
            decisionLog.Info($"Current cell: {(self.CurrentCell != null ? $"({self.CurrentCell.GridCoordinates.x}, {self.CurrentCell.GridCoordinates.y})" : "None")}");
            decisionLog.Info($"Movement: {self.MovementPoints:F1}/{self.MaxMovementPoints:F1}, Total cells: {allCells.Count}");

            // 新 AI 不经过能力选中流程，必须在读取可达格前主动建立移动路径缓存。
            if (self.CurrentCell != null)
            {
                self.CachePaths(gridController.CellManager);
            }
            else
            {
                decisionLog.Info("Current cell is null; skipping movement path cache.");
            }

            // 获取可达格子
            var reachableCells = self.GetAvailableDestinations(allCells);
            decisionLog.Info($"Reachable cells: {reachableCells.Count}");

            // 获取候选目标（所有活着的敌人）
            var candidateTargets = enemies.Where(e => !e.IsDowned).ToList();
            decisionLog.Info($"Candidate targets: {candidateTargets.Count}");
            foreach (var target in candidateTargets)
            {
                decisionLog.Info(
                    $"Target Unit_{target.UnitID}: cell={FormatCell(target.CurrentCell)}, hp={target.Health:F1}/{target.MaxHealth:F1}, downed={target.IsDowned}");
            }

            // 获取可用技能（通过 CanPerform 判定实际可用性）
            var availableAbilities = new List<AbilityInfo>();
            var baseAbilities = self.GetBaseAbilities();
            if (baseAbilities != null)
            {
                foreach (var ability in baseAbilities)
                {
                    bool isReady = ability.CanPerform(gridController);
                    var metadata = BuildAbilityMetadata(ability, self);
                    availableAbilities.Add(new AbilityInfo(
                        ability.DisplayName,
                        metadata.Range,
                        isReady,
                        ability,
                        metadata.Tags,
                        metadata.BaseDamage,
                        metadata.HealAmount,
                        metadata.ControlValue,
                        metadata.UtilityValue));
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

        private static (int Range, AbilityAiTags Tags, float BaseDamage, float HealAmount, float ControlValue, float UtilityValue)
            BuildAbilityMetadata(IAbility ability, IUnit self)
        {
            int range = self.AttackRange;
            AbilityAiTags tags = AbilityAiTags.None;
            float baseDamage = 0f;
            float healAmount = 0f;
            float controlValue = 0f;
            float utilityValue = 0f;

            if (ability is GenericAbilityImpl generic && generic.Config != null)
            {
                var config = generic.Config;
                range = GetRange(config.TargetingStrategy, self.AttackRange);
                tags |= GetTargetingTags(config.TargetingStrategy);

                foreach (var effect in config.Effects)
                {
                    switch (effect)
                    {
                        case DamageEffect damage:
                            tags |= AbilityAiTags.Damage;
                            baseDamage += damage.BaseDamage;
                            break;
                        case HealEffect heal:
                            tags |= AbilityAiTags.Heal;
                            healAmount += heal.HealAmount;
                            break;
                        case ApplyBuffEffect:
                            tags |= AbilityAiTags.Buff | AbilityAiTags.Utility;
                            utilityValue += 0.35f;
                            break;
                        case DamageOverTimeEffect dot:
                            tags |= AbilityAiTags.Damage | AbilityAiTags.Debuff;
                            baseDamage += dot.DamagePerTurn * dot.Duration;
                            utilityValue += 0.25f;
                            break;
                        case KnockbackEffect knockback:
                            tags |= AbilityAiTags.Control;
                            controlValue += 0.2f + knockback.Distance * 0.1f;
                            break;
                        case SpawnEffect:
                            tags |= AbilityAiTags.Utility;
                            utilityValue += 0.5f;
                            break;
                        case MoveEffect:
                            tags |= AbilityAiTags.Movement;
                            break;
                    }
                }
            }

            if (tags == AbilityAiTags.None)
            {
                tags = InferTagsFromName(ability.DisplayName);
            }

            return (range, tags, baseDamage, healAmount, controlValue, utilityValue);
        }

        private static string FormatCell(ICell cell)
        {
            return cell != null ? $"({cell.GridCoordinates.x}, {cell.GridCoordinates.y})" : "None";
        }

        private static int GetRange(TargetingStrategy strategy, int fallback)
        {
            return strategy switch
            {
                SingleTargetEnemy single => single.MaxRange,
                SingleTargetAlly ally => ally.MaxRange,
                AoETargeting aoe => aoe.MaxRange,
                MultiTargetEnemy multi => multi.MaxRange,
                MoveThenHealTargeting heal => heal.HealRange,
                _ => fallback
            };
        }

        private static AbilityAiTags GetTargetingTags(TargetingStrategy strategy)
        {
            return strategy switch
            {
                AoETargeting => AbilityAiTags.Aoe,
                MultiTargetEnemy => AbilityAiTags.Aoe,
                MoveThenHealTargeting => AbilityAiTags.Heal | AbilityAiTags.Movement,
                MoveThenAttackTargeting => AbilityAiTags.Damage | AbilityAiTags.Movement,
                _ => AbilityAiTags.None
            };
        }

        private static AbilityAiTags InferTagsFromName(string abilityName)
        {
            string name = abilityName?.ToLowerInvariant() ?? string.Empty;
            AbilityAiTags tags = AbilityAiTags.None;

            if (name.Contains("move")) tags |= AbilityAiTags.Movement;
            if (name.Contains("heal")) tags |= AbilityAiTags.Heal;
            if (name.Contains("buff")) tags |= AbilityAiTags.Buff | AbilityAiTags.Utility;
            if (name.Contains("debuff") || name.Contains("poison") || name.Contains("burn")) tags |= AbilityAiTags.Debuff;
            if (name.Contains("stun") || name.Contains("slow") || name.Contains("knock")) tags |= AbilityAiTags.Control;
            if (name.Contains("fire") || name.Contains("attack") || name.Contains("damage")) tags |= AbilityAiTags.Damage;
            if (name.Contains("area") || name.Contains("aoe")) tags |= AbilityAiTags.Aoe;

            return tags;
        }
    }
}
