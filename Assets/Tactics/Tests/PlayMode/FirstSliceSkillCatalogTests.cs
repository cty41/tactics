using System.Linq;
using NUnit.Framework;
using Tactics.Common.Battle;
using Tactics.Common.Units.Classes;
using Tactics.Roster;

namespace Tactics.Tests.PlayMode
{
    /// <summary>
    /// Guards the first-slice skill catalog and its unlock constraints.
    /// </summary>
    public class FirstSliceSkillCatalogTests
    {
        [Test]
        public void Catalog_ContainsExactlySixSkillsPerFirstSliceRole()
        {
            Assert.That(FirstSliceSkillCatalog.All.Count(skill => skill.RoleType == RoleType.Mage), Is.EqualTo(6));
            Assert.That(FirstSliceSkillCatalog.All.Count(skill => skill.RoleType == RoleType.Necromancer), Is.EqualTo(6));
            Assert.That(FirstSliceSkillCatalog.All.Count(skill => skill.RoleType == RoleType.Amazon), Is.EqualTo(6));
        }

        [Test]
        public void CanLearnSkill_RequiresAdvancedPrerequisiteAndAttributeThreshold()
        {
            var amazon = CharacterDefinition.CreateDefault("amazon", "Amazon", agilityBonus: 2, roleType: RoleType.Amazon);
            Assert.That(FirstSliceSkillCatalog.TryGet("amazon.multi_stab", out var multiStab), Is.True);
            Assert.That(SkillSystem.CanLearnSkill(amazon, multiStab), Is.False);

            amazon.LearnedSkills.Add(new CharacterDefinition.LearnedSkill
            {
                SkillId = "amazon.thrust",
                SkillType = SkillType.Active,
                Level = 1
            });
            Assert.That(SkillSystem.CanLearnSkill(amazon, multiStab), Is.True);
        }

        [Test]
        public void UpgradeSkill_UsesCatalogSpecificMaxLevel()
        {
            var necromancer = CharacterDefinition.CreateDefault("necromancer", "Necromancer", roleType: RoleType.Necromancer);
            necromancer.LearnedSkills.Add(new CharacterDefinition.LearnedSkill
            {
                SkillId = "necromancer.summon_skeleton",
                SkillType = SkillType.Active,
                Level = 2
            });

            Assert.That(SkillSystem.UpgradeSkill(necromancer, "necromancer.summon_skeleton"), Is.True);
            Assert.That(SkillSystem.UpgradeSkill(necromancer, "necromancer.summon_skeleton"), Is.False);
        }

        [Test]
        public void GetUpgradeableSkills_IncludesFirstSliceLevelTwoSkills()
        {
            var amazon = CharacterDefinition.CreateDefault("amazon", "Amazon", agilityBonus: 2, roleType: RoleType.Amazon);
            amazon.LearnedSkills.Add(new CharacterDefinition.LearnedSkill
            {
                SkillId = "amazon.thrust",
                SkillType = SkillType.Active,
                Level = 2
            });

            var upgradeable = SkillDatabase.GetUpgradeableSkills(amazon);

            Assert.That(upgradeable.Select(skill => skill.Id), Does.Contain("amazon.thrust"));
        }

        [Test]
        public void CandidatePool_UsesFirstSlicePrerequisiteRules()
        {
            var amazon = CharacterDefinition.CreateDefault("amazon", "Amazon", agilityBonus: 2, roleType: RoleType.Amazon);
            amazon.LearnedSkills.Add(new CharacterDefinition.LearnedSkill
            {
                SkillId = "amazon.thrust",
                SkillType = SkillType.Active,
                Level = 1
            });

            var candidates = SkillDatabase.GetRandomSkillsForSelection(amazon, SkillType.Active, 1, 3);

            Assert.That(candidates.Select(skill => skill.Id), Does.Contain("amazon.multi_stab"));
            Assert.That(candidates.Select(skill => skill.Id), Does.Not.Contain("amazon.thrust"));
        }
    }
}
