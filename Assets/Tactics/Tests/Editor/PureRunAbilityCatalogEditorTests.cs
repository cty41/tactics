using System.Linq;
using NUnit.Framework;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Battle;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Abilities;
using Tactics.Editor.SkillGraphEditor;
using UnityEditor;

namespace Tactics.Tests.Editor
{
    /// <summary>Editor-time release guards for stable Pure Run skill metadata and published assets.</summary>
    public sealed class PureRunAbilityCatalogEditorTests
    {
        [Test]
        public void FormalSkills_HaveUniqueStableIdsAndValidPrerequisites()
        {
            var skills = PureRunAbilityCatalog.FormalSkills.ToList();

            Assert.That(skills.Select(skill => skill.Id).Distinct().Count(), Is.EqualTo(skills.Count));
            foreach (var definition in skills)
            {
                Assert.That(definition.Id, Does.Contain("."), definition.Id);
                Assert.That(definition.MaxSkillLevel, Is.GreaterThanOrEqualTo(1), definition.Id);
                Assert.That(definition.IsLevelImplemented(1), Is.True,
                    $"{definition.Id} must publish its first playable level.");

                string prerequisiteId = definition.Skill.PrerequisiteSkillId;
                if (string.IsNullOrEmpty(prerequisiteId))
                    continue;

                Assert.That(PureRunAbilityCatalog.TryGet(prerequisiteId, out var prerequisite), Is.True, definition.Id);
                Assert.That(prerequisite.RoleType, Is.EqualTo(definition.RoleType), definition.Id);
            }
        }

        [Test]
        public void PublishedLevels_AreContiguousAndReferenceLoadableAbilityConfigs()
        {
            foreach (var definition in PureRunAbilityCatalog.All)
            {
                var publishedLevels = definition.AbilityConfigPaths.Keys.OrderBy(level => level).ToList();
                if (publishedLevels.Count == 0)
                    continue;

                Assert.That(publishedLevels, Is.EqualTo(Enumerable.Range(1, publishedLevels.Count)), definition.Id);
                foreach (string path in definition.AbilityConfigPaths.Values)
                {
                    Assert.That(AssetDatabase.LoadAssetAtPath<AbilityConfig>(path), Is.Not.Null, path);
                }
            }

            Assert.That(PureRunAbilityCatalog.TryGet("mage.fireball", out var fireball), Is.True);
            Assert.That(fireball.IsLevelImplemented(1), Is.True);
            Assert.That(fireball.IsLevelImplemented(2), Is.True);
            Assert.That(fireball.IsLevelImplemented(3), Is.True,
                "Mage Slice publishes the ignite detonation level together with its real graph asset.");
        }

        [Test]
        public void BaseAttackPaths_ReferenceLoadableAbilityConfigs()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<AbilityConfig>(PureRunAbilityCatalog.MagicBaseAttackPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AbilityConfig>(PureRunAbilityCatalog.MeleeBaseAttackPath), Is.Not.Null);
        }

        [TestCase("Fireball_Lv1_Ability", 4, "primary", 1)]
        [TestCase("Fireball_Lv2_Ability", 4, "primary", 1)]
        [TestCase("Fireball_Lv3_Ability", 4, "primary", 1)]
        [TestCase("IceBolt_Graph_Ability", 4, "primary", 1)]
        [TestCase("IceBolt_Lv2_Graph_Ability", 4, "primary", 1)]
        [TestCase("IceBolt_Lv3_Graph_Ability", 4, "primary", 1)]
        [TestCase("Lightning_Graph_Ability", 4, "primary", 1)]
        [TestCase("Lightning_Lv2_Graph_Ability", 4, "primary", 1)]
        [TestCase("Lightning_Lv3_Graph_Ability", 4, "primary", 1)]
        [TestCase("SummonFireDemon_Graph_Ability", 3, "none", 0)]
        [TestCase("SummonFireDemon_Lv2_Graph_Ability", 3, "none", 0)]
        [TestCase("Teleport_Graph_Ability", 4, "teleport", 0)]
        [TestCase("Teleport_Lv2_Graph_Ability", 4, "teleport", 0)]
        [TestCase("Curse_Graph_Ability", 4, "primary", 1)]
        [TestCase("Curse_Lv2_Graph_Ability", 4, "point", 0)]
        [TestCase("Curse_Lv3_Graph_Ability", 4, "point", 0)]
        [TestCase("BoneSpear_Graph_Ability", 4, "primary", 1)]
        [TestCase("BoneSpear_Lv2_Graph_Ability", 4, "primary", 1)]
        [TestCase("BoneSpear_Lv3_Graph_Ability", 4, "point", 0)]
        [TestCase("FearCurse_Graph_Ability", 4, "primary", 1)]
        [TestCase("FearCurse_Lv2_Graph_Ability", 4, "point", 0)]
        [TestCase("SummonSkeleton_Graph_Ability", 999, "corpse", 0)]
        [TestCase("SummonSkeleton_Lv2_Graph_Ability", 999, "corpse", 0)]
        [TestCase("SummonSkeleton_Lv3_Graph_Ability", 999, "corpse", 0)]
        [TestCase("SkeletonMage_Graph_Ability", 999, "corpse", 0)]
        [TestCase("SkeletonMage_Lv2_Graph_Ability", 999, "corpse", 0)]
        [TestCase("PoisonSpear_Graph_Ability", 5, "primary", 1)]
        [TestCase("PoisonSpear_Lv2_Graph_Ability", 5, "primary", 1)]
        [TestCase("PoisonSpear_Lv3_Graph_Ability", 5, "primary", 1)]
        [TestCase("RecoverSpear_Graph_Ability", 5, "point", 0)]
        [TestCase("RecoverSpear_Lv2_Graph_Ability", 5, "point", 0)]
        [TestCase("Thrust_Graph_Ability", 2, "primary", 1)]
        [TestCase("Thrust_Lv2_Graph_Ability", 3, "primary", 1)]
        [TestCase("Thrust_Lv3_Graph_Ability", 3, "primary", 1)]
        [TestCase("MultiStab_Graph_Ability", 3, "primary", 1)]
        [TestCase("MultiStab_Lv2_Graph_Ability", 3, "primary", 1)]
        [TestCase("Decoy_Graph_Ability", 2, "point", 0)]
        [TestCase("Decoy_Lv2_Graph_Ability", 2, "point", 0)]
        public void PlayerSkillRanges_MatchFixedBoardCalibration(
            string assetName,
            int expectedRange,
            string selectorKind,
            int expectedMinimumRange)
        {
            SkillGraphAbilityConfig config = LoadConfig(assetName);

            Assert.That(config.TargetRange, Is.EqualTo(expectedRange), $"{assetName} config range");
            AssertSelectorRange(config, selectorKind, expectedMinimumRange, expectedRange);
        }

        [TestCase("ChargeStrike_Lv1_Ability", 3, "charge", 0)]
        [TestCase("RangedAttack_Graph_Ability", 4, "primary", 2)]
        [TestCase("HeavyShot_Graph_Ability", 4, "primary", 0)]
        [TestCase("AreaBlast_Lv1_Ability", 3, "area", 0)]
        [TestCase("Curse_Graph_Ability", 4, "primary", 1)]
        public void MonsterSkillRanges_MatchFixedBoardCalibration(
            string assetName,
            int expectedRange,
            string selectorKind,
            int expectedMinimumRange)
        {
            SkillGraphAbilityConfig config = LoadConfig(assetName);

            Assert.That(config.TargetRange, Is.EqualTo(expectedRange), $"{assetName} config range");
            AssertSelectorRange(config, selectorKind, expectedMinimumRange, expectedRange);
            if (selectorKind == "charge")
                Assert.That(config.SkillGraph.Nodes.OfType<DashToTargetNodeRecord>().Single().MaxRange,
                    Is.EqualTo(expectedRange), $"{assetName} dash range");
            if (selectorKind == "area")
                Assert.That(config.SkillGraph.Nodes.OfType<CollectTargetsInAreaNodeRecord>().Single().Radius,
                    Is.EqualTo(2), $"{assetName} area radius must remain unchanged");
        }

        [TestCase("RangedBrain", 2, 4)]
        [TestCase("AOEBrain", 2, 3)]
        [TestCase("SupportBrain", 2, 3)]
        public void EncounterAiPreferredRanges_MatchFixedBoardCalibration(
            string assetName,
            int expectedMinimumRange,
            int expectedMaximumRange)
        {
            string path = $"Assets/Tactics/AI/Encounters/{assetName}.asset";
            AiBrainAsset brain = AssetDatabase.LoadAssetAtPath<AiBrainAsset>(path);

            Assert.That(brain, Is.Not.Null, path);
            Assert.That(brain.PreferredMinimumRange, Is.EqualTo(expectedMinimumRange), path);
            Assert.That(brain.PreferredMaximumRange, Is.EqualTo(expectedMaximumRange), path);
        }

        [Test]
        public void CalibrationBuilderSource_DeclaresFixedBoardRangeContract()
        {
            Assert.That(PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, Is.EqualTo(4));
            Assert.That(PureRunRangeCalibrationAssetBuilder.ExtendedPlayerRange, Is.EqualTo(5));
            Assert.That(PureRunRangeCalibrationAssetBuilder.FireDemonSummonRange, Is.EqualTo(3));
            Assert.That(PureRunRangeCalibrationAssetBuilder.CorpseSelectionRange, Is.EqualTo(999));
            Assert.That(PureRunRangeCalibrationAssetBuilder.ChargeRange, Is.EqualTo(3));
            Assert.That(PureRunRangeCalibrationAssetBuilder.MonsterRangedMinimumRange, Is.EqualTo(2));
            Assert.That(PureRunRangeCalibrationAssetBuilder.MonsterRangedMaximumRange, Is.EqualTo(4));
            Assert.That(PureRunRangeCalibrationAssetBuilder.AreaBlastRange, Is.EqualTo(3));
            Assert.That(PureRunRangeCalibrationAssetBuilder.AreaBlastRadius, Is.EqualTo(2));
            Assert.That(PureRunRangeCalibrationAssetBuilder.PreferredMinimumRange, Is.EqualTo(2));
            Assert.That(PureRunRangeCalibrationAssetBuilder.PreferredMaximumRange, Is.EqualTo(3));
            Assert.That(PureRunRangeCalibrationAssetBuilder.RangedPreferredMaximumRange, Is.EqualTo(4));
        }

        private static SkillGraphAbilityConfig LoadConfig(string assetName)
        {
            string path = $"Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/{assetName}.asset";
            var config = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(path);
            Assert.That(config, Is.Not.Null, path);
            Assert.That(config.SkillGraph, Is.Not.Null, path);
            return config;
        }

        private static void AssertSelectorRange(
            SkillGraphAbilityConfig config,
            string selectorKind,
            int expectedMinimumRange,
            int expectedMaximumRange)
        {
            switch (selectorKind)
            {
                case "none":
                    return;
                case "primary":
                case "charge":
                    var primary = config.SkillGraph.Nodes.OfType<SelectPrimaryTargetNodeRecord>().Single();
                    Assert.That(primary.MinRange, Is.EqualTo(expectedMinimumRange), $"{config.name} minimum range");
                    Assert.That(primary.MaxRange, Is.EqualTo(expectedMaximumRange), $"{config.name} selector range");
                    return;
                case "point":
                case "area":
                    Assert.That(config.SkillGraph.Nodes.OfType<SelectTargetPointNodeRecord>().Single().MaxRange,
                        Is.EqualTo(expectedMaximumRange), $"{config.name} selector range");
                    return;
                case "teleport":
                    Assert.That(config.SkillGraph.Nodes.OfType<TeleportNodeRecord>().Single().MaxRange,
                        Is.EqualTo(expectedMaximumRange), $"{config.name} teleport range");
                    return;
                case "corpse":
                    var corpse = config.SkillGraph.Nodes.OfType<SelectCorpseTargetNodeRecord>().Single();
                    Assert.That(corpse.MinRange, Is.EqualTo(expectedMinimumRange), $"{config.name} minimum range");
                    Assert.That(corpse.MaxRange, Is.EqualTo(expectedMaximumRange),
                        $"{config.name} corpse selection is an intentional full-board exception");
                    return;
                default:
                    Assert.Fail($"Unknown selector kind: {selectorKind}");
                    return;
            }
        }
    }
}
