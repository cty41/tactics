using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Tactics.Common.Controllers;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Roster;
using Tactics.Runtime.BattleLog;
using UnityEngine;

namespace Tactics.Consumables
{
    /// <summary>
    /// Builds transient SkillGraph abilities from consumable templates.
    /// </summary>
    public static class ConsumableAbilityFactory
    {
        public static ConsumableBattleAbility Create(
            IUnit owner,
            ConsumableInstance instance,
            string characterId)
        {
            if (owner == null || instance == null)
                return null;

            var definition = ConsumableDatabase.GetById(instance.DefinitionId);
            if (definition == null)
                return null;

            var graph = CreateGraph(definition);
            var config = SkillGraphAbilityConfig.CreateRuntime(
                $"{definition.DisplayName} [{instance.RemainingCharges}/{instance.MaxCharges}]",
                graph,
                definition.MaxRange);
            var usePolicy = new ConsumableUsePolicy(
                owner,
                instance.InstanceId,
                characterId,
                definition);
            return new ConsumableBattleAbility(
                owner,
                config,
                usePolicy,
                definition,
                instance.InstanceId,
                characterId);
        }

        private static SkillGraphAsset CreateGraph(ConsumableDefinition definition)
        {
            var graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
            graph.DisplayName = definition.Id;
            graph.Tags = new[] { "consumable", definition.EffectKind.ToString() };

            var start = graph.AddNode(SkillGraphNodeType.Start, Vector2.zero);
            SkillGraphNodeRecord selection;
            if (definition.TargetMode == ConsumableTargetMode.AllyIncludingSelf)
            {
                var selectAlly = (SelectAllyNodeRecord)graph.AddNode(
                    SkillGraphNodeType.SelectAlly,
                    new Vector2(180f, 0f));
                selectAlly.MaxRange = definition.MaxRange;
                selectAlly.IncludeSelf = true;
                selection = selectAlly;
            }
            else
            {
                selection = graph.AddNode(SkillGraphNodeType.SelectSelf, new Vector2(180f, 0f));
            }

            SkillGraphNodeRecord effect = definition.EffectKind switch
            {
                ConsumableEffectKind.RestoreMana => CreateManaNode(graph, definition.Magnitude),
                ConsumableEffectKind.RemoveHarmfulBuffs => graph.AddNode(
                    SkillGraphNodeType.RemoveHarmfulBuffs,
                    new Vector2(360f, 0f)),
                _ => CreateHealNode(graph, definition.Magnitude)
            };
            var finish = graph.AddNode(SkillGraphNodeType.Finish, new Vector2(540f, 0f));

            graph.AddEdge(start.NodeId, selection.NodeId);
            graph.AddEdge(selection.NodeId, effect.NodeId);
            graph.AddEdge(effect.NodeId, finish.NodeId);
            return graph;
        }

        private static SkillGraphNodeRecord CreateHealNode(SkillGraphAsset graph, float magnitude)
        {
            var node = (ApplyHealNodeRecord)graph.AddNode(SkillGraphNodeType.ApplyHeal, new Vector2(360f, 0f));
            node.HealAmount = magnitude;
            return node;
        }

        private static SkillGraphNodeRecord CreateManaNode(SkillGraphAsset graph, float magnitude)
        {
            var node = (ApplyManaNodeRecord)graph.AddNode(SkillGraphNodeType.ApplyMana, new Vector2(360f, 0f));
            node.ManaAmount = magnitude;
            return node;
        }
    }

    /// <summary>
    /// Limits a character to one consumable use in each player turn and commits durability
    /// only after the SkillGraph completes.
    /// </summary>
    internal sealed class ConsumableUsePolicy : ISkillGraphUsePolicy
    {
        private static readonly HashSet<string> Uses = new HashSet<string>(StringComparer.Ordinal);
        private readonly IUnit _owner;
        private readonly string _instanceId;
        private readonly string _characterId;
        private readonly ConsumableDefinition _definition;
        private IGridController _gridController;

        public ConsumableUsePolicy(
            IUnit owner,
            string instanceId,
            string characterId,
            ConsumableDefinition definition)
        {
            _owner = owner;
            _instanceId = instanceId;
            _characterId = characterId;
            _definition = definition;
        }

        public event Action UseCommitted;

        public int RemainingCharges
        {
            get
            {
                var state = PlayerAdventureStateStore.LoadRepairAndSave();
                return state?.ConsumableInstances?
                    .FirstOrDefault(item => item.InstanceId == _instanceId)?
                    .RemainingCharges ?? 0;
            }
        }

        public string DisplayName
        {
            get
            {
                return $"{_definition.DisplayName} [{RemainingCharges}/{_definition.MaxCharges}]";
            }
        }

        public bool CanPerform(IGridController gridController)
        {
            _gridController = gridController;
            if (gridController?.TurnContext.CurrentPlayer == null ||
                gridController.TurnContext.CurrentPlayer.PlayerNumber != _owner.PlayerNumber)
                return false;

            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            var instance = state?.ConsumableInstances?.FirstOrDefault(item => item.InstanceId == _instanceId);
            var character = state?.Roster?.FirstOrDefault(candidate => candidate?.Id == _characterId);
            return instance != null &&
                   instance.RemainingCharges > 0 &&
                   character?.CarriedConsumableInstanceId == _instanceId &&
                   !Uses.Contains(BuildTurnKey(gridController));
        }

        public void CommitCompletedUse(SkillExecutionContext context)
        {
            var state = PlayerAdventureStateStore.LoadRepairAndSave();
            if (!CharacterLoadoutService.TryCommitConsumableUse(state, _instanceId))
                return;

            PlayerAdventureStateStore.Save(state);

            if (_gridController != null)
                Uses.Add(BuildTurnKey(_gridController));

            if (_definition.EffectKind == ConsumableEffectKind.RemoveHarmfulBuffs && TBattleLog.IsBattleActive)
            {
                TBattleLog.Log(new CleanseLogData
                {
                    Source = GetUnitName(_owner),
                    ItemName = _definition.DisplayName,
                    RemovedCount = context?.GetBlackboard<int>("RemovedHarmfulBuffCount", 0) ?? 0
                });
            }

            UseCommitted?.Invoke();
        }

        private string BuildTurnKey(IGridController gridController)
        {
            return $"{RuntimeHelpers.GetHashCode(gridController)}:{gridController.CurrentRound}:{_owner.PlayerNumber}:{_owner.UnitID}";
        }

        private static string GetUnitName(IUnit unit)
        {
            if (unit is INamedUnit named && !string.IsNullOrWhiteSpace(named.UnitName))
                return named.UnitName;

            return unit == null ? "Unknown" : $"Unit_{unit.UnitID}";
        }
    }

    /// <summary>
    /// Grants deterministic instances from named acquisition pools.
    /// </summary>
    public static class ConsumableRewardService
    {
        public static ConsumableInstance GrantFromPool(PlayerAdventureState state, string poolId, int seed)
        {
            if (state == null)
                return null;

            var definition = ConsumableDatabase.Roll(poolId, seed);
            if (definition == null)
                return null;

            state.ConsumableInstances ??= new List<ConsumableInstance>();
            var instance = ConsumableInstance.Create(definition);
            state.ConsumableInstances.Add(instance);
            return instance;
        }
    }
}
