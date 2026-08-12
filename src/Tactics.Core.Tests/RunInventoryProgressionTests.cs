using NUnit.Framework;
using Tactics.Core.Content;
using Tactics.Core.Items;
using Tactics.Core.Runs;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class RunInventoryProgressionTests
{
    [Test]
    public void CarryReplacesTheSingleSlotAndPreservesInstanceOwnership()
    {
        PureRunState state = State();
        var service = new RunInventoryProgressionService();
        RunMutationResult result = service.Carry(state, state.Revision, "mage", new ItemInstanceId("potion-2"));
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.Party[0].CarriedConsumables.Single().InstanceId.Value, Is.EqualTo("potion-2"));
            Assert.That(result.State.BackpackConsumables.Single().InstanceId.Value, Is.EqualTo("potion-1"));
            Assert.That(result.State.Revision, Is.EqualTo(state.Revision + 1));
        });
    }

    [Test]
    public void ProgressionRaisesOneAttributeAndUpgradesExistingBranch()
    {
        PureRunState state = State(withProgression: true);
        SkillDefinition levelTwo = new(new ContentId("skill.mage.fireball.lv2"), "fireball", SkillRole.Mage,
            SkillKind.Active, 2, 7, 1, 4, SkillExecutionKind.Fireball, 4, SkillDamageKind.Magical,
            branchId: "mage.fireball", prerequisiteContentId: new ContentId("skill.mage.fireball.lv1"));
        UnitAttributes attributes = state.Party[0].Attributes;
        var raised = new UnitAttributes(attributes.Strength, attributes.Agility, attributes.Constitution,
            attributes.Intelligence + 1, attributes.Charisma, attributes.Luck);
        RunMutationResult result = new RunInventoryProgressionService().CompleteProgression(state, state.Revision,
            "progression:n1", raised, levelTwo.ContentId, new Dictionary<ContentId, SkillDefinition> { [levelTwo.ContentId] = levelTwo });
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.Party[0].Level, Is.EqualTo(2));
            Assert.That(result.State.Party[0].LearnedSkillStates.Single().Level, Is.EqualTo(2));
            Assert.That(result.State.PendingProgression, Is.Empty);
        });
    }

    [Test]
    public void StaleRevisionLeavesInventoryUntouched()
    {
        PureRunState state = State();
        RunMutationResult result = new RunInventoryProgressionService().Unload(state, state.Revision - 1, "mage");
        Assert.That(result.RejectionCode, Is.EqualTo("run.revision_mismatch"));
        Assert.That(result.State, Is.SameAs(state));
    }

    private static PureRunState State(bool withProgression = false)
    {
        var attributes = new UnitAttributes(3, 3, 3, 5, 4, 2);
        var carried = new BattleConsumableState(new ItemInstanceId("potion-1"), new ContentId("item.consumable.life-potion"), 1, 1);
        RunCharacterState Character(string id, string unit, string skill) => new(id, new ContentId(unit), 1, attributes, 20, 20, 12, 12, false,
            new[] { new ContentId(skill) }, carriedConsumables: id == "mage" ? new[] { carried } : null);
        RunCharacterState[] party =
        {
            Character("mage", "unit.pure-run.mage", "skill.mage.fireball.lv1"),
            Character("necromancer", "unit.pure-run.necromancer", "skill.necromancer.summon-skeleton.lv1"),
            Character("amazon", "unit.pure-run.amazon", "skill.amazon.thrust.lv1")
        };
        var backpack = new[] { new BattleConsumableState(new ItemInstanceId("potion-2"), new ContentId("item.consumable.mana-potion"), 1, 1) };
        PendingProgression[] pending = withProgression ? new[] { new PendingProgression("progression:n1", "n1", "mage") } : Array.Empty<PendingProgression>();
        return new PureRunState("run-1", 7, 3, PureRunPhase.Ready, 1, new ContentId("encounter.pure-run.n2"), party,
            backpackConsumables: backpack, pendingProgression: pending);
    }
}
