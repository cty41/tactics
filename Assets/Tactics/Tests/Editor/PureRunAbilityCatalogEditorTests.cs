using System.Linq;
using NUnit.Framework;
using Tactics.Common.Battle;
using Tactics.Common.Units.Abilities;
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
    }
}
