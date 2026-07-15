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

        [Test]
        public void PureRunBootstrap_CreatesFixedNeutralPartyWithSeededBranches()
        {
            var state = PlayerAdventureStateStore.CreatePureRunState(1234);

            Assert.That(state.IsPureRun, Is.True);
            Assert.That(state.RunSeed, Is.EqualTo(1234));
            Assert.That(state.Roster.Select(character => character.RoleType), Is.EqualTo(new[]
            {
                RoleType.Mage,
                RoleType.Necromancer,
                RoleType.Amazon
            }));
            Assert.That(state.ActivePartyCharacterIds, Is.EqualTo(state.Roster.Select(character => character.Id)));

            foreach (var character in state.Roster)
            {
                Assert.That(character.Level, Is.EqualTo(1));
                Assert.That(character.Strength, Is.EqualTo(5));
                Assert.That(character.Agility, Is.EqualTo(5));
                Assert.That(character.Constitution, Is.EqualTo(5));
                Assert.That(character.Intelligence, Is.EqualTo(5));
                Assert.That(character.Charisma, Is.EqualTo(5));
                Assert.That(character.Luck, Is.EqualTo(5));
                Assert.That(character.Speed, Is.EqualTo(5f));
                Assert.That(character.StartingBranchSkillId, Is.Not.Empty);
                Assert.That(character.LearnedSkills.Select(skill => skill.SkillId),
                    Does.Contain(character.StartingBranchSkillId));
            }
        }

        [Test]
        public void PureRunProgression_LevelsExactlyOneLowestLivingCharacterPerVictory()
        {
            var state = PlayerAdventureStateStore.CreatePureRunState(27);

            for (int victory = 0; victory < 4; victory++)
                Assert.That(PureRunProgression.GrantVictoryLevel(state), Is.Not.Null);

            Assert.That(state.Roster.Select(character => character.Level), Is.EqualTo(new[] { 3, 2, 2 }));
            Assert.That(state.Roster.Select(character => character.AttributePoints), Is.EqualTo(new[] { 2, 1, 1 }));

            state.Roster[1].IsDead = true;
            var selected = PureRunProgression.SelectLowestLevelLivingCharacter(state);
            Assert.That(selected, Is.SameAs(state.Roster[2]));
        }

        [Test]
        public void PureRunProgression_GuaranteesStartingBranchAdvancedSkillOnceAndPersistsIt()
        {
            var state = PlayerAdventureStateStore.CreatePureRunState(88);
            var character = state.Roster[0];
            var advanced = FirstSliceSkillCatalog.All.Single(skill =>
                skill.RoleType == character.RoleType &&
                skill.PrerequisiteSkillId == character.StartingBranchSkillId);

            SetBaseAttribute(character, advanced.RequiredAttribute.Value, advanced.MinimumAttribute);

            var choices = PureRunProgression.BuildSkillChoices(character, state.RunSeed, 2);
            Assert.That(choices, Has.Count.EqualTo(3));
            Assert.That(choices[0].Id, Is.EqualTo(advanced.Id));
            Assert.That(PureRunProgression.MarkAdvancedGuaranteeConsumed(character, choices), Is.True);
            Assert.That(PureRunProgression.TryGetGuaranteedAdvancedSkill(character, out _), Is.False);
            Assert.That(SkillSystem.LearnSkill(character, advanced), Is.True);

            const int testSlot = 2;
            try
            {
                PlayerAdventureStateStore.Save(testSlot, state);
                var restored = PlayerAdventureStateStore.Load(testSlot);
                var restoredCharacter = restored.Roster.Single(entry => entry.Id == character.Id);
                Assert.That(restoredCharacter.HasConsumedStartingAdvancedGuarantee, Is.True);
                Assert.That(restoredCharacter.LearnedSkills.Select(skill => skill.SkillId), Does.Contain(advanced.Id));
            }
            finally
            {
                PlayerAdventureStateStore.Delete(testSlot);
            }
        }

        private static void SetBaseAttribute(CharacterDefinition character, AttributeType attribute, int value)
        {
            switch (attribute)
            {
                case AttributeType.Strength: character.Strength = value; break;
                case AttributeType.Agility: character.Agility = value; break;
                case AttributeType.Constitution: character.Constitution = value; break;
                case AttributeType.Intelligence: character.Intelligence = value; break;
                case AttributeType.Charisma: character.Charisma = value; break;
                case AttributeType.Luck: character.Luck = value; break;
                case AttributeType.Speed: character.Speed = value; break;
            }
        }
    }
}
