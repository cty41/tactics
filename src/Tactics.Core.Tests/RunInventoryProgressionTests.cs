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
    public void EquipReplacesOccupiedSlotAndKeepsEachInstanceInExactlyOnePlace()
    {
        PureRunState state = State();
        RunCharacterState mage = state.Party[0];
        var oldItem = new RunEquipmentState(new ItemInstanceId("sword-old"), new ContentId("item.equipment.sword-01"), EquipmentSlot.Weapon);
        var newItem = new RunEquipmentState(new ItemInstanceId("staff-new"), new ContentId("item.equipment.staff-01"), EquipmentSlot.Weapon);
        mage = new RunCharacterState(mage.CharacterId, mage.UnitContentId, mage.Level, mage.Attributes,
            mage.CurrentHealth, mage.MaxHealth, mage.CurrentMana, mage.MaxMana, mage.IsDead, mage.LearnedSkills,
            [oldItem], mage.CarriedConsumables, mage.LearnedSkillStates);
        state = new PureRunState(state.RunId, state.Seed, state.Revision, state.Phase, state.EncounterIndex,
            state.EncounterContentId, [mage, .. state.Party.Skip(1)], state.BackpackConsumables, [newItem]);
        UnitAttributes zero = new(0, 0, 0, 0, 0, 0);
        var definitions = new Dictionary<ContentId, EquipmentDefinition>
        {
            [oldItem.DefinitionId] = new(oldItem.DefinitionId, "old", "Old Sword", EquipmentSlot.Weapon, ItemRarity.Common, 1, zero),
            [newItem.DefinitionId] = new(newItem.DefinitionId, "new", "New Staff", EquipmentSlot.Weapon, ItemRarity.Common, 1, zero)
        };

        RunMutationResult result = new RunInventoryProgressionService().Equip(state, state.Revision,
            mage.CharacterId, newItem.InstanceId, definitions, 3f);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.Party[0].Equipment.Single().InstanceId, Is.EqualTo(newItem.InstanceId));
            Assert.That(result.State.BackpackEquipment.Single().InstanceId, Is.EqualTo(oldItem.InstanceId));
            Assert.That(result.State.Party.SelectMany(value => value.Equipment).Concat(result.State.BackpackEquipment)
                .Select(value => value.InstanceId).Distinct().Count(), Is.EqualTo(2));
        });
    }

    [Test]
    public void UnequipReprojectsDerivedHealthAndManaFromRemainingLoadout()
    {
        PureRunState state = State();
        RunCharacterState mage = state.Party[0];
        var item = new RunEquipmentState(new ItemInstanceId("armor"), new ContentId("item.equipment.armor"), EquipmentSlot.Armor);
        UnitAttributes bonus = new(0, 0, 3, 0, 2, 0);
        var definition = new EquipmentDefinition(item.DefinitionId, "armor", "Armor", EquipmentSlot.Armor,
            ItemRarity.Common, 1, bonus);
        mage = new RunCharacterState(mage.CharacterId, mage.UnitContentId, mage.Level, mage.Attributes,
            mage.CurrentHealth, mage.MaxHealth + 6, mage.CurrentMana, mage.MaxMana + 2, mage.IsDead,
            mage.LearnedSkills, [item], mage.CarriedConsumables, mage.LearnedSkillStates);
        state = new PureRunState(state.RunId, state.Seed, state.Revision, state.Phase, state.EncounterIndex,
            state.EncounterContentId, [mage, .. state.Party.Skip(1)], state.BackpackConsumables, []);

        RunMutationResult result = new RunInventoryProgressionService().Unequip(state, state.Revision,
            mage.CharacterId, EquipmentSlot.Armor, new Dictionary<ContentId, EquipmentDefinition>
            { [item.DefinitionId] = definition }, 3f);
        UnitDerivedStats expected = UnitDerivedStatRules.Calculate(mage.Attributes, 3f);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.Party[0].MaxHealth, Is.EqualTo(expected.MaxHealth));
            Assert.That(result.State.Party[0].MaxMana, Is.EqualTo(expected.MaxMana));
            Assert.That(result.State.BackpackEquipment.Single().InstanceId, Is.EqualTo(item.InstanceId));
        });
    }

    [Test]
    public void RunStateRejectsDuplicateInstanceIdentityAcrossBackpackAndLoadout()
    {
        PureRunState state = State();
        RunCharacterState mage = state.Party[0];
        BattleConsumableState duplicate = state.BackpackConsumables[0];
        mage = new RunCharacterState(mage.CharacterId, mage.UnitContentId, mage.Level, mage.Attributes,
            mage.CurrentHealth, mage.MaxHealth, mage.CurrentMana, mage.MaxMana, mage.IsDead, mage.LearnedSkills,
            mage.Equipment, [duplicate], mage.LearnedSkillStates);

        Assert.Throws<ArgumentException>(() => new PureRunState(state.RunId, state.Seed, state.Revision,
            state.Phase, state.EncounterIndex, state.EncounterContentId, [mage, .. state.Party.Skip(1)],
            state.BackpackConsumables, state.BackpackEquipment));
    }

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
            "progression:n1", raised, levelTwo.ContentId, new Dictionary<ContentId, SkillDefinition> { [levelTwo.ContentId] = levelTwo }, Definition(state));
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.State.Party[0].Level, Is.EqualTo(2));
            Assert.That(result.State.Party[0].LearnedSkillStates.Single().Level, Is.EqualTo(2));
            Assert.That(result.State.PendingProgression, Is.Empty);
        });
    }

    [Test]
    public void AttributePreviewDoesNotMutateOrAdvanceRunRevision()
    {
        PureRunState state = State(withProgression: true);
        RunCharacterState mage = state.Party[0];
        UnitAttributes proposed = new(mage.Attributes.Strength + 1, mage.Attributes.Agility,
            mage.Attributes.Constitution, mage.Attributes.Intelligence, mage.Attributes.Charisma, mage.Attributes.Luck);

        var service = new RunInventoryProgressionService();
        IReadOnlyList<SkillDefinition> offer = service.PreviewGrowthOffer(state, "progression:n1", proposed,
            new Dictionary<ContentId, SkillDefinition>(), Definition(state));

        Assert.Multiple(() =>
        {
            Assert.That(offer, Is.Empty);
            Assert.That(state.Party[0].Attributes, Is.EqualTo(mage.Attributes));
            Assert.That(state.PendingProgression.Single().ProposedAttributes, Is.Null);
            Assert.That(state.Revision, Is.EqualTo(State(withProgression: true).Revision));
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
            raised, candidate.ContentId, skills, Definition(state));
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
            new Dictionary<ContentId, SkillDefinition>(), Definition(state));
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
            "progression:n1", raised, null, skills, Definition(state));
        RunMutationResult crossRole = service.CompleteProgression(allocated.State, allocated.State.Revision,
            "progression:n1", raised, amazonSkill.ContentId, skills, Definition(state));

        Assert.That(skipped.RejectionCode, Is.EqualTo("progression.skill_required"));
        Assert.That(crossRole.RejectionCode, Is.EqualTo("progression.invalid_skill"));
    }

    [Test]
    public void ProgressionAtomicallyAcceptsFinalAttributesWithoutPersistedDraft()
    {
        PureRunState state = State(withProgression: true, frozenCharacterIds: true);
        UnitAttributes a = state.Party[0].Attributes;
        UnitAttributes raised = new(a.Strength, a.Agility, a.Constitution, a.Intelligence + 1, a.Charisma, a.Luck);

        RunMutationResult result = new RunInventoryProgressionService().CompleteProgression(
            state, state.Revision, "progression:n1", raised, null, new Dictionary<ContentId, SkillDefinition>(), Definition(state));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.State.Party[0].Attributes, Is.EqualTo(raised));
        Assert.That(result.State.PendingProgression, Is.Empty);
    }

    [Test]
    public void GrowthOfferIsDeterministicLimitedToThreeAndNeverRepeatsLearnedLevel()
    {
        PureRunState state = State(withProgression: true, frozenCharacterIds: true);
        RunCharacterState mage = state.Party[0];
        SkillDefinition Skill(string branch, int level) => new(
            new ContentId($"skill.{branch}.lv{level}"), branch, SkillRole.Mage, SkillKind.Active,
            level, 1, 1, 4, SkillExecutionKind.Fireball, 4, SkillDamageKind.Magical,
            branchId: branch, prerequisiteContentId: level == 2 ? new ContentId($"skill.{branch}.lv1") : null);
        SkillDefinition[] values =
        [
            Skill("mage.fireball", 1), Skill("mage.fireball", 2),
            Skill("mage.ice-bolt", 1), Skill("mage.lightning", 1),
            Skill("mage.ice-armor", 1), Skill("mage.teleport", 1), Skill("mage.summon-fire-demon", 1)
        ];
        var skills = values.ToDictionary(value => value.ContentId);
        var service = new RunInventoryProgressionService();

        ContentId[] first = service.GrowthOffer(state, mage, skills, Definition(state)).Select(value => value.ContentId).ToArray();
        ContentId[] replay = service.GrowthOffer(state, mage, skills, Definition(state)).Select(value => value.ContentId).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(first, Has.Length.EqualTo(3));
            Assert.That(replay, Is.EqualTo(first));
            Assert.That(first, Does.Not.Contain(new ContentId("skill.mage.fireball.lv1")));
            Assert.That(first, Does.Contain(new ContentId("skill.mage.fireball.lv2")));
        });
    }

    [Test]
    public void GrowthOfferPinsAndConsumesTheStartingBranchAdvancedGuarantee()
    {
        PureRunState state = State(withProgression: true, frozenCharacterIds: true);
        RunCharacterState mage = state.Party[0];
        ContentId starting = new("skill.mage.fireball.lv1");
        SkillDefinition fireball = new(starting,"fireball",SkillRole.Mage,SkillKind.Active,1,1,1,4,
            SkillExecutionKind.Fireball,4,SkillDamageKind.Magical,branchId:"mage.fireball");
        SkillDefinition fireballTwo = new(new ContentId("skill.mage.fireball.lv2"),"fireball2",SkillRole.Mage,SkillKind.Active,2,1,1,4,
            SkillExecutionKind.Fireball,5,SkillDamageKind.Magical,branchId:"mage.fireball",prerequisiteContentId:starting);
        SkillDefinition advanced = new(new ContentId("skill.mage.summon-fire-demon.lv1"),"advanced",SkillRole.Mage,SkillKind.Active,1,1,1,3,
            SkillExecutionKind.SummonFireDemon,0,SkillDamageKind.None,branchId:"mage.summon-fire-demon",prerequisiteBranchId:"mage.fireball");
        SkillDefinition other = new(new ContentId("skill.mage.ice-bolt.lv1"),"other",SkillRole.Mage,SkillKind.Active,1,1,1,4,
            SkillExecutionKind.IceBolt,4,SkillDamageKind.Magical,branchId:"mage.ice-bolt");
        SkillDefinition lightning = new(new ContentId("skill.mage.lightning.lv1"),"lightning",SkillRole.Mage,SkillKind.Active,1,1,1,4,
            SkillExecutionKind.Lightning,4,SkillDamageKind.Magical,branchId:"mage.lightning");
        SkillDefinition teleport = new(new ContentId("skill.mage.teleport.lv1"),"teleport",SkillRole.Mage,SkillKind.Active,1,1,1,4,
            SkillExecutionKind.Teleport,0,SkillDamageKind.None,branchId:"mage.teleport");
        SkillDefinition lockedAdvanced = new(new ContentId("skill.mage.ice-armor.lv1"),"locked",SkillRole.Mage,SkillKind.Active,1,1,0,0,
            SkillExecutionKind.IceArmor,0,SkillDamageKind.None,branchId:"mage.ice-armor",prerequisiteBranchId:"mage.ice-bolt");
        var skills = new[] { fireball, fireballTwo, advanced, other, lightning, teleport, lockedAdvanced }.ToDictionary(value=>value.ContentId);
        var service = new RunInventoryProgressionService();
        PureRunDefinition definition=Definition(state);
        SkillDefinition[] offer = service.GrowthOffer(state,mage,skills,definition).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(offer.Select(value => value.ContentId), Is.EqualTo(new[]
            {
                advanced.ContentId,
                fireballTwo.ContentId,
                new ContentId("skill.mage.lightning.lv1")
            }));
            Assert.That(offer.Select(value=>value.ContentId),Does.Not.Contain(lockedAdvanced.ContentId));
        });

        UnitAttributes a=mage.Attributes;
        UnitAttributes raised=new(a.Strength,a.Agility,a.Constitution,a.Intelligence+1,a.Charisma,a.Luck);
        RunMutationResult allocated=service.AllocateProgressionAttributes(state,state.Revision,"progression:n1",raised);
        RunMutationResult completed=service.CompleteProgression(allocated.State,allocated.State.Revision,"progression:n1",raised,
            advanced.ContentId,skills,definition);
        Assert.That(completed.Succeeded,Is.True);
        Assert.That(completed.State.AppliedTransactionKeys,Does.Contain("growth-guarantee:pure_run_mage"));

        RunLearnedSkillState[] learnedWithAdvanced = mage.LearnedSkillStates
            .Append(new RunLearnedSkillState(lockedAdvanced.BranchId, 1, lockedAdvanced.ContentId)).ToArray();
        var mageWithAdvanced = new RunCharacterState(mage.CharacterId, mage.UnitContentId, mage.Level,
            mage.Attributes, mage.CurrentHealth, mage.MaxHealth, mage.CurrentMana, mage.MaxMana, mage.IsDead,
            learnedWithAdvanced.Select(value => value.DefinitionId).ToArray(), mage.Equipment,
            mage.CarriedConsumables, learnedWithAdvanced);
        PureRunState StateWith(IReadOnlyList<string> transactions) => new(state.RunId, state.Seed, state.Revision,
            state.Phase, state.EncounterIndex, state.EncounterContentId,
            state.Party.Select(value => value.CharacterId == mage.CharacterId ? mageWithAdvanced : value).ToArray(),
            state.BackpackConsumables, state.BackpackEquipment, state.PendingProgression, transactions,
            state.Gold, state.BattlesCompleted, state.EnemiesDefeated, state.AcquiredItems, state.Checkpoint,
            state.MapState, state.NodeTransaction);
        ContentId[] suppressedByLearnedAdvanced = service.GrowthOffer(StateWith([]), mageWithAdvanced, skills, definition)
            .Select(value => value.ContentId).ToArray();
        ContentId[] suppressedByConsumedMarker = service.GrowthOffer(
                StateWith(["growth-guarantee:pure_run_mage"]), mageWithAdvanced, skills, definition)
            .Select(value => value.ContentId).ToArray();
        Assert.That(suppressedByLearnedAdvanced, Is.EqualTo(suppressedByConsumedMarker));
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

    private static PureRunDefinition Definition(PureRunState state) => new(
        new ContentId("run.test"),
        new[] { new ContentId("encounter.n1"), new ContentId("encounter.n2"), new ContentId("encounter.n3") },
        state.Party.Select(character => new PureRunPartyTemplate(character.CharacterId, character.UnitContentId,
            character.LearnedSkills.First(), character.Attributes)));
}
