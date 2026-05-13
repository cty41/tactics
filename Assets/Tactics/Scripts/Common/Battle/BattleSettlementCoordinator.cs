using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Units;
using Tactics.Roster;
using Tactics.Runtime.Utilities;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// 结算阶段枚举，定义战斗结算的各个阶段流转。
    /// </summary>
    public enum SettlementPhase
    {
        None,
        Rewards,               // 显示战斗奖励
        LevelUp,               // 升级检查
        AttributeAllocation,   // 属性加点
        SkillSelection,        // 技能选择
        Complete               // 结算完成
    }

    /// <summary>
    /// 战斗结算协调器，以单例模式运行。
    /// 管理奖励计算、升级检查、属性加点、技能选择等阶段的流转，
    /// 通过事件驱动通知外部 UI 层显示对应界面。
    /// </summary>
    public sealed class BattleSettlementCoordinator
    {
        private static readonly BattleSettlementCoordinator _instance = new BattleSettlementCoordinator();

        /// <summary>获取单例实例。</summary>
        public static BattleSettlementCoordinator Instance => _instance;

        private BattleSettlementCoordinator() { }

        #region Fields

        private SettlementPhase _currentPhase = SettlementPhase.None;
        private Action _onComplete;
        private GameResult _result;
        private int _totalRounds;
        private List<IUnit> _allUnits;
        private bool _isSettling;
        private PlayerAdventureState _state;

        #endregion

        #region Events

        /// <summary>当奖励计算完成时触发，携带奖励数据供 UI 显示。</summary>
        public event Action<BattleRewardSystem.BattleRewards> OnRewardsCalculated;

        /// <summary>当角色升级时触发，携带升级后的角色数据。</summary>
        public event Action<CharacterDefinition> OnCharacterLevelUp;

        /// <summary>当整个结算流程完成时触发。</summary>
        public event Action OnSettlementComplete;

        #endregion

        #region Properties

        /// <summary>结算是否已完成。</summary>
        public bool IsSettlementComplete => _currentPhase == SettlementPhase.Complete;

        /// <summary>当前结算阶段。</summary>
        public SettlementPhase CurrentPhase => _currentPhase;

        #endregion

        #region Public Methods

        /// <summary>
        /// 开始战斗结算流程。
        /// 计算奖励、保存到存档，并触发事件通知 UI 层显示结算。
        /// 注意：此阶段只计算和保存数据，不直接显示 UI。
        /// </summary>
        /// <param name="result">战斗结果（胜者/败者）。</param>
        /// <param name="totalRounds">总回合数。</param>
        /// <param name="allUnits">参与战斗的所有单位。</param>
        /// <param name="state">玩家冒险状态。</param>
        /// <param name="onComplete">结算完成时的回调。</param>
        public void StartSettlement(GameResult result, int totalRounds, IEnumerable<IUnit> allUnits, PlayerAdventureState state, Action onComplete)
        {
            if (_isSettling)
            {
                TLog.Warning("[BattleSettlementCoordinator] Settlement was already in progress. Force resetting.");
                _onComplete?.Invoke(); // 触发旧的回调避免泄漏
                _onComplete = null;
            }
            // 始终重置状态
            _isSettling = false;
            _currentPhase = SettlementPhase.None;

            if (allUnits == null)
            {
                TLog.Error("[BattleSettlementCoordinator] allUnits cannot be null.");
                return;
            }

            _isSettling = true;
            _result = result;
            _totalRounds = totalRounds;
            _allUnits = new List<IUnit>(allUnits);
            _state = state;
            _onComplete = onComplete;
            _currentPhase = SettlementPhase.None;

            TLog.Info($"[BattleSettlementCoordinator] Starting battle settlement. TotalRounds={totalRounds}, UnitCount={_allUnits.Count}");

            // Phase 1: 计算并保存奖励
            ProcessRewards();
        }

        /// <summary>
        /// 检查并处理角色升级。
        /// 如果角色满足升级条件：Level++，Experience 扣除升级所需经验，AttributePoints++。
        /// </summary>
        /// <param name="character">要检查的角色。</param>
        /// <returns>如果角色升级了返回 true，否则返回 false。</returns>
        public bool ProcessLevelUp(CharacterDefinition character)
        {
            if (character == null)
            {
                TLog.Warning("[BattleSettlementCoordinator] ProcessLevelUp called with null character.");
                return false;
            }

            bool hasLeveledUp = ExperienceSystem.CheckLevelUp(character);
            if (!hasLeveledUp)
            {
                TLog.Info($"[BattleSettlementCoordinator] Character {character.DisplayName} has not leveled up. Exp={character.Experience}");
                return false;
            }

            int maxLevel = ExperienceTable.GetMaxLevel();
            if (character.Level >= maxLevel)
            {
                TLog.Info($"[BattleSettlementCoordinator] Character {character.DisplayName} is already at max level ({maxLevel}).");
                return false;
            }

            int requiredExp = ExperienceTable.GetExperienceToNextLevel(character.Level);
            character.Experience -= requiredExp;
            character.Level++;
            // 每次升级获得1点属性点
            character.AttributePoints += 1;

            TLog.Info($"[BattleSettlementCoordinator] Character {character.DisplayName} leveled up! Level={character.Level}, RemainingExp={character.Experience}, AttributePoints={character.AttributePoints}");

            OnCharacterLevelUp?.Invoke(character);

            return true;
        }

        /// <summary>
        /// 获取当前阶段的下一个结算阶段。
        /// </summary>
        public SettlementPhase GetNextSettlementPhase()
        {
            return _currentPhase switch
            {
                SettlementPhase.None => SettlementPhase.Rewards,
                SettlementPhase.Rewards => SettlementPhase.LevelUp,
                SettlementPhase.LevelUp => SettlementPhase.AttributeAllocation,
                SettlementPhase.AttributeAllocation => SettlementPhase.SkillSelection,
                SettlementPhase.SkillSelection => SettlementPhase.Complete,
                SettlementPhase.Complete => SettlementPhase.Complete,
                _ => SettlementPhase.None
            };
        }

        /// <summary>
        /// 推进到下一个结算阶段。
        /// 如果推进到 Complete 阶段，触发 OnSettlementComplete 事件和完成回调。
        /// </summary>
        public void AdvancePhase()
        {
            SettlementPhase nextPhase = GetNextSettlementPhase();
            _currentPhase = nextPhase;

            TLog.Info($"[BattleSettlementCoordinator] Advanced to phase: {_currentPhase}");

            if (_currentPhase == SettlementPhase.Complete)
            {
                OnSettlementComplete?.Invoke();
                _onComplete?.Invoke();
                _isSettling = false;
                TLog.Info("[BattleSettlementCoordinator] Settlement complete.");
            }
        }

        /// <summary>
        /// 重置结算协调器状态以供下次使用。
        /// </summary>
        public void Reset()
        {
            _currentPhase = SettlementPhase.None;
            _onComplete = null;
            _result = default;
            _totalRounds = 0;
            _allUnits = null;
            _state = null;
            _isSettling = false;

            TLog.Info("[BattleSettlementCoordinator] State reset.");
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 处理奖励计算阶段：调用 BattleRewardSystem 计算奖励，保存到存档，触发事件。
        /// </summary>
        private void ProcessRewards()
        {
            _currentPhase = SettlementPhase.Rewards;

            TLog.Info("[BattleSettlementCoordinator] Calculating battle rewards...");

            // 调用 BattleRewardSystem 计算战斗奖励
            var rewards = BattleRewardSystem.CalculateBattleRewards(_result, _totalRounds, _allUnits);

            // 保存金币奖励到存档
            SaveGoldReward(rewards.TotalGold);

            TLog.Info($"[BattleSettlementCoordinator] Rewards calculated: Gold={rewards.TotalGold}, Characters={rewards.ExperiencePerCharacter?.Count ?? 0}");

            // 触发事件通知 UI 层显示结算信息
            OnRewardsCalculated?.Invoke(rewards);

            // 将经验值应用到角色数据
            if (rewards.ExperiencePerCharacter != null && _state != null && _state.Roster != null)
            {
                foreach (var kvp in rewards.ExperiencePerCharacter)
                {
                    var character = _state.Roster.FirstOrDefault(c => c.Id == kvp.Key);
                    if (character != null)
                    {
                        character.Experience += kvp.Value;
                        TLog.Info($"[BattleSettlementCoordinator] {character.DisplayName} gained {kvp.Value} exp (total: {character.Experience})");
                    }
                }
            }

            // Persist state after rewards applied
            PlayerAdventureStateStore.Save(_state);
        }

        /// <summary>
        /// 保存金币奖励到玩家冒险存档。
        /// </summary>
        private void SaveGoldReward(int gold)
        {
            if (gold <= 0)
                return;

            if (_state == null)
            {
                TLog.Warning("[BattleSettlementCoordinator] Cannot save gold reward: _state is null.");
                return;
            }

            _state.Gold += gold;

            TLog.Info($"[BattleSettlementCoordinator] Gold reward added to _state: +{gold}. TotalGold={_state.Gold}");
        }

        #endregion
    }
}