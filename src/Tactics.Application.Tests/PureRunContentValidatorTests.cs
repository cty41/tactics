using NUnit.Framework;
using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;
using Tactics.Core.Units;

namespace Tactics.Application.Tests;

public sealed class PureRunContentValidatorTests
{
    [Test]
    public void MissingStartingSkillReference_IsRejectedBeforeRunStarts()
    {
        PureRunDefinition definition = Definition(new ContentId("skill.missing.lv1"));

        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            PureRunContentValidator.ValidateSkillReferences(definition,
                [new ContentId("skill.mage.fireball.lv1")]))!;

        Assert.That(error.Message, Does.Contain("skill.missing.lv1"));
    }

    private static PureRunDefinition Definition(ContentId amazonSkill) => new(
        new ContentId("run.pure-run.three-encounter-v1"),
        [
            new ContentId("encounter.pure-run.n1"),
            new ContentId("encounter.pure-run.n2"),
            new ContentId("encounter.pure-run.n3")
        ],
        [
            new PureRunPartyTemplate("mage", new ContentId("unit.pure-run.mage"),
                new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5, 5, 5, 6, 5, 5)),
            new PureRunPartyTemplate("necromancer", new ContentId("unit.pure-run.necromancer"),
                new ContentId("skill.mage.fireball.lv1"), new UnitAttributes(5, 5, 5, 5, 6, 5)),
            new PureRunPartyTemplate("amazon", new ContentId("unit.pure-run.amazon"),
                amazonSkill, new UnitAttributes(5, 6, 5, 5, 5, 5))
        ]);
}
