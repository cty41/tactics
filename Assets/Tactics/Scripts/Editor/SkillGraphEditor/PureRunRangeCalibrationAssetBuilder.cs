using System;
using System.Collections.Generic;
using System.Linq;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Abilities;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.SkillGraphEditor
{
    /// <summary>
    /// Owns the published ability and AI ranges calibrated for the fixed 10x10 battle board.
    /// </summary>
    public static class PureRunRangeCalibrationAssetBuilder
    {
        public const int StandardPlayerRange = 4;
        public const int ExtendedPlayerRange = 5;
        public const int FireDemonSummonRange = 3;
        public const int CorpseSelectionRange = 999;
        public const int ChargeRange = 3;
        public const int MonsterRangedMinimumRange = 2;
        public const int MonsterRangedMaximumRange = 4;
        public const int AreaBlastRange = 3;
        public const int AreaBlastRadius = 2;
        public const int PreferredMinimumRange = 2;
        public const int PreferredMaximumRange = 3;
        public const int RangedPreferredMaximumRange = 4;

        private const string ConfigRoot =
            "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs";
        private const string BrainRoot = "Assets/Tactics/AI/Encounters";

        private static readonly string[] StandardPrimaryConfigs =
        {
            "IceBolt_Graph_Ability",
            "IceBolt_Lv2_Graph_Ability",
            "IceBolt_Lv3_Graph_Ability",
            "Lightning_Graph_Ability",
            "Lightning_Lv2_Graph_Ability",
            "Lightning_Lv3_Graph_Ability",
            "Curse_Graph_Ability",
            "BoneSpear_Graph_Ability",
            "BoneSpear_Lv2_Graph_Ability",
            "FearCurse_Graph_Ability"
        };

        private static readonly string[] StandardPointConfigs =
        {
            "Curse_Lv2_Graph_Ability",
            "Curse_Lv3_Graph_Ability",
            "BoneSpear_Lv3_Graph_Ability",
            "FearCurse_Lv2_Graph_Ability"
        };

        private static readonly string[] ExtendedPrimaryConfigs =
        {
            "PoisonSpear_Graph_Ability",
            "PoisonSpear_Lv2_Graph_Ability",
            "PoisonSpear_Lv3_Graph_Ability"
        };

        private static readonly string[] ExtendedPointConfigs =
        {
            "RecoverSpear_Graph_Ability",
            "RecoverSpear_Lv2_Graph_Ability"
        };

        [MenuItem("Tactics/Tools/Pure Run/Rebuild Fixed Board Range Calibration")]
        public static void RebuildAll()
        {
            var mutations = new List<Action>();
            var dirtyObjects = new HashSet<UnityEngine.Object>();

            foreach (string name in StandardPrimaryConfigs)
                PrepareSelectorRange<SelectPrimaryTargetNodeRecord>(name, StandardPlayerRange, mutations, dirtyObjects);
            foreach (string name in StandardPointConfigs)
                PrepareSelectorRange<SelectTargetPointNodeRecord>(name, StandardPlayerRange, mutations, dirtyObjects);
            foreach (string name in ExtendedPrimaryConfigs)
                PrepareSelectorRange<SelectPrimaryTargetNodeRecord>(name, ExtendedPlayerRange, mutations, dirtyObjects);
            foreach (string name in ExtendedPointConfigs)
                PrepareSelectorRange<SelectTargetPointNodeRecord>(name, ExtendedPlayerRange, mutations, dirtyObjects);

            PrepareSelectorRange<TeleportNodeRecord>("Teleport_Graph_Ability", StandardPlayerRange, mutations, dirtyObjects);
            PrepareSelectorRange<TeleportNodeRecord>("Teleport_Lv2_Graph_Ability", StandardPlayerRange, mutations, dirtyObjects);
            PrepareCharge(mutations, dirtyObjects);
            PrepareSelectorRange<SelectPrimaryTargetNodeRecord>(
                "RangedAttack_Graph_Ability", MonsterRangedMaximumRange, mutations, dirtyObjects,
                MonsterRangedMinimumRange);
            PrepareSelectorRange<SelectPrimaryTargetNodeRecord>(
                "HeavyShot_Graph_Ability", MonsterRangedMaximumRange, mutations, dirtyObjects);
            PrepareAreaBlast(mutations, dirtyObjects);
            PrepareBrain("RangedBrain", PreferredMinimumRange, RangedPreferredMaximumRange, mutations, dirtyObjects);
            PrepareBrain("AOEBrain", PreferredMinimumRange, PreferredMaximumRange, mutations, dirtyObjects);
            PrepareBrain("SupportBrain", PreferredMinimumRange, PreferredMaximumRange, mutations, dirtyObjects);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild fixed board range calibration");
            try
            {
                Undo.RecordObjects(dirtyObjects.ToArray(), "Rebuild fixed board range calibration");
                foreach (Action mutation in mutations)
                    mutation();
                foreach (UnityEngine.Object target in dirtyObjects)
                    EditorUtility.SetDirty(target);
                foreach (UnityEngine.Object target in dirtyObjects)
                    AssetDatabase.SaveAssetIfDirty(target);
                Undo.CollapseUndoOperations(undoGroup);
                TLog.Info("[PureRunRangeCalibration] Published ranges rebuilt for the fixed 10x10 board.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        private static void PrepareSelectorRange<TNode>(
            string configName,
            int maximumRange,
            ICollection<Action> mutations,
            ISet<UnityEngine.Object> dirtyObjects,
            int? minimumRange = null)
            where TNode : SkillGraphNodeRecord
        {
            SkillGraphAbilityConfig config = LoadConfig(configName);
            TNode node = config.SkillGraph.Nodes.OfType<TNode>().Single();
            dirtyObjects.Add(config);
            dirtyObjects.Add(config.SkillGraph);
            mutations.Add(() => SetConfigRange(config, maximumRange));
            mutations.Add(() => SetNodeRange(node, maximumRange, minimumRange));
        }

        private static void PrepareCharge(
            ICollection<Action> mutations,
            ISet<UnityEngine.Object> dirtyObjects)
        {
            SkillGraphAbilityConfig config = LoadConfig("ChargeStrike_Lv1_Ability");
            SelectPrimaryTargetNodeRecord selector = config.SkillGraph.Nodes
                .OfType<SelectPrimaryTargetNodeRecord>().Single();
            DashToTargetNodeRecord dash = config.SkillGraph.Nodes.OfType<DashToTargetNodeRecord>().Single();
            dirtyObjects.Add(config);
            dirtyObjects.Add(config.SkillGraph);
            mutations.Add(() => SetConfigRange(config, ChargeRange));
            mutations.Add(() => selector.MaxRange = ChargeRange);
            mutations.Add(() => dash.MaxRange = ChargeRange);
        }

        private static void PrepareAreaBlast(
            ICollection<Action> mutations,
            ISet<UnityEngine.Object> dirtyObjects)
        {
            SkillGraphAbilityConfig config = LoadConfig("AreaBlast_Lv1_Ability");
            SelectTargetPointNodeRecord selector = config.SkillGraph.Nodes
                .OfType<SelectTargetPointNodeRecord>().Single();
            CollectTargetsInAreaNodeRecord area = config.SkillGraph.Nodes
                .OfType<CollectTargetsInAreaNodeRecord>().Single();
            dirtyObjects.Add(config);
            dirtyObjects.Add(config.SkillGraph);
            mutations.Add(() => SetConfigRange(config, AreaBlastRange));
            mutations.Add(() => selector.MaxRange = AreaBlastRange);
            mutations.Add(() => area.Radius = AreaBlastRadius);
        }

        private static void PrepareBrain(
            string assetName,
            int minimumRange,
            int maximumRange,
            ICollection<Action> mutations,
            ISet<UnityEngine.Object> dirtyObjects)
        {
            string path = $"{BrainRoot}/{assetName}.asset";
            AiBrainAsset brain = AssetDatabase.LoadAssetAtPath<AiBrainAsset>(path);
            if (brain == null)
                throw new InvalidOperationException($"Required AI brain is missing: {path}");

            var serialized = new SerializedObject(brain);
            SerializedProperty minimum = RequireIntegerProperty(serialized, "_preferredMinimumRange", path);
            SerializedProperty maximum = RequireIntegerProperty(serialized, "_preferredMaximumRange", path);
            dirtyObjects.Add(brain);
            mutations.Add(() =>
            {
                serialized.Update();
                minimum.intValue = minimumRange;
                maximum.intValue = maximumRange;
                serialized.ApplyModifiedProperties();
            });
        }

        private static SkillGraphAbilityConfig LoadConfig(string assetName)
        {
            string path = $"{ConfigRoot}/{assetName}.asset";
            var config = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(path);
            if (config?.SkillGraph == null)
                throw new InvalidOperationException($"Required ability config or graph is missing: {path}");
            return config;
        }

        private static void SetConfigRange(SkillGraphAbilityConfig config, int range)
        {
            var serialized = new SerializedObject(config);
            SerializedProperty property = RequireIntegerProperty(
                serialized, "_targetRange", AssetDatabase.GetAssetPath(config));
            property.intValue = range;
            serialized.ApplyModifiedProperties();
        }

        private static SerializedProperty RequireIntegerProperty(
            SerializedObject serialized,
            string propertyName,
            string path)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Integer)
                throw new InvalidOperationException($"Required integer property '{propertyName}' is missing: {path}");
            return property;
        }

        private static void SetNodeRange(
            SkillGraphNodeRecord node,
            int maximumRange,
            int? minimumRange)
        {
            switch (node)
            {
                case SelectPrimaryTargetNodeRecord primary:
                    if (minimumRange.HasValue)
                        primary.MinRange = minimumRange.Value;
                    primary.MaxRange = maximumRange;
                    return;
                case SelectTargetPointNodeRecord point:
                    point.MaxRange = maximumRange;
                    return;
                case TeleportNodeRecord teleport:
                    teleport.MaxRange = maximumRange;
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported range node: {node.GetType().Name}");
            }
        }
    }
}
