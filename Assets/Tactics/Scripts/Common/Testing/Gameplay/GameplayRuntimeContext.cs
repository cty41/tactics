using System;
using System.Collections.Generic;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Battle;
using Tactics.Common.Battle.Runtime;
using Tactics.Common.Controllers.GameResolvers;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Skills.Graph.Testing;
using Tactics.Common.Units;
using Tactics.Common.Cells;
using Tactics.Common.Units.Abilities;
using Tactics.RoguelikeMap;
using Tactics.RoguelikeMap.Events;
using Tactics.Roster;
using Tactics.UI;
using UnityEngine;

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
        public Dictionary<string, AiBrainAsset> AiBrainAssets { get; } = new(StringComparer.OrdinalIgnoreCase);

        // Interactable Corpse 测试支持：cell alias -> 是否存在 interactable corpse
        public Dictionary<string, bool> InteractableCorpsesByCell { get; } = new(StringComparer.OrdinalIgnoreCase);
        public AiDecisionLog LastAiDecisionLog { get; set; }

        // AI 执行快照（用于验证 AI 是否真正产出了效果）
        public AiExecutionSnapshot LastAiSnapshot { get; set; }
        public AiExecutionSnapshot PreviousAiSnapshot { get; set; }
        public AiTurnResultSnapshot LastAiTurnResult { get; set; }

        // Map/Roguelike 相关
        public global::Tactics.RoguelikeMap.RoguelikeMap RoguelikeMap { get; set; }
        public string CurrentNodeId { get; set; }
        public RoguelikeEvent CurrentEvent { get; set; }
        public bool EventCompleted { get; set; }
        public PlayerAdventureState CurrentAdventureState { get; set; }

        // UI 相关
        public UIManager.UIId? CurrentUiId { get; set; }

        // 真实资产模式
        public bool UseRealAssets { get; set; }

        // 战斗测试配置模式
        public bool UseBattleTestMode { get; set; }
        public string TestPartyConfigPath { get; set; }
        public string TestEncounterConfigPath { get; set; }

        // Deterministic map test settings.
        public int? RunSeed { get; set; }
        public bool StrictAsset { get; set; }

        // BattleEnded 订阅追踪（防止重复订阅泄漏）
        public BattleController SubscribedBattleController { get; set; }
        public Action<GameResult> BattleEndedHandler { get; set; }

        /// <summary>
        /// 运行时作用域，管理所有异步操作的生命周期。
        /// </summary>
        public IBattleRuntimeScope RuntimeScope { get; set; }

        public void Dispose()
        {
            // 0. 解绑 BattleEnded 订阅（防止旧 context 继续收到结果写入）
            if (SubscribedBattleController != null && BattleEndedHandler != null)
            {
                SubscribedBattleController.BattleEnded -= BattleEndedHandler;
                SubscribedBattleController = null;
                BattleEndedHandler = null;
            }

            // 1. 清除每个 Unit 上的 Buff（Buff 持有对 Unit 和 BuffConfig 的引用）
            foreach (var unit in Units.Values)
            {
                if (unit is Unit concreteUnit)
                {
                    concreteUnit.BuffComponent?.OnUnitDestroyed();
                }
            }

            // 2. 先清除所有对 MonoBehaviour 的引用（在 GameObject 销毁之前）
            Units.Clear();
            Cells.Clear();
            SkillAbilities.Clear();

            // 3. 销毁 ScriptableObject 实例
            foreach (var config in SkillAbilityConfigs.Values)
            {
                if (config != null)
                    UnityEngine.Object.Destroy(config);
            }
            SkillAbilityConfigs.Clear();

            foreach (var graph in SkillGraphs.Values)
            {
                if (graph != null)
                    UnityEngine.Object.Destroy(graph);
            }
            SkillGraphs.Clear();

            // 4. 销毁 GameObjects（通过 SkillWorld）
            SkillWorld?.Dispose();
            SkillWorld = null;

            // 5. 清空其余引用
            AiBrainAssets.Clear();
            LastSkillResult = null;
            LastStepMessage = null;
            BattleController = null;
            LastBattleResult = null;
            LastAiDecisionLog = null;
            LastAiSnapshot = null;
            PreviousAiSnapshot = null;
            LastAiTurnResult = null;
            RuntimeScope = null;

            // 6. 清空 Map 相关
            RoguelikeMap = null;
            CurrentNodeId = null;
            CurrentEvent = null;
            EventCompleted = false;
            CurrentAdventureState = null;

            // 7. 清空 UI 相关
            CurrentUiId = null;
        }
    }
}
