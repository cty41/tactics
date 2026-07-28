using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Battle;
using Tactics.Flow.Roguelike;
using Tactics.Roguelike;
using Tactics.RoguelikeMap;
using Tactics.Roster;
using Tactics.Runtime.Utilities;
using Tactics.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tactics.Flow
{
    /// <summary>
    /// New Run setup coordinator.
    /// Creates a fresh Pure Run adventure state, walks each roster character through the
    /// starting-skill selection UI, hands the customized state to the map flow via
    /// <see cref="PureRunPendingSetup"/> and opens the roguelike map.
    /// Falls back to the store-authored random starting skills when the UI is unavailable.
    /// </summary>
    public sealed class RunSetupFlowCoordinator
    {
        private static readonly RunSetupFlowCoordinator _instance = new RunSetupFlowCoordinator();
        public static RunSetupFlowCoordinator Instance => _instance;

        private bool _isRunning;

        private RunSetupFlowCoordinator() { }

        public async Task StartNewRunAsync()
        {
            if (_isRunning)
            {
                TLog.Warning("[RunSetupFlowCoordinator] StartNewRunAsync already in progress; ignoring re-entrant call.");
                return;
            }

            _isRunning = true;
            try
            {
                PureRunSessionStore.Clear();
                PureRunPendingSetup.Clear();

                int runSeed = RoguelikeMapGenerator.CreateRunSeed();
                var state = PlayerAdventureStateStore.CreatePureRunState(runSeed);

                bool customized = await RunSkillSelectionAsync(state);
                if (!customized)
                    TLog.Warning("[RunSetupFlowCoordinator] Skill selection UI unavailable; using random starting skills.");

                PureRunPendingSetup.SetPending(state);
                UIManager.Instance.Hide(UIManager.UIId.Home);
                await RoguelikeFlowCoordinator.Instance.OpenMapAsync();
            }
            finally
            {
                _isRunning = false;
            }
        }

        private static async Task<bool> RunSkillSelectionAsync(PlayerAdventureState state)
        {
            await UIManager.Instance.ShowAsync(UIManager.UIId.SkillSelection);
            await Task.Yield(); // warmup 2 frames, same convention as BattleSettlementFlow
            await Task.Yield();

            var controller = FindSkillSelectionController();
            if (controller == null)
            {
                UIManager.Instance.Hide(UIManager.UIId.SkillSelection);
                return false;
            }

            var roster = state.Roster.Where(c => c != null).ToList();
            for (int i = 0; i < roster.Count; i++)
            {
                var character = roster[i];
                var branchIds = PureRunAbilityCatalog.GetStartingBranchSkillIds(character.RoleType);
                var options = branchIds
                    .Select(id => PureRunAbilityCatalog.TryGet(id, out var def) ? def.CreateOffer(1) : null)
                    .Where(offer => offer != null)
                    .ToList();
                if (options.Count < 3) continue;

                controller.SetHeader($"选择初始技能 — {character.DisplayName}", $"角色 {i + 1} / {roster.Count}");
                // Key: pass null so CurrentSkillsContainer stays hidden and replace mode
                // can never trigger (the UXML default subtitle is already overridden by SetHeader).
                controller.SetCharacter(null);
                controller.SetSkillOptions(options);

                int defaultIndex = branchIds.ToList().IndexOf(character.StartingBranchSkillId);
                if (defaultIndex >= 0) controller.OnSkillSelected(defaultIndex);

                string chosen = await WaitForConfirmAsync(controller);
                if (chosen != null)
                    PlayerAdventureStateStore.ApplyStartingBranchSkill(character, chosen);
            }

            UIManager.Instance.Hide(UIManager.UIId.SkillSelection);
            return true;
        }

        private static Task<string> WaitForConfirmAsync(SkillSelectionUIController controller)
        {
            var tcs = new TaskCompletionSource<string>();
            System.Action<string, int?> handler = null;
            handler = (skillId, _) =>
            {
                controller.OnSkillConfirmed -= handler;
                tcs.TrySetResult(skillId);
            };
            controller.OnSkillConfirmed += handler;
            return tcs.Task;
        }

        private static SkillSelectionUIController FindSkillSelectionController()
        {
            string uiName = UIManager.UIId.SkillSelection.ToString();
            foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (doc.gameObject.name == uiName)
                {
                    var c = doc.GetComponent<SkillSelectionUIController>();
                    if (c != null) return c;
                }
            }
            return Object.FindFirstObjectByType<SkillSelectionUIController>();
        }
    }
}
