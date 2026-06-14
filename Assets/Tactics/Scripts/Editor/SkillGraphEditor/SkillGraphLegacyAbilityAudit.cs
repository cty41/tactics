using System.Collections.Generic;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Abilities;
using UnityEditor;

namespace Tactics.Editor.SkillGraphEditor
{
    public enum LegacyAbilityReadinessStatus
    {
        ReadyForMigration,
        NeedsProjectileSemantic,
        BlockedByLegacyIncompleteImplementation,
        NeedsManualDesign,
        SpecialCase
    }

    public static class SkillGraphLegacyAbilityAudit
    {
        public const string LegacyAbilityDir = "Assets/Tactics/Battle/Abilities";

        public static List<LegacyAbilityAuditResult> RunAudit()
        {
            var results = new List<LegacyAbilityAuditResult>();
            string[] guids = AssetDatabase.FindAssets("t:AbilityConfig", new[] { LegacyAbilityDir });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<AbilityConfig>(path);
                if (asset == null)
                    continue;

                if (asset is SkillGraphAbilityConfig)
                    continue;

                var result = Evaluate(path, asset);
                result.HasSkillGraphBridge = CheckHasBridge(asset.name);
                results.Add(result);
            }

            results.Sort((a, b) => string.CompareOrdinal(a.AbilityName, b.AbilityName));
            return results;
        }

        private static bool CheckHasBridge(string abilityName)
        {
            string graphPath = $"{SkillGraphAbilityConfigGenerator.SkillGraphDir}/{abilityName}_Graph.asset";
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(graphPath);
            if (graph == null) return false;

            var bridge = SkillGraphAbilityConfigGenerator.FindAbilityConfigForGraph(graph);
            return bridge != null;
        }

        private static LegacyAbilityAuditResult Evaluate(string assetPath, AbilityConfig asset)
        {
            string name = asset.name;
            var result = new LegacyAbilityAuditResult
            {
                AssetPath = assetPath,
                AbilityName = name,
                CurrentDisplayName = asset.DisplayName,
                HasSkillGraphBridge = false
            };

            switch (name)
            {
                case "MeleeAttack":
                    result.Status = LegacyAbilityReadinessStatus.ReadyForMigration;
                    result.Reason = "Simple melee direct-damage skill with no projectile or complex trigger dependency.";
                    result.RecommendedTrack = "Batch1-LowRisk";
                    break;

                case "RangedAttack":
                case "MagicAttack":
                case "HeavyShot":
                case "Fireball":
                    result.Status = LegacyAbilityReadinessStatus.NeedsProjectileSemantic;
                    result.Reason = "Requires projectile / hit timing semantics before graph migration is stable.";
                    result.RecommendedTrack = "Batch2-Projectile";
                    break;

                case "Freeze":
                case "Mark":
                case "Counter":
                    result.Status = LegacyAbilityReadinessStatus.NeedsManualDesign;
                    result.Reason = "State or trigger-driven skill; migrate after buff/trigger semantics are confirmed in graph workflow.";
                    result.RecommendedTrack = "Batch3-StateTrigger";
                    break;

                case "ChargeAttack":
                case "Uppercut":
                    result.Status = LegacyAbilityReadinessStatus.BlockedByLegacyIncompleteImplementation;
                    result.Reason = "Legacy behavior is known incomplete or under active redesign; do not batch-migrate before behavior parity is defined.";
                    result.RecommendedTrack = "Batch4-HighRisk";
                    break;

                case "ChargeHeal":
                case "MeleeHeal":
                    result.Status = LegacyAbilityReadinessStatus.NeedsManualDesign;
                    result.Reason = "Healing / move-then-heal behavior needs graph semantics and validation strategy before safe migration.";
                    result.RecommendedTrack = "Batch4-HighRisk";
                    break;

                case "Move":
                    result.Status = LegacyAbilityReadinessStatus.SpecialCase;
                    result.Reason = "Core movement ability should not be migrated together with normal combat abilities.";
                    result.RecommendedTrack = "SpecialCase-Move";
                    break;

                default:
                    result.Status = LegacyAbilityReadinessStatus.NeedsManualDesign;
                    result.Reason = "Unclassified ability requires manual migration review.";
                    result.RecommendedTrack = "ManualReview";
                    break;
            }

            return result;
        }
    }

    public class LegacyAbilityAuditResult
    {
        public string AssetPath;
        public string AbilityName;
        public string CurrentDisplayName;
        public LegacyAbilityReadinessStatus Status;
        public string Reason;
        public string RecommendedTrack;
        public bool HasSkillGraphBridge;
    }
}
