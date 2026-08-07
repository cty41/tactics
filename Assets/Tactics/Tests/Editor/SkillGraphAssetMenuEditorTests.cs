using System.Linq;
using NUnit.Framework;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Abilities;
using Tactics.Editor.SkillGraphEditor;
using UnityEditor;

namespace Tactics.Tests.Editor
{
    public sealed class SkillGraphAssetMenuEditorTests
    {
        private const string GraphRoot = "Assets/Tactics/Battle/Abilities/SkillGraphs";

        [Test]
        public void CreateSampleGraphs_IsReentrantAndUsesPublishedCombatValues()
        {
            const string chargePath = GraphRoot + "/ChargeStrike_Lv1.asset";
            const string areaPath = GraphRoot + "/AreaBlast_Lv1.asset";
            string chargeGuid = AssetDatabase.AssetPathToGUID(chargePath);
            string areaGuid = AssetDatabase.AssetPathToGUID(areaPath);
            string[] chargeEdgeIds = GetEdgeIds(chargePath);
            string[] areaEdgeIds = GetEdgeIds(areaPath);
            Assert.That(chargeGuid, Is.Not.Empty);
            Assert.That(areaGuid, Is.Not.Empty);

            SkillGraphAssetMenu.CreateSampleGraphs();
            AssertPublishedGraph(chargePath, expectedDamage: 8f, expectedNodeCount: 6);
            AssertChargeKnockbackDuration(chargePath, expectedDuration: 0.5f);
            AssertPublishedGraph(areaPath, expectedDamage: 6f, expectedNodeCount: 6);
            AssertPublishedBridge(chargePath, "ChargeStrike_Lv1_Ability");
            AssertPublishedBridge(areaPath, "AreaBlast_Lv1_Ability");
            Assert.That(GetEdgeIds(chargePath), Is.EqualTo(chargeEdgeIds));
            Assert.That(GetEdgeIds(areaPath), Is.EqualTo(areaEdgeIds));

            SkillGraphAssetMenu.CreateSampleGraphs();
            AssertPublishedGraph(chargePath, expectedDamage: 8f, expectedNodeCount: 6);
            AssertChargeKnockbackDuration(chargePath, expectedDuration: 0.5f);
            AssertPublishedGraph(areaPath, expectedDamage: 6f, expectedNodeCount: 6);
            AssertPublishedBridge(chargePath, "ChargeStrike_Lv1_Ability");
            AssertPublishedBridge(areaPath, "AreaBlast_Lv1_Ability");
            Assert.That(AssetDatabase.AssetPathToGUID(chargePath), Is.EqualTo(chargeGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(areaPath), Is.EqualTo(areaGuid));
            Assert.That(GetEdgeIds(chargePath), Is.EqualTo(chargeEdgeIds));
            Assert.That(GetEdgeIds(areaPath), Is.EqualTo(areaEdgeIds));
        }

        private static void AssertPublishedGraph(string path, float expectedDamage, int expectedNodeCount)
        {
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(path);
            Assert.That(graph, Is.Not.Null, path);
            Assert.That(graph.Nodes.Count, Is.EqualTo(expectedNodeCount), path);
            var damage = graph.Nodes.OfType<ApplyDamageNodeRecord>().SingleOrDefault();
            Assert.That(damage, Is.Not.Null, path);
            Assert.That(damage.BaseDamage, Is.EqualTo(expectedDamage), path);
        }

        private static void AssertChargeKnockbackDuration(string path, float expectedDuration)
        {
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(path);
            var knockback = graph.Nodes.OfType<ApplyKnockbackNodeRecord>().SingleOrDefault();
            Assert.That(knockback, Is.Not.Null, path);
            Assert.That(knockback.Duration, Is.EqualTo(expectedDuration), path);
        }

        private static string[] GetEdgeIds(string path)
        {
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(path);
            return graph.Edges.Select(edge => edge.EdgeId).ToArray();
        }

        private static void AssertPublishedBridge(string graphPath, string configName)
        {
            SkillGraphAsset graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(graphPath);
            string configPath =
                $"Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/{configName}.asset";
            SkillGraphAbilityConfig config = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(configPath);

            Assert.That(config, Is.Not.Null, configPath);
            Assert.That(config.SkillGraph, Is.SameAs(graph),
                $"{configPath} must keep its canonical graph bridge after a sample rebuild.");
        }
    }
}
