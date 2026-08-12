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
        var service = new RunInventoryProgressionService();
        RunMutationResult allocated = service.AllocateProgressionAttributes(state, state.Revision, "progression:n1", raised);
        RunMutationResult result = service.CompleteProgression(allocated.State, allocated.State.Revision,
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
    public void AttributeAllocationIsPersistedBeforeSkillSelection()
    {
        PureRunState state = State(withProgression: true);
        RunCharacterState mage = state.Party[0];
        UnitAttributes proposed = new(mage.Attributes.Strength + 1, mage.Attributes.Agility,
            mage.Attributes.Constitution, mage.Attributes.Intelligence, mage.Attributes.Charisma, mage.Attributes.Luck);

        RunMutationResult result = new RunInventoryProgressionService().AllocateProgressionAttributes(
            state, state.Revision, "progression:n1", proposed);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.Party[0].Attributes, Is.EqualTo(mage.Attributes), "Allocation is a draft until skill confirmation.");
            Assert.That(result.State.PendingProgression.Single().ProposedAttributes, Is.EqualTo(proposed));
            Assert.That(result.State.PendingProgression.Single().SelectedSkillContentId, Is.Null);
        });

        var service = new RunInventoryProgressionService();
        RunMutationResult duplicate = service.AllocateProgressionAttributes(
            result.State, result.State.Revision, "progression:n1", proposed);
        Assert.That(duplicate.Succeeded, Is.True);
        Assert.That(duplicate.State, Is.SameAs(result.State));

        UnitAttributes changed = new(proposed.Strength - 1, proposed.Agility, proposed.Constitution,
            proposed.Intelligence, proposed.Charisma, proposed.Luck + 1);
        RunMutationResult changedAllocation = service.AllocateProgressionAttributes(
            result.State, result.State.Revision, "progression:n1", changed);
        Assert.That(changedAllocation.Succeeded, Is.False);
        Assert.That(changedAllocation.RejectionCode, Is.EqualTo("progression.attributes_already_allocated"));
    }

    [Test]
    public void StaleRevisionLeavesInventoryUntouched()
    {
        PureRunState state = State();
        RunMutationResult result = new RunInventoryProgressionService().Unload(state, state.Revision - 1, "mage");
        Assert.That(result.RejectionCode, Is.EqualTo("run.revision_mismatch"));
        Assert.That(result.State, Is.SameAs(state));
    }

    [Test]
    public void FrozenPureRunCharacterIdsResolveGrowthCandidatesAndCompleteProgression()
    {
        PureRunState state = State(withProgression: true, frozenCharacterIds: true);
        SkillDefinition fireball = new(new ContentId("skill.mage.fireball.lv2"), "fireball", SkillRole.Mage,
            SkillKind.Active, 2, 7, 1, 4, SkillExecutionKind.Fireball, 4, SkillDamageKind.Magical,
            branchId: "mage.fireball", prerequisiteContentId: new ContentId("skill.mage.fireball.lv1"),
            requiredAttribute: "Intelligence", minimumAttribute: 6);
        var skills = new Dictionary<ContentId, SkillDefinition> { [fireball.ContentId] = fireball };
        var service = new RunInventoryProgressionService();
        RunCharacterState mage = state.Party[0];
        SkillDefinition candidate = service.GrowthCandidates(mage, skills).Single();
        Assert.That(service.CanUnlockWithAttributePoints(mage, candidate, 1), Is.True);
        UnitAttributes a = mage.Attributes;
        UnitAttributes raised = new(a.Strength, a.Agility, a.Constitution, a.Intelligence + 1, a.Charisma, a.Luck);
        RunMutationResult allocated = service.AllocateProgressionAttributes(state, state.Revision, "progression:n1", raised);
        RunMutationResult result = service.CompleteProgression(allocated.State, allocated.State.Revision, "progression:n1",
            raised, candidate.ContentId, skills);
        Assert.Multiple(() => { Assert.That(result.Succeeded, Is.True); Assert.That(result.State.PendingProgression, Is.Empty); });
    }

    [Test]
    public void NoSkillCandidateAllowsAttributeOnlyProgression()
    {
        PureRunState state = State(withProgression: true, frozenCharacterIds: true);
        RunCharacterState mage = state.Party[0]; UnitAttributes a = mage.Attributes;
        var service = new RunInventoryProgressionService();
        UnitAttributes raised = new(a.Strength, a.Agility, a.Constitution, a.Intelligence + 1, a.Charisma, a.Luck);
        RunMutationResult allocated = service.AllocateProgressionAttributes(state, state.Revision, "progression:n1", raised);
        RunMutationResult result = service.CompleteProgression(allocated.State, allocated.State.Revision,
            "progression:n1", raised, null,
            new Dictionary<ContentId, SkillDefinition>());
        Assert.Multiple(() => { Assert.That(result.Succeeded, Is.True); Assert.That(result.State.Party[0].Level, Is.EqualTo(2)); });
    }

    [Test]
    public void ProgressionRejectsCrossRoleSkillAndCannotSkipAvailableCandidate()
    {
        PureRunState state = State(withProgression: true, frozenCharacterIds: true);
        RunCharacterState mage = state.Party[0];
        UnitAttributes a = mage.Attributes;
        UnitAttributes raised = new(a.Strength, a.Agility, a.Constitution, a.Intelligence + 1, a.Charisma, a.Luck);
        SkillDefinition mageSkill = new(new ContentId("skill.mage.fireball.lv2"), "fireball", SkillRole.Mage,
            SkillKind.Active, 2, 7, 1, 4, SkillExecutionKind.Fireball, 4, SkillDamageKind.Magical,
            branchId: "mage.fireball", prerequisiteContentId: new ContentId("skill.mage.fireball.lv1"));
        SkillDefinition amazonSkill = new(new ContentId("skill.amazon.thrust.lv1"), "thrust", SkillRole.Amazon,
            SkillKind.Active, 1, 2, 1, 2, SkillExecutionKind.Thrust, 4, SkillDamageKind.Physical,
            branchId: "amazon.thrust");
        var skills = new Dictionary<ContentId, SkillDefinition>
        {
            [mageSkill.ContentId] = mageSkill,
            [amazonSkill.ContentId] = amazonSkill
        };
        var service = new RunInventoryProgressionService();

        RunMutationResult allocated = service.AllocateProgressionAttributes(state, state.Revision, "progression:n1", raised);
        RunMutationResult skipped = service.CompleteProgression(allocated.State, allocated.State.Revision,
            "progression:n1", raised, null, skills);
        RunMutationResult crossRole = service.CompleteProgression(allocated.State, allocated.State.Revision,
            "progression:n1", raised, amazonSkill.ContentId, skills);

        Assert.That(skipped.RejectionCode, Is.EqualTo("progression.skill_required"));
        Assert.That(crossRole.RejectionCode, Is.EqualTo("progression.invalid_skill"));
    }

    [Test]
    public void ProgressionCannotBypassPersistedAttributeAllocation()
    {
        PureRunState state = State(withProgression: true, frozenCharacterIds: true);
        UnitAttributes a = state.Party[0].Attributes;
        UnitAttributes raised = new(a.Strength, a.Agility, a.Constitution, a.Intelligence + 1, a.Charisma, a.Luck);

        RunMutationResult result = new RunInventoryProgressionService().CompleteProgression(
            state, state.Revision, "progression:n1", raised, null, new Dictionary<ContentId, SkillDefinition>());

        Assert.That(result.RejectionCode, Is.EqualTo("progression.attributes_not_allocated"));
        Assert.That(result.State, Is.SameAs(state));
    }

    private static PureRunState State(bool withProgression = false, bool frozenCharacterIds = false)
    {
        var attributes = new UnitAttributes(3, 3, 3, 5, 4, 2);
        var carried = new BattleConsumableState(new ItemInstanceId("potion-1"), new ContentId("item.consumable.life-potion"), 1, 1);
        RunCharacterState Character(string id, string unit, string skill) => new(id, new ContentId(unit), 1, attributes, 20, 20, 12, 12, false,
            new[] { new ContentId(skill) }, carriedConsumables: id == "mage" ? new[] { carried } : null);
        string mageId=frozenCharacterIds?"pure_run_mage":"mage";
        RunCharacterState[] party =
        {
            Character(mageId, "unit.pure-run.mage", "skill.mage.fireball.lv1"),
            Character("necromancer", "unit.pure-run.necromancer", "skill.necromancer.summon-skeleton.lv1"),
            Character("amazon", "unit.pure-run.amazon", "skill.amazon.thrust.lv1")
        };
        var backpack = new[] { new BattleConsumableState(new ItemInstanceId("potion-2"), new ContentId("item.consumable.mana-potion"), 1, 1) };
        PendingProgression[] pending = withProgression ? new[] { new PendingProgression("progression:n1", "n1", mageId) } : Array.Empty<PendingProgression>();
        return new PureRunState("run-1", 7, 3, PureRunPhase.Ready, 1, new ContentId("encounter.pure-run.n2"), party,
            backpackConsumables: backpack, pendingProgression: pending);
    }
}
