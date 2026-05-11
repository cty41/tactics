using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Roster;
using Tactics.Runtime.Utilities;
using Tactics.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// 战斗结算 UI 流程管理器（单例）。
    /// 订阅 <see cref="BattleSettlementCoordinator"/> 事件，驱动结算 UI 流程：
    /// 显示战斗奖励 → 处理角色升级 → 属性加点 → 技能选择 → 完成。
    /// </summary>
    public sealed class BattleSettlementFlow
    {
        private static readonly BattleSettlementFlow _instance = new BattleSettlementFlow();
        public static BattleSettlementFlow Instance => _instance;

        private BattleSettlementCoordinator _coordinator;
        private PlayerAdventureState _state;
        private Action _onComplete;

        private List<CharacterDefinition> _characterQueue;
        private int _currentCharacterIndex;

        private BattleRewardSystem.BattleRewards _pendingRewards;
        private bool _pendingIsVictory;

        private BattleSettlementFlow() { }

        /// <summary>
        /// 订阅结算协调器事件，开始监听结算流程。
        /// </summary>
        /// <param name="coordinator">战斗结算协调器。</param>
        /// <param name="state">玩家冒险状态。</param>
        public void Subscribe(BattleSettlementCoordinator coordinator, PlayerAdventureState state)
        {
            if (coordinator == null)
            {
                TLog.Warning("[BattleSettlementFlow] Subscribe called with null coordinator.");
                return;
            }

            Unsubscribe();

            _coordinator = coordinator;
            _state = state;

            _coordinator.OnRewardsCalculated += OnRewardsCalculated;
            _coordinator.OnSettlementComplete += OnSettlementComplete;

            TLog.Info("[BattleSettlementFlow] Subscribed to BattleSettlementCoordinator events.");
        }

        /// <summary>
        /// 取消订阅结算协调器事件。
        /// </summary>
        public void Unsubscribe()
        {
            if (_coordinator != null)
            {
                _coordinator.OnRewardsCalculated -= OnRewardsCalculated;
                _coordinator.OnSettlementComplete -= OnSettlementComplete;
                _coordinator = null;
            }

            _state = null;
            _onComplete = null;
            _characterQueue = null;
            _currentCharacterIndex = 0;

            TLog.Info("[BattleSettlementFlow] Unsubscribed from BattleSettlementCoordinator events.");
        }

        #region Event Handlers

        private void OnRewardsCalculated(BattleRewardSystem.BattleRewards rewards)
        {
            TLog.Info("[BattleSettlementFlow] OnRewardsCalculated received. Showing BattleSettlement UI.");

            _pendingRewards = rewards;
            _pendingIsVictory = _coordinator != null
                && _coordinator.CurrentPhase != SettlementPhase.None;

            _ = ShowBattleSettlementAsync();
        }

        private void OnSettlementComplete()
        {
            TLog.Info("[BattleSettlementFlow] Settlement complete.");
        }

        #endregion

        #region UI Flow

        private async Task ShowBattleSettlementAsync()
        {
            await UIManager.Instance.ShowAsync(UIManager.UIId.BattleSettlement);

            var controller = FindController<BattleSettlementUIController>(UIManager.UIId.BattleSettlement);
            if (controller == null)
            {
                TLog.Error("[BattleSettlementFlow] BattleSettlementUIController not found.");
                return;
            }

            controller.SetBattleResult(_pendingRewards, _pendingIsVictory);
            controller.OnContinue += OnBattleSettlementContinue;
        }

        private void OnBattleSettlementContinue()
        {
            TLog.Info("[BattleSettlementFlow] BattleSettlement continue clicked.");

            var controller = FindController<BattleSettlementUIController>(UIManager.UIId.BattleSettlement);
            if (controller != null)
                controller.OnContinue -= OnBattleSettlementContinue;

            UIManager.Instance.Hide(UIManager.UIId.BattleSettlement);

            // 初始化角色队列（活着的角色）
            InitializeCharacterQueue();
            _currentCharacterIndex = 0;

            // 开始处理角色升级流程
            ProcessNextCharacter();
        }

        private void InitializeCharacterQueue()
        {
            _characterQueue = new List<CharacterDefinition>();

            if (_state?.Roster == null)
            {
                TLog.Warning("[BattleSettlementFlow] PlayerAdventureState or Roster is null.");
                return;
            }

            foreach (var character in _state.Roster)
            {
                if (character == null)
                    continue;

                _characterQueue.Add(character);
            }

            TLog.Info($"[BattleSettlementFlow] Initialized character queue with {_characterQueue.Count} alive characters.");
        }

        private void ProcessNextCharacter()
        {
            if (_characterQueue == null || _currentCharacterIndex >= _characterQueue.Count)
            {
                TLog.Info("[BattleSettlementFlow] All characters processed. Settlement flow complete.");
                _onComplete?.Invoke();
                return;
            }

            var character = _characterQueue[_currentCharacterIndex];
            TLog.Info($"[BattleSettlementFlow] Processing character: {character.DisplayName} (Level {character.Level})");

            bool hasLeveledUp = false;
            if (_coordinator != null)
            {
                hasLeveledUp = _coordinator.ProcessLevelUp(character);
            }

            if (hasLeveledUp)
            {
                _ = ShowAttributeAllocationAsync(character);
            }
            else
            {
                _currentCharacterIndex++;
                ProcessNextCharacter();
            }
        }

        private async Task ShowAttributeAllocationAsync(CharacterDefinition character)
        {
            await UIManager.Instance.ShowAsync(UIManager.UIId.AttributeAllocation);

            var controller = FindController<AttributeAllocationUIController>(UIManager.UIId.AttributeAllocation);
            if (controller == null)
            {
                TLog.Error("[BattleSettlementFlow] AttributeAllocationUIController not found.");
                _currentCharacterIndex++;
                ProcessNextCharacter();
                return;
            }

            controller.SetCharacter(character);
            _ = WaitForAttributeAllocationCloseAsync(character);
        }

        private async Task WaitForAttributeAllocationCloseAsync(CharacterDefinition character)
        {
            int maxWaitFrames = 6000;
            int frameCount = 0;

            while (frameCount < maxWaitFrames)
            {
                await Task.Yield();
                frameCount++;

                if (!IsUiVisible(UIManager.UIId.AttributeAllocation))
                {
                    break;
                }
            }

            TLog.Info($"[BattleSettlementFlow] AttributeAllocation closed for {character.DisplayName}.");

            bool shouldShowSkill = SkillSystem.ShouldShowSkillSelection(character, character.Level);
            if (shouldShowSkill)
            {
                _ = ShowSkillSelectionAsync(character);
            }
            else
            {
                _currentCharacterIndex++;
                ProcessNextCharacter();
            }
        }

        private async Task ShowSkillSelectionAsync(CharacterDefinition character)
        {
            await UIManager.Instance.ShowAsync(UIManager.UIId.SkillSelection);

            var controller = FindController<SkillSelectionUIController>(UIManager.UIId.SkillSelection);
            if (controller == null)
            {
                TLog.Error("[BattleSettlementFlow] SkillSelectionUIController not found.");
                _currentCharacterIndex++;
                ProcessNextCharacter();
                return;
            }

            controller.SetCharacter(character);

            var options = GenerateSkillOptions(character);
            if (options.Count > 0)
            {
                controller.SetSkillOptions(options);
            }

            controller.OnSkillConfirmed += OnSkillConfirmed;
        }

        private void OnSkillConfirmed(string skillId, int? replaceIndex)
        {
            TLog.Info($"[BattleSettlementFlow] Skill confirmed: {skillId}, replaceIndex={replaceIndex}");

            var controller = FindController<SkillSelectionUIController>(UIManager.UIId.SkillSelection);
            if (controller != null)
                controller.OnSkillConfirmed -= OnSkillConfirmed;

            UIManager.Instance.Hide(UIManager.UIId.SkillSelection);

            _currentCharacterIndex++;
            ProcessNextCharacter();
        }

        #endregion

        #region Helpers

        private List<SkillDefinition> GenerateSkillOptions(CharacterDefinition character)
        {
            var options = new List<SkillDefinition>();

            if (character == null)
                return options;

            bool isNewSkill = SkillSystem.IsNewSkillLevel(character.Level);
            bool isUpgrade = SkillSystem.IsUpgradeSkillLevel(character.Level);

            if (isNewSkill)
            {
                var activeSlot = SkillSystem.GetSkillSlotStatus(character, Tactics.Roster.SkillType.Active);
                var passiveSlot = SkillSystem.GetSkillSlotStatus(character, Tactics.Roster.SkillType.Passive);

                SkillType targetType;
                if (activeSlot.Remaining > 0)
                    targetType = SkillType.Active;
                else if (passiveSlot.Remaining > 0)
                    targetType = SkillType.Passive;
                else
                    targetType = SkillType.Active;

                options = SkillDatabase.GetRandomSkillsForSelection(
                    character.RoleType,
                    targetType,
                    1,
                    3,
                    character.LearnedSkills);

                if (options.Count < 3)
                {
                    var otherType = targetType == SkillType.Active ? SkillType.Passive : SkillType.Active;
                    var additional = SkillDatabase.GetRandomSkillsForSelection(
                        character.RoleType,
                        otherType,
                        1,
                        3 - options.Count,
                        character.LearnedSkills);
                    options.AddRange(additional);
                }
            }
            else if (isUpgrade)
            {
                var upgradeable = SkillDatabase.GetUpgradeableSkills(character);
                if (upgradeable.Count > 0)
                {
                    var rng = new System.Random();
                    var shuffled = upgradeable.OrderBy(_ => rng.Next()).ToList();
                    options = shuffled.Take(3).ToList();
                }
            }

            TLog.Info($"[BattleSettlementFlow] Generated {options.Count} skill options for {character.DisplayName} (Level {character.Level}).");
            return options;
        }

        private T FindController<T>(UIManager.UIId uiId) where T : UIControllerBase
        {
            var uiDoc = GetUiDocument(uiId);
            if (uiDoc != null)
            {
                var controller = uiDoc.GetComponent<T>();
                if (controller != null)
                    return controller;
            }

            var controllers = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            if (controllers.Length > 0)
                return controllers[0];

            return null;
        }

        private UIDocument GetUiDocument(UIManager.UIId uiId)
        {
            string uiName = uiId.ToString();
            var uiDocs = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var doc in uiDocs)
            {
                if (doc.gameObject.name.Contains(uiName))
                    return doc;
            }
            return null;
        }

        private bool IsUiVisible(UIManager.UIId uiId)
        {
            var uiDoc = GetUiDocument(uiId);
            if (uiDoc == null)
                return false;

            if (uiDoc.rootVisualElement == null)
                return false;

            return uiDoc.rootVisualElement.style.display != DisplayStyle.None
                && uiDoc.gameObject.activeInHierarchy;
        }

        #endregion
    }
}
