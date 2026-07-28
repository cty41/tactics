using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tactics.Common.Battle;
using Tactics.Common.Units.Classes;
using Tactics.Roster;

namespace Tactics.Tests.Editor
{
    /// <summary>Guards for the Pure Run starting-branch skill catalog and selection flow.</summary>
    public sealed class StartingBranchSkillTests
    {
        [Test]
        public void GetStartingBranchSkillIds_ReturnsThreePerPureRunRole()
        {
            Assert.That(PureRunAbilityCatalog.GetStartingBranchSkillIds(RoleType.Mage),
                Is.EquivalentTo(new[] { "mage.fireball", "mage.ice_bolt", "mage.lightning" }));
            Assert.That(PureRunAbilityCatalog.GetStartingBranchSkillIds(RoleType.Necromancer),
                Is.EquivalentTo(new[]
                {
                    "necromancer.summon_skeleton",
                    "necromancer.amplify_damage",
                    "necromancer.bone_spear"
                }));
            Assert.That(PureRunAbilityCatalog.GetStartingBranchSkillIds(RoleType.Amazon),
                Is.EquivalentTo(new[] { "amazon.thrust", "amazon.poison_spear", "amazon.combat_techniques" }));
            Assert.That(PureRunAbilityCatalog.GetStartingBranchSkillIds(RoleType.Barbarian), Is.Empty);
        }

        [Test]
        public void GetStartingBranchSkillIds_AllEntriesAreFormalNoPrerequisite()
        {
            foreach (var roleType in new[] { RoleType.Mage, RoleType.Necromancer, RoleType.Amazon })
            {
                foreach (string skillId in PureRunAbilityCatalog.GetStartingBranchSkillIds(roleType))
                {
                    Assert.That(PureRunAbilityCatalog.TryGet(skillId, out var definition), Is.True, skillId);
                    Assert.That(definition.IsUpgradeVisible, Is.True, skillId);
                    Assert.That(definition.RoleType, Is.EqualTo(roleType), skillId);
                    Assert.That(definition.Skill.PrerequisiteSkillId, Is.Null.Or.Empty, skillId);
                }
            }
        }

        [Test]
        public void ApplyStartingBranchSkill_SwapsBranchAndLearnedEntry()
        {
            var state = PlayerAdventureStateStore.CreatePureRunState(12345);
            var mage = state.Roster[0];
            Assert.That(mage.RoleType, Is.EqualTo(RoleType.Mage));

            bool applied = PlayerAdventureStateStore.ApplyStartingBranchSkill(mage, "mage.lightning");

            Assert.That(applied, Is.True);
            Assert.That(mage.StartingBranchSkillId, Is.EqualTo("mage.lightning"));

            var formalEntries = mage.LearnedSkills
                .Where(learned =>
                    learned != null &&
                    PureRunAbilityCatalog.TryGet(learned.SkillId, out var definition) &&
                    definition.IsUpgradeVisible &&
                    definition.RoleType == mage.RoleType)
                .ToList();
            Assert.That(formalEntries.Count, Is.EqualTo(1));
            Assert.That(formalEntries[0].SkillId, Is.EqualTo("mage.lightning"));
            Assert.That(formalEntries[0].Level, Is.EqualTo(1));
        }

        [Test]
        public void ApplyStartingBranchSkill_DoesNotTouchAttributes()
        {
            var state = PlayerAdventureStateStore.CreatePureRunState(12345);
            var necromancer = state.Roster[1];
            Assert.That(necromancer.RoleType, Is.EqualTo(RoleType.Necromancer));

            var before = SnapshotAttributes(necromancer);
            bool applied = PlayerAdventureStateStore.ApplyStartingBranchSkill(necromancer, "necromancer.bone_spear");
            var after = SnapshotAttributes(necromancer);

            Assert.That(applied, Is.True);
            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void ApplyStartingBranchSkill_RejectsForeignOrInvalidSkill()
        {
            var state = PlayerAdventureStateStore.CreatePureRunState(12345);
            var mage = state.Roster[0];
            Assert.That(mage.RoleType, Is.EqualTo(RoleType.Mage));

            foreach (string skillId in new[] { "amazon.thrust", "mage.summon_fire_demon", "nonexistent" })
            {
                string branchBefore = mage.StartingBranchSkillId;
                var learnedBefore = mage.LearnedSkills
                    .Select(learned => (learned.SkillId, learned.SkillType, learned.Level))
                    .ToList();

                bool applied = PlayerAdventureStateStore.ApplyStartingBranchSkill(mage, skillId);

                Assert.That(applied, Is.False, skillId);
                Assert.That(mage.StartingBranchSkillId, Is.EqualTo(branchBefore), skillId);
                Assert.That(
                    mage.LearnedSkills.Select(learned => (learned.SkillId, learned.SkillType, learned.Level)).ToList(),
                    Is.EqualTo(learnedBefore),
                    skillId);
            }
        }

        private static Dictionary<string, int> SnapshotAttributes(CharacterDefinition character)
        {
            return new Dictionary<string, int>
            {
                ["Strength"] = character.Strength,
                ["Agility"] = character.Agility,
                ["Constitution"] = character.Constitution,
                ["Intelligence"] = character.Intelligence,
                ["Charisma"] = character.Charisma,
                ["Luck"] = character.Luck,
                ["AttributePoints"] = character.AttributePoints,
                ["MaxHp"] = character.MaxHp,
                ["CurrentHp"] = character.CurrentHp
            };
        }
    }
}
