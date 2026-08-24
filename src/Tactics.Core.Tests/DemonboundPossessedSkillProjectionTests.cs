using NUnit.Framework;
using Tactics.Core.Content;
using Tactics.Core.Skills;

namespace Tactics.Core.Tests;

[TestFixture]
public class DemonboundPossessedSkillProjectionTests
{
    private static readonly ContentId BaneBranch = new("skill.demonbound.bane");

    private static SkillDefinition Skill(string contentId, int level, SkillKind kind = SkillKind.Active,
        SkillExecutionKind execution = SkillExecutionKind.Bane, string branchId = "skill.demonbound.bane") => new(
        new ContentId(contentId), "test." + contentId, SkillRole.Demonbound, kind, level, 3, 1, 2,
        execution, 5, SkillDamageKind.Magical, branchId: branchId,
        executionProfile: new SkillExecutionProfile(CorruptionCost: 3));

    [Test]
    public void UnpossessedSkills_AreReturnedUnchanged()
    {
        SkillDefinition lv1 = Skill("skill.demonbound.bane.lv1", 1);
        SkillDefinition meditation = Skill("skill.demonbound.meditation", 1,
            execution: SkillExecutionKind.Meditation, branchId: "skill.demonbound.meditation");

        IReadOnlyList<SkillDefinition> projected = DemonboundPossessedSkillProjection.Project(
            [lv1, meditation], Catalog(lv1, meditation), isPossessed: false);

        Assert.That(projected.Select(skill => skill.ContentId), Is.EqualTo([lv1.ContentId, meditation.ContentId]));
    }

    [Test]
    public void PossessedLearnedSkill_IsRaisedToHighestPublishedBranchLevel()
    {
        SkillDefinition lv1 = Skill("skill.demonbound.bane.lv1", 1);
        SkillDefinition lv2 = Skill("skill.demonbound.bane.lv2", 2);
        SkillDefinition lv3 = Skill("skill.demonbound.bane.lv3", 3);

        IReadOnlyList<SkillDefinition> projected = DemonboundPossessedSkillProjection.Project(
            [lv1], Catalog(lv1, lv2, lv3), isPossessed: true);

        Assert.Multiple(() =>
        {
            Assert.That(projected, Has.Count.EqualTo(1));
            Assert.That(projected[0].ContentId, Is.EqualTo(lv3.ContentId));
            Assert.That(projected[0].Level, Is.EqualTo(3));
        });
    }

    [Test]
    public void Meditation_IsNeverProjected()
    {
        SkillDefinition meditation = Skill("skill.demonbound.meditation", 1,
            execution: SkillExecutionKind.Meditation, branchId: "skill.demonbound.meditation");

        IReadOnlyList<SkillDefinition> projected = DemonboundPossessedSkillProjection.Project(
            [meditation], Catalog(meditation), isPossessed: true);

        Assert.That(projected.Single().ContentId, Is.EqualTo(meditation.ContentId));
    }

    [Test]
    public void Projection_NeverAddsUnlearnedSkills()
    {
        SkillDefinition lv1 = Skill("skill.demonbound.bane.lv1", 1);
        SkillDefinition lv2 = Skill("skill.demonbound.bane.lv2", 2);
        SkillDefinition cleave = Skill("skill.demonbound.cleave.lv1", 1, branchId: "skill.demonbound.cleave");

        IReadOnlyList<SkillDefinition> projected = DemonboundPossessedSkillProjection.Project(
            [lv1], Catalog(lv1, lv2, cleave), isPossessed: true);

        Assert.That(projected.Select(skill => skill.ContentId), Is.EquivalentTo([lv2.ContentId]));
        Assert.That(projected.Any(skill => skill.BranchId == "skill.demonbound.cleave"), Is.False);
    }

    private static IReadOnlyDictionary<ContentId, SkillDefinition> Catalog(params SkillDefinition[] skills) =>
        skills.ToDictionary(skill => skill.ContentId);
}