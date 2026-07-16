using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Units;
using Tactics.RoguelikeMap.Interaction;
using Tactics.RoguelikeMap.Economy;
using Tactics.Roster;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// 战斗奖励计算系统。
    /// 根据战斗结果、回合数和敌方单位计算金币和经验奖励，
    /// 并提供将奖励应用到玩家冒险状态的方法。
    /// </summary>
    public static class BattleRewardSystem
    {
        /// <summary>Accumulated cheat experience to be merged into settlement rewards.</summary>
        public static readonly Dictionary<string, int> PendingCheatExperience = new Dictionary<string, int>();

        /// <summary>
        /// 战斗奖励数据，包含金币和经验分配结果。
        /// </summary>
        public struct BattleRewards
        {
            /// <summary>总金币奖励。</summary>
            public int TotalGold;

            /// <summary>每个角色获得的经验值映射（CharacterId → 经验值）。</summary>
            public Dictionary<string, int> ExperiencePerCharacter;

            /// <summary>战斗总回合数。</summary>
            public int TotalRounds;

            /// <summary>Run-only item drops generated during settlement.</summary>
            public List<string> ItemIds;

            /// <summary>
            /// 转换为统一节点结果结构，供 Roguelike 地图层统一消费。
            /// </summary>
            public RewardResult ToRewardResult()
            {
                var result = new RewardResult
                {
                    GoldAmount = TotalGold,
                    ExperienceAmount = ExperiencePerCharacter?.Values.Sum() ?? 0,
                    EnemiesDefeated = ExperiencePerCharacter?.Count ?? 0,
                    IsBattleReward = true
                };
                if (ItemIds != null)
                    result.ItemIds.AddRange(ItemIds);
                return result;
            }
        }

        /// <summary>
        /// 计算战斗奖励。
        /// </summary>
        /// <param name="result">战斗结果，包含胜者和败者信息。</param>
        /// <param name="totalRounds">战斗总回合数。</param>
        /// <param name="enemyUnits">敌方单位集合，用于计算经验值。</param>
        /// <returns>计算得到的战斗奖励数据。</returns>
        public static BattleRewards CalculateBattleRewards(GameResult result, int totalRounds, IEnumerable<IUnit> enemyUnits)
        {
            var rewards = new BattleRewards
            {
                TotalGold = 0,
                ExperiencePerCharacter = new Dictionary<string, int>(),
                TotalRounds = totalRounds,
                ItemIds = new List<string>()
            };

            if (result.Winners == null)
            {
                TLog.Warning("[BattleRewardSystem] GameResult 没有胜者，返回空奖励。");
                return rewards;
            }

            var enemyList = enemyUnits?.ToList() ?? new List<IUnit>();

            // Step 1: 计算击败敌方单位获得的总经验值
            int totalExperience = 0;
            foreach (var enemy in enemyList)
            {
                EnemyType enemyType = DetermineEnemyType(enemy, enemyList);
                int enemyLevel = GetUnitLevel(enemy);
                int exp = ExperienceSystem.CalculateExperienceReward(enemyLevel, enemyType);
                totalExperience += exp;

                string enemyName = GetUnitName(enemy);
                TLog.Info($"[BattleRewardSystem] 敌人 '{enemyName}' (等级 {enemyLevel}, 类型 {enemyType}) 贡献 {exp} 经验值。");
            }

            // Step 2: 从胜者中获取存活的友方单位
            var friendlyUnits = new List<IUnit>();
            if (BattleController.Instance != null)
            {
                foreach (var winner in result.Winners)
                {
                    var units = BattleController.Instance.GetFriendlyUnits(winner);
                    if (units != null)
                    {
                        friendlyUnits.AddRange(units.Where(u => u.Health > 0));
                    }
                }
            }
            else
            {
                TLog.Warning("[BattleRewardSystem] BattleController.Instance 为 null，无法解析友方单位进行经验分配。");
            }

            // Step 3: 将经验值平均分配给所有存活的友方单位
            if (friendlyUnits.Count > 0 && totalExperience > 0)
            {
                int expPerUnit = totalExperience / friendlyUnits.Count;
                int remainder = totalExperience % friendlyUnits.Count;

                for (int i = 0; i < friendlyUnits.Count; i++)
                {
                    var unit = friendlyUnits[i];
                    string characterId = GetCharacterId(unit);
                    int expForThisUnit = expPerUnit + (i < remainder ? 1 : 0);

                    if (rewards.ExperiencePerCharacter.ContainsKey(characterId))
                        rewards.ExperiencePerCharacter[characterId] += expForThisUnit;
                    else
                        rewards.ExperiencePerCharacter[characterId] = expForThisUnit;
                }

                TLog.Info($"[BattleRewardSystem] 将 {totalExperience} 经验值平均分配给 {friendlyUnits.Count} 个存活单位。");
            }
            else if (friendlyUnits.Count == 0)
            {
                TLog.Warning("[BattleRewardSystem] 没有存活的友方单位，经验值未分配。");
            }

            // Step 4: 根据回合数计算金币奖励
            int baseGold = 3;
            int roundBonus = totalRounds switch
            {
                <= 3 => 5,
                <= 5 => 3,
                <= 10 => 1,
                _ => 0
            };
            rewards.TotalGold = baseGold + roundBonus;

            TLog.Info($"[BattleRewardSystem] 金币：{baseGold}（基础）+ {roundBonus}（回合奖励）= {rewards.TotalGold}");

            return rewards;
        }

        /// <summary>
        /// 应用战斗奖励到玩家冒险状态。
        /// 将金币累加到 state.Gold，将经验值分配给对应角色，并检查升级。
        /// </summary>
        /// <param name="state">玩家冒险状态，将被修改。</param>
        /// <param name="rewards">要应用的奖励数据。</param>
        public static void ApplyRewards(PlayerAdventureState state, BattleRewards rewards)
        {
            if (state == null)
            {
                TLog.Warning("[BattleRewardSystem] ApplyRewards 接收到 null 的 state。");
                return;
            }

            // 累加金币
            rewards.ToRewardResult().ApplyGoldToState(state);
            TLog.Info($"[BattleRewardSystem] 添加 {rewards.TotalGold} 金币（总计：{state.Gold}）。");

            // 分配经验值
            if (rewards.ExperiencePerCharacter != null && rewards.ExperiencePerCharacter.Count > 0)
            {
                foreach (var kvp in rewards.ExperiencePerCharacter)
                {
                    string characterId = kvp.Key;
                    int expAmount = kvp.Value;

                    var character = state.Roster.FirstOrDefault(c => c.Id == characterId);
                    if (character == null)
                    {
                        TLog.Warning($"[BattleRewardSystem] 未在队伍中找到角色 '{characterId}'，跳过经验分配。");
                        continue;
                    }

                    character.Experience += expAmount;
                    TLog.Info($"[BattleRewardSystem] 为 {character.DisplayName} 添加 {expAmount} 经验值（总计：{character.Experience}）。");

                    // 检查并处理升级（可能连续升级）
                    bool leveledUp = ExperienceSystem.CheckLevelUp(character);
                    while (leveledUp)
                    {
                        character.Level++;
                        character.AttributePoints++;
                        TLog.Info($"[BattleRewardSystem] {character.DisplayName} 升级至等级 {character.Level}！");

                        leveledUp = ExperienceSystem.CheckLevelUp(character);
                    }
                }
            }
        }

        /// <summary>
        /// 判断敌人类型。
        /// Boss：名字包含"Boss"或是唯一敌人。
        /// Elite：名字包含"Elite"。
        /// Normal：其他情况。
        /// </summary>
        private static EnemyType DetermineEnemyType(IUnit enemy, List<IUnit> allEnemies)
        {
            string unitName = GetUnitName(enemy);

            // Boss 判定：名字含"Boss"或唯一敌人
            if ((!string.IsNullOrEmpty(unitName) && unitName.Contains("Boss")) || allEnemies.Count == 1)
            {
                return EnemyType.Boss;
            }

            // Elite 判定：名字含"Elite"
            if (!string.IsNullOrEmpty(unitName) && unitName.Contains("Elite"))
            {
                return EnemyType.Elite;
            }

            return EnemyType.Normal;
        }

        /// <summary>
        /// 获取单位等级。由于 IUnit 未暴露 Level 属性，默认返回 1。
        /// 具体实现类可通过其他组件提供等级数据。
        /// </summary>
        private static int GetUnitLevel(IUnit unit)
        {
            // IUnit 接口不包含 Level 属性，默认返回 1。
            return 1;
        }

        /// <summary>
        /// 获取单位显示名称。如果单位实现了 INamedUnit，则使用 UnitName；
        /// 否则回退到 UnitID。
        /// </summary>
        private static string GetUnitName(IUnit unit)
        {
            if (unit is INamedUnit namedUnit)
                return namedUnit.UnitName;
            return unit.UnitID.ToString();
        }

        /// <summary>
        /// 获取单位对应的角色 ID。
        /// 尝试从 RosterCharacterLink 组件获取 CharacterId；
        /// 如果没有则回退到 UnitID。
        /// </summary>
        private static string GetCharacterId(IUnit unit)
        {
            if (unit is MonoBehaviour mb)
            {
                var link = mb.GetComponent<RosterCharacterLink>();
                if (link != null && !string.IsNullOrEmpty(link.CharacterId))
                    return link.CharacterId;
            }

            TLog.Warning($"[BattleRewardSystem] 单位 {unit.UnitID} 上未找到 RosterCharacterLink，使用 UnitID 作为角色 ID。");
            return unit.UnitID.ToString();
        }
    }
}
