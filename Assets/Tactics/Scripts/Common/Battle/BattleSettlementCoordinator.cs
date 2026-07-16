using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Players;
using Tactics.RoguelikeMap.Interaction;
using Tactics.Common.Units;
using Tactics.Consumables;
using Tactics.Roguelike;
using Tactics.RoguelikeMap;
using Tactics.RoguelikeMap.Economy;
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
        private RewardResult _currentRewardResult;
        private bool _isPlayerVictory;

        #endregion

        #region Events

        /// <summary>当奖励计算完成时触发，携带奖励数据供 UI 显示。</summary>
        public event Action<BattleRewardSystem.BattleRewards> OnRewardsCalculated;

        /// <summary>当统一结果结构生成后触发，供 Roguelike 地图层后续接入。</summary>
        public event Action<RewardResult> OnRewardResultGenerated;

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

        /// <summary>当前战斗结算对应的统一结果结构。</summary>
        public RewardResult CurrentRewardResult => _currentRewardResult;

        /// <summary>Whether the human player won the battle being settled.</summary>
        public bool IsPlayerVictory => _isPlayerVictory;

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
            _isPlayerVictory = result.Winners != null &&
                               result.Winners.Any(player => player != null && player.PlayerType == PlayerType.HumanPlayer);
            _onComplete = onComplete;
            _currentRewardResult = null;
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

            if (_state?.IsPureRun == true)
            {
                if (!_isPlayerVictory)
                    return false;

                bool granted = PureRunProgression.GrantLevel(character);
                if (granted)
                    OnCharacterLevelUp?.Invoke(character);
                return granted;
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
            _currentRewardResult = null;
            _isPlayerVictory = false;
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

            // 生成统一结果结构，供地图层和节点语义统一消费
            AppendPureRunConsumableDrop(ref rewards);
            _currentRewardResult = rewards.ToRewardResult();

            // 保存金币奖励到存档
            SaveGoldReward(rewards.TotalGold);

            TLog.Info($"[BattleSettlementCoordinator] Rewards calculated: Gold={rewards.TotalGold}, Characters={rewards.ExperiencePerCharacter?.Count ?? 0}");

            // 触发事件通知 UI 层显示结算信息
            OnRewardsCalculated?.Invoke(rewards);
            OnRewardResultGenerated?.Invoke(_currentRewardResult);

            // 将经验值应用到角色数据
            if (_state?.IsPureRun != true &&
                rewards.ExperiencePerCharacter != null &&
                _state?.Roster != null)
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
        }

        private void AppendPureRunConsumableDrop(ref BattleRewardSystem.BattleRewards rewards)
        {
            if (!_isPlayerVictory || _state?.IsPureRun != true)
                return;

            string nodeId = RoguelikeMapRuntimeState.PendingBattleNodeId;
            var node = RoguelikeMapRuntimeState.CurrentMap?.GetNode(nodeId);
            float chance = node?.nodeType switch
            {
                RoguelikeNodeType.MinorEnemy => 0.25f,
                RoguelikeNodeType.EliteEnemy => 0.30f,
                _ => 0f
            };
            if (chance <= 0f)
                return;

            int chanceSeed = RoguelikeMapRuntimeState.DeriveSeed(_state.RunSeed, $"battle-drop:{nodeId}");
            if (new Random(chanceSeed).NextDouble() >= chance)
                return;

            int itemSeed = RoguelikeMapRuntimeState.DeriveSeed(_state.RunSeed, $"battle-drop-item:{nodeId}");
            var definition = ConsumableDatabase.Roll("consumables", itemSeed);
            if (definition == null)
                return;

            rewards.ItemIds ??= new List<string>();
            rewards.ItemIds.Add(definition.Id);
            rewards.ToRewardResult().ApplyItemsToState(_state);
            TLog.Info($"[BattleSettlementCoordinator] Consumable drop: {definition.DisplayName}.");
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

            RunGoldManager.Instance.SyncFromState(_state);
            RunGoldManager.Instance.AddGold(gold);
            RunGoldManager.Instance.SyncToState(_state);

            TLog.Info($"[BattleSettlementCoordinator] Gold reward added to _state: +{gold}. TotalGold={_state.Gold}");
        }

        #endregion
    }

    /// <summary>
    /// Pure-run growth rules that can be consumed by settlement, commands, and tests without UI dependencies.
    /// </summary>
    public static class PureRunProgression
    {
        public const int SkillChoiceCount = 3;

        /// <summary>
        /// Selects the lowest-level living active character. Active-party order is the stable tie-breaker.
        /// </summary>
        public static CharacterDefinition SelectLowestLevelLivingCharacter(PlayerAdventureState state)
        {
            if (state?.Roster == null || state.ActivePartyCharacterIds == null)
                return null;

            var rosterById = state.Roster
                .Where(character => character != null && !string.IsNullOrEmpty(character.Id))
                .GroupBy(character => character.Id)
                .ToDictionary(group => group.Key, group => group.First());

            CharacterDefinition selected = null;
            foreach (string characterId in state.ActivePartyCharacterIds)
            {
                if (!rosterById.TryGetValue(characterId, out var candidate) || candidate.IsDead)
                    continue;

                if (selected == null || candidate.Level < selected.Level)
                    selected = candidate;
            }

            return selected;
        }

        /// <summary>Grants one victory level to the selected pure-run character.</summary>
        public static CharacterDefinition GrantVictoryLevel(PlayerAdventureState state)
        {
            if (state?.IsPureRun != true)
                return null;

            var character = SelectLowestLevelLivingCharacter(state);
            return GrantLevel(character) ? character : null;
        }

        public static bool GrantLevel(CharacterDefinition character)
        {
            if (character == null || character.Level >= SkillSystem.MaxCharacterLevel)
                return false;

            character.Level++;
            character.AttributePoints++;
            return true;
        }

        /// <summary>
        /// Builds deterministic legal first-slice choices and reserves slot zero for the one-time
        /// starting-branch advanced guarantee when its base attribute reaches seven.
        /// </summary>
        public static List<SkillDefinition> BuildSkillChoices(
            CharacterDefinition character,
            int runSeed,
            int offerOrdinal,
            int count = SkillChoiceCount)
        {
            if (character == null || count <= 0)
                return new List<SkillDefinition>();

            var legal = FirstSliceSkillCatalog.All
                .Where(skill => skill.RoleType == character.RoleType && SkillSystem.CanLearnSkill(character, skill))
                .OrderBy(skill => skill.Id, StringComparer.Ordinal)
                .ToList();

            SkillDefinition guaranteed = null;
            TryGetGuaranteedAdvancedSkill(character, out guaranteed);
            if (guaranteed != null)
                legal.RemoveAll(skill => skill.Id == guaranteed.Id);

            int randomSeed = Tactics.Roguelike.RoguelikeMapRuntimeState.DeriveSeed(
                runSeed,
                $"skill-offer-{character.Id}",
                offerOrdinal);
            Shuffle(legal, new Random(randomSeed));

            var result = new List<SkillDefinition>();
            if (guaranteed != null)
                result.Add(guaranteed);

            result.AddRange(legal.Take(Math.Max(0, count - result.Count)));
            return result;
        }

        public static bool TryGetGuaranteedAdvancedSkill(
            CharacterDefinition character,
            out SkillDefinition advancedSkill)
        {
            advancedSkill = null;
            if (character == null ||
                character.HasConsumedStartingAdvancedGuarantee ||
                string.IsNullOrEmpty(character.StartingBranchSkillId) ||
                HasLearnedAdvancedSkill(character))
            {
                return false;
            }

            advancedSkill = FindStartingBranchAdvancedSkill(character);
            if (advancedSkill == null || !SkillSystem.CanLearnSkill(character, advancedSkill))
            {
                advancedSkill = null;
                return false;
            }

            return true;
        }

        /// <summary>Consumes the guarantee only when the corresponding advanced skill was actually offered.</summary>
        public static bool MarkAdvancedGuaranteeConsumed(
            CharacterDefinition character,
            IEnumerable<SkillDefinition> offeredSkills)
        {
            if (character == null || character.HasConsumedStartingAdvancedGuarantee || offeredSkills == null)
                return false;

            var advancedSkill = FindStartingBranchAdvancedSkill(character);
            if (advancedSkill == null || !offeredSkills.Any(skill => skill?.Id == advancedSkill.Id))
                return false;

            character.HasConsumedStartingAdvancedGuarantee = true;
            return true;
        }

        private static SkillDefinition FindStartingBranchAdvancedSkill(CharacterDefinition character)
        {
            return FirstSliceSkillCatalog.All.FirstOrDefault(skill =>
                skill.RoleType == character.RoleType &&
                string.Equals(skill.PrerequisiteSkillId, character.StartingBranchSkillId, StringComparison.Ordinal));
        }

        private static bool HasLearnedAdvancedSkill(CharacterDefinition character)
        {
            if (character.LearnedSkills == null)
                return false;

            foreach (var learnedSkill in character.LearnedSkills)
            {
                if (learnedSkill != null &&
                    FirstSliceSkillCatalog.TryGet(learnedSkill.SkillId, out var definition) &&
                    !string.IsNullOrEmpty(definition.PrerequisiteSkillId))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Shuffle<T>(IList<T> values, Random random)
        {
            for (int index = values.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
            }
        }
    }
}
